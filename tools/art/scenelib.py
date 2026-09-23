"""Helpers for editing Unity scene YAML safely (see memory: scene-patch-id-collisions)."""
import io
import re
from collections import Counter

import yaml

SCRIPT = {
    "ObjectInteraction": "627a7bfc3dde6934984d68abb9ffcd52",
    "NPCInteraction": "520295d28482b86479187f82c9f2ccf0",
    "NpcEventController": "f6a7b8c9d0e1434fcd5e6f7a8b9c0d1e",
    "CharacterVisualController": "ab212121212121212121212121212121",
}

HEAD = "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n"


class Scene:
    def __init__(self, path):
        self.path = path
        raw = io.open(path, encoding="utf-8").read()
        self.crlf = "\r\n" in raw
        self.text = raw.replace("\r\n", "\n")
        assert self.text.startswith(HEAD)
        body = self.text[len(HEAD):]
        parts = re.split(r"(?m)^(?=--- !u!)", body)
        self.docs = [p for p in parts if p.strip()]
        ids = [int(m) for m in re.findall(r"(?m)^--- !u!\d+ &(\d+)", self.text)]
        self.next_id = max(ids + [900000000]) + 1

    # ------------------------------------------------------------ lookup
    def doc_id(self, doc):
        return int(re.match(r"--- !u!\d+ &(\d+)", doc).group(1))

    def find(self, fid):
        for i, d in enumerate(self.docs):
            if self.doc_id(d) == fid:
                return i
        raise KeyError(fid)

    def get(self, fid):
        return self.docs[self.find(fid)]

    def set(self, fid, doc):
        self.docs[self.find(fid)] = doc

    def gameobject_by_name(self, name):
        hits = [d for d in self.docs if d.startswith("--- !u!1 &") and re.search(r"(?m)^  m_Name: %s$" % re.escape(name), d)]
        assert len(hits) == 1, (name, len(hits))
        return self.doc_id(hits[0])

    def components(self, go_id):
        d = self.get(go_id)
        return [int(x) for x in re.findall(r"component: \{fileID: (\d+)\}", d)]

    def transform_of(self, go_id):
        for c in self.components(go_id):
            if self.get(c).startswith("--- !u!4 ") or self.get(c).startswith("--- !u!224 "):
                return c
        raise KeyError(go_id)

    def new_id(self):
        v = self.next_id
        self.next_id += 1
        return v

    # ------------------------------------------------------------ edits
    def remove_gameobject(self, go_id):
        ids = [go_id] + self.components(go_id)
        tr = self.transform_of(go_id)
        father = int(re.search(r"m_Father: \{fileID: (\d+)\}", self.get(tr)).group(1))
        self.docs = [d for d in self.docs if self.doc_id(d) not in ids]
        if father:
            self.remove_child(father, tr)

    def remove_child(self, parent_tr, child_tr):
        d = self.get(parent_tr)
        d2 = d.replace("  - {fileID: %d}\n" % child_tr, "")
        assert d2 != d
        if re.search(r"m_Children:\n  m_Father", d2):
            d2 = d2.replace("m_Children:\n  m_Father", "m_Children: []\n  m_Father")
        self.set(parent_tr, d2)

    def add_child(self, parent_tr, child_tr):
        d = self.get(parent_tr)
        if "m_Children: []" in d:
            d = d.replace("m_Children: []", "m_Children:\n  - {fileID: %d}" % child_tr)
        else:
            d = re.sub(r"(m_Children:\n(?:  - \{fileID: \d+\}\n)+)", lambda m: m.group(1) + "  - {fileID: %d}\n" % child_tr, d, count=1)
        self.set(parent_tr, d)
        return len(re.findall(r"  - \{fileID: \d+\}", d.split("m_Children:")[1].split("m_Father")[0])) - 1

    def set_position(self, tr, x, y):
        d = self.get(tr)
        d = re.sub(r"m_LocalPosition: \{[^}]*\}", "m_LocalPosition: {x: %s, y: %s, z: 0}" % (fmt(x), fmt(y)), d)
        self.set(tr, d)

    def set_box(self, col, w, h, trigger=None, offset=None):
        d = self.get(col)
        d = re.sub(r"(?m)^  m_Size: \{[^}]*\}", "  m_Size: {x: %s, y: %s}" % (fmt(w), fmt(h)), d)
        if trigger is not None:
            d = re.sub(r"m_IsTrigger: \d", "m_IsTrigger: %d" % (1 if trigger else 0), d)
        if offset is not None:
            d = re.sub(r"(?m)^  m_Offset: \{[^}]*\}", "  m_Offset: {x: %s, y: %s}" % (fmt(offset[0]), fmt(offset[1])), d)
        self.set(col, d)

    def add_component(self, go_id, comp_id):
        d = self.get(go_id)
        d = re.sub(r"(m_Component:\n(?:  - component: \{fileID: \d+\}\n)+)",
                   lambda m: m.group(1) + "  - component: {fileID: %d}\n" % comp_id, d, count=1)
        self.set(go_id, d)

    def append(self, doc):
        self.docs.append(doc if doc.endswith("\n") else doc + "\n")

    # ------------------------------------------------------------ builders
    def new_gameobject(self, name, parent_tr, x, y, scale=1.0):
        go, tr = self.new_id(), self.new_id()
        order = self.add_child(parent_tr, tr)
        self.append(GO.format(id=go, name=name, comps="  - component: {fileID: %d}\n" % tr))
        self.append(TR.format(id=tr, go=go, x=fmt(x), y=fmt(y), s=fmt(scale), father=parent_tr, order=order))
        return go, tr

    def add_box(self, go, w, h, trigger, ox=0, oy=0):
        cid = self.new_id()
        self.append(BOX.format(id=cid, go=go, w=fmt(w), h=fmt(h), trig=1 if trigger else 0, ox=fmt(ox), oy=fmt(oy)))
        self.add_component(go, cid)
        return cid

    def add_circle(self, go, r, trigger=True):
        cid = self.new_id()
        self.append(CIRCLE.format(id=cid, go=go, r=fmt(r), trig=1 if trigger else 0))
        self.add_component(go, cid)
        return cid

    def add_script(self, go, script, fields_yaml):
        cid = self.new_id()
        self.append(MONO.format(id=cid, go=go, guid=SCRIPT[script], fields=fields_yaml))
        self.add_component(go, cid)
        return cid

    # ------------------------------------------------------------ save
    def validate(self):
        text = self.render()
        dup = {k: v for k, v in Counter(re.findall(r"(?m)^--- !u!\d+ &(\d+)", text)).items() if v > 1}
        assert not dup, dup
        ids = set(int(x) for x in re.findall(r"(?m)^--- !u!\d+ &(\d+)", text))
        for ref in re.findall(r"\{fileID: (\d+)\}", text):
            r = int(ref)
            assert r == 0 or r in ids, "dangling local ref %d" % r
        for d in self.docs:
            body = re.sub(r"^--- !u!\d+ &\d+[^\n]*\n", "", d)
            yaml.safe_load(body)
        return True

    def render(self):
        return HEAD + "".join(self.docs)

    def save(self):
        self.validate()
        out = self.render()
        if self.crlf:
            out = out.replace("\n", "\r\n")
        io.open(self.path, "w", encoding="utf-8", newline="").write(out)
        print("saved", self.path)


def fmt(v):
    if isinstance(v, float):
        s = ("%.3f" % v).rstrip("0").rstrip(".")
        return s if s not in ("-0", "") else "0"
    return str(v)


GO = """--- !u!1 &{id}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
{comps}  m_Layer: 0
  m_Name: {name}
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
"""

TR = """--- !u!4 &{id}
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: {x}, y: {y}, z: 0}}
  m_LocalScale: {{x: {s}, y: {s}, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: {father}}}
  m_RootOrder: {order}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
"""

BOX = """--- !u!61 &{id}
BoxCollider2D:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go}}}
  m_Enabled: 1
  m_Density: 1
  m_Material: {{fileID: 0}}
  m_IsTrigger: {trig}
  m_UsedByEffector: 0
  m_UsedByComposite: 0
  m_Offset: {{x: {ox}, y: {oy}}}
  m_SpriteTilingProperty:
    border: {{x: 0, y: 0, z: 0, w: 0}}
    pivot: {{x: 0.5, y: 0.5}}
    oldSize: {{x: 1, y: 1}}
    newSize: {{x: 1, y: 1}}
    adaptiveTilingThreshold: 0.5
    drawMode: 0
    adaptiveTiling: 0
  m_AutoTiling: 0
  serializedVersion: 2
  m_Size: {{x: {w}, y: {h}}}
  m_EdgeRadius: 0
"""

CIRCLE = """--- !u!58 &{id}
CircleCollider2D:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go}}}
  m_Enabled: 1
  m_Density: 1
  m_Material: {{fileID: 0}}
  m_IsTrigger: {trig}
  m_UsedByEffector: 0
  m_UsedByComposite: 0
  m_Offset: {{x: 0, y: 0}}
  serializedVersion: 2
  m_Radius: {r}
"""

MONO = """--- !u!114 &{id}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {guid}, type: 3}}
  m_Name:
  m_EditorClassIdentifier:
{fields}"""


def px_box(x0, y0, x1, y1):
    """Background pixels (1920x1080) -> world centre and size."""
    cx = ((x0 + x1) / 2 - 960) / 108.0
    cy = (540 - (y0 + y1) / 2) / 108.0
    return round(cx, 3), round(cy, 3), round((x1 - x0) / 108.0, 3), round((y1 - y0) / 108.0, 3)


def px_point(x, y):
    return round((x - 960) / 108.0, 3), round((540 - y) / 108.0, 3)
