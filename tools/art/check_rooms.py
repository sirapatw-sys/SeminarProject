"""Overlay each room's colliders on its background and flood-fill the
player's feet box from the spawn point."""
import os
import re
import sys
from collections import deque

import numpy as np
from PIL import Image, ImageDraw

from paths import ASSETS, SP

BG = {"Room01": "Room01_Study_Dark.png", "Room02": "Room02_Library_Dark.png", "Room03": "Room03_HauntedParlor.png"}
FEET_W, FEET_H = 0.6, 0.3


def parse(room):
    text = open(os.path.join(ASSETS, "Scenes", room + ".unity"), encoding="utf-8").read()
    by = {}
    for d in re.split(r"(?m)^(?=--- !u!)", text):
        m = re.match(r"--- !u!(\d+) &(\d+)", d)
        if m:
            by[int(m.group(2))] = (int(m.group(1)), d)
    names, pos, scale = {}, {}, {}
    for fid, (cls, d) in by.items():
        if cls == 1:
            names[fid] = re.search(r"m_Name: (.*)", d).group(1)
        if cls == 4:
            go = int(re.search(r"m_GameObject: \{fileID: (\d+)\}", d).group(1))
            p = re.search(r"m_LocalPosition: \{x: ([-\d.e]+), y: ([-\d.e]+)", d)
            s = re.search(r"m_LocalScale: \{x: ([-\d.e]+), y: ([-\d.e]+)", d)
            pos[go] = (float(p.group(1)), float(p.group(2)))
            scale[go] = (float(s.group(1)), float(s.group(2)))
    cols = []
    for fid, (cls, d) in by.items():
        if cls not in (61, 58):
            continue
        go = int(re.search(r"m_GameObject: \{fileID: (\d+)\}", d).group(1))
        name = names.get(go, "?")
        if name in ("Canvas", "Floor"):
            continue
        x, y = pos[go]
        sx, sy = scale[go]
        off = re.search(r"m_Offset: \{x: ([-\d.e]+), y: ([-\d.e]+)", d)
        ox, oy = float(off.group(1)) * sx, float(off.group(2)) * sy
        trig = re.search(r"m_IsTrigger: (\d)", d).group(1) == "1"
        if cls == 61:
            sz = re.search(r"m_Size: \{x: ([-\d.e]+), y: ([-\d.e]+)", d)
            er = re.search(r"m_EdgeRadius: ([-\d.e]+)", d)
            e = float(er.group(1)) if er else 0.0
            w, h = (float(sz.group(1)) + 2 * e) * sx, (float(sz.group(2)) + 2 * e) * sy
            cols.append(("box", name, trig, x + ox, y + oy, w, h))
        else:
            r = float(re.search(r"m_Radius: ([-\d.e]+)", d).group(1)) * sx
            cols.append(("circle", name, trig, x + ox, y + oy, r, r))
    return cols, pos, names


def w2p(x, y):
    return 960 + x * 108, 540 - y * 108


def check(room):
    cols, pos, names = parse(room)
    img = Image.open(os.path.join(ASSETS, "Art", "Backgrounds", BG[room])).convert("RGBA")
    ov = Image.new("RGBA", img.size)
    d = ImageDraw.Draw(ov)
    solids = []
    player = None
    for kind, name, trig, x, y, w, h in cols:
        if kind == "box":
            x0, y0 = w2p(x - w / 2, y + h / 2)
            x1, y1 = w2p(x + w / 2, y - h / 2)
            c = (60, 200, 255, 60) if trig else (255, 60, 60, 110)
            if name == "PlayerCharacter":
                c = (255, 255, 0, 200)
                player = (x, y)
            elif not trig:
                solids.append((x - w / 2, y - h / 2, x + w / 2, y + h / 2))
            d.rectangle((x0, y0, x1, y1), fill=c, outline=c[:3] + (255,), width=2)
            d.text((x0 + 3, y0 + 3), name, fill=(255, 255, 255, 255))
        else:
            cx, cy = w2p(x, y)
            d.ellipse((cx - w * 108, cy - w * 108, cx + w * 108, cy + w * 108), outline=(120, 255, 120, 255), width=2)
            d.text((cx - 10, cy), name, fill=(255, 255, 255, 255))
    out = Image.alpha_composite(img, ov)

    step = 0.1
    xs = np.arange(-8.8, 8.8, step)
    ys = np.arange(-5.0, 5.0, step)

    def free(x, y):
        for a, b, c2, e in solids:
            if x + FEET_W / 2 > a and x - FEET_W / 2 < c2 and y + FEET_H / 2 > b and y - FEET_H / 2 < e:
                return False
        return True

    grid = np.array([[free(x, y) for x in xs] for y in ys])
    i0 = int(round((player[0] - xs[0]) / step))
    j0 = int(round((player[1] - ys[0]) / step))
    assert grid[j0, i0], "spawn is inside a collider"
    seen = np.zeros_like(grid)
    q = deque([(j0, i0)])
    seen[j0, i0] = True
    while q:
        j, i = q.popleft()
        for dj, di in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            nj, ni = j + dj, i + di
            if 0 <= nj < grid.shape[0] and 0 <= ni < grid.shape[1] and grid[nj, ni] and not seen[nj, ni]:
                seen[nj, ni] = True
                q.append((nj, ni))
    dd = ImageDraw.Draw(out)
    for j, i in zip(*np.nonzero(seen[::3, ::3])):
        px, py = w2p(xs[i * 3], ys[j * 3])
        dd.point((px, py), fill=(255, 255, 0, 200))
    print(room, "free", int(grid.sum()), "reached", int(seen.sum()))
    problems = 0
    for kind, name, trig, x, y, w, h in cols:
        if not trig or name in ("PlayerCharacter",):
            continue
        ok = False
        for j, i in zip(*np.nonzero(seen)):
            if kind == "box":
                hit = abs(xs[i] - x) < w / 2 + FEET_W / 2 and abs(ys[j] - y) < h / 2 + FEET_H / 2
            else:
                hit = (xs[i] - x) ** 2 + (ys[j] - y) ** 2 < (w + 0.2) ** 2
            if hit:
                ok = True
                break
        if not ok:
            problems += 1
        print("  ", "ok " if ok else "UNREACHABLE", name)
    out.convert("RGB").save(os.path.join(SP, "colliders_%s.png" % room))
    return problems


if __name__ == "__main__":
    total = sum(check(r) for r in (sys.argv[1:] or ["Room01", "Room02", "Room03"]))
    print("unreachable triggers:", total)
