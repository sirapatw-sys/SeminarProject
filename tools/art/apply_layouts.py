"""Rebuild the collision layout of every room around the players' FEET.

Before: the player's collider sat at the middle of the body, so the body
stopped a metre short of furniture seen from below and the feet sank into
furniture seen from above - "walking into invisible walls". Now the player
collides with a flat, rounded box at the feet, walls hug the painted floor,
obstacles are the floor footprint of each prop, and every trigger (objects
and NPCs) sits where the feet can reach it.

All boxes are background pixels (1920x1080)."""
import os
import re

from paths import ASSETS
from scenelib import Scene, px_box, px_point, fmt

FLOOR = (150, 300, 1770, 960)

# how far below the transform the feet are, in world units
# Drawn height of each character (CharacterVisualController.targetWorldHeight)
# and the distance from its transform down to its soles, both in world units.
# Everyone but Sena was shrunk by 15% in round 5 (was 2.13 / 2.2 / 2.25 / 2.1);
# the sprite scales about the transform, so the feet distance scales with it.
SHRINK = 0.85
HEIGHT = {"PlayerCharacter": 2.13 * SHRINK, "Alice": 2.2 * SHRINK,
          "Rina": 2.25 * SHRINK, "Stelle": 2.1 * SHRINK}
FEET = {"PlayerCharacter": 1.025 * SHRINK, "Alice": 0.86 * SHRINK, "Rina": 1.10 * SHRINK,
        "Stelle": 1.02 * SHRINK, "Sena": 1.42}
VISUAL_SCRIPT = "ab212121212121212121212121212121"   # CharacterVisualController

ROOMS = {
    "Room01": {
        "obstacles": {
            "Obstacle_BackShelves": (151, 296, 409, 336),
            "Obstacle_Sideboard": (1152, 296, 1448, 336),
            "Obstacle_Desk": (342, 440, 598, 606),
            "Obstacle_Drawer": (648, 430, 768, 598),
            "Obstacle_Sofa": (1244, 772, 1696, 858),
            "Obstacle_SideTable": (1366, 620, 1574, 698),
            "Obstacle_Plant": (1664, 350, 1770, 428),
            "Obstacle_PlantPot": (170, 862, 258, 940),
            "Obstacle_BookPile": (292, 872, 370, 936),
        },
        "triggers": {
            "Painting": (840, 290, 1080, 460),
            "Desk": (282, 400, 622, 680),
            "Drawer": (624, 360, 832, 670),
            "Door": (1480, 290, 1680, 460),
        },
        "drop": ["Wall_Top_Right", "Wall_Top_Alcove"],
        "feet": {"PlayerCharacter": (960, 868), "Alice": (1100, 860)},
    },
    "Room02": {
        "obstacles": {
            "Obstacle_BackShelves": (151, 296, 539, 336),
            "Obstacle_Clock": (604, 296, 676, 334),
            "Obstacle_BrazierLeft": (748, 296, 812, 352),
            "Obstacle_BrazierRight": (1108, 296, 1172, 352),
            "Obstacle_Cubbies": (1526, 296, 1754, 330),
            "Obstacle_WestBookcase": (160, 336, 418, 948),
            "Obstacle_ReadingDesk": (1360, 470, 1640, 688),
            "Obstacle_Bench": (1360, 810, 1640, 918),
            "Obstacle_StoneChest": (636, 862, 744, 918),
            "Obstacle_BooksSouth": (1156, 870, 1204, 930),
            "Obstacle_BooksEast": (1696, 456, 1744, 520),
        },
        "triggers": {
            "Bookshelf_A": (410, 336, 590, 552),
            "Bookshelf_B": (410, 552, 590, 750),
            "Bookshelf_C": (410, 750, 590, 960),
            "GrandfatherClock": (580, 290, 712, 470),
            "SacredBrazier": (712, 290, 828, 480),
            "CelestialGate": (840, 290, 1080, 470),
            "ClockworkWall": (1230, 290, 1510, 470),
            "ReadingDesk": (1300, 420, 1700, 760),
            "StoneChest": (592, 750, 830, 960),
        },
        "feet": {"PlayerCharacter": (960, 900), "Alice": (1090, 880), "Sena": (960, 380)},
    },
    "Room03": {
        "obstacles": {
            "Obstacle_Hearth": (240, 296, 570, 335),
            "Obstacle_Clock": (1325, 296, 1415, 322),
            "Obstacle_Vanity": (1584, 486, 1770, 686),
            "Obstacle_Sofa": (176, 712, 452, 878),
            "Obstacle_SideTable": (476, 796, 554, 866),
            "Obstacle_RockingChair": (602, 380, 688, 476),
            "Obstacle_Bookcase": (150, 380, 236, 598),
            "Obstacle_ToyChest": (1544, 826, 1696, 960),
        },
        "triggers": {
            "Mirror": (850, 290, 1070, 460),
            "Fireplace": (240, 290, 580, 470),
            "GirlPortrait": (1090, 290, 1300, 460),
            "StoppedClock": (1300, 290, 1450, 460),
            "Door": (1460, 290, 1690, 460),
            "MusicBox": (1500, 440, 1790, 740),
            "RockingChair": (560, 340, 730, 530),
            "CoveredSofa": (150, 670, 500, 930),
        },
        "feet": {"PlayerCharacter": (960, 900), "Alice": (830, 892), "Rina": (1380, 700), "Stelle": (585, 700)},
    },
}


def components_of(sc, go, cls):
    return [c for c in sc.components(go) if sc.get(c).startswith("--- !u!%d " % cls)]


def remove_component(sc, go, comp):
    d = sc.get(go)
    sc.set(go, d.replace("  - component: {fileID: %d}\n" % comp, ""))
    sc.docs = [x for x in sc.docs if sc.doc_id(x) != comp]


def set_pos(sc, name, x, y):
    go = sc.gameobject_by_name(name)
    sc.set_position(sc.transform_of(go), x, y)
    return go


def local_scale(sc, go):
    d = sc.get(sc.transform_of(go))
    m = re.search(r"m_LocalScale: \{x: ([-\d.e]+), y: ([-\d.e]+)", d)
    return float(m.group(1)), float(m.group(2))


def set_edge_radius(sc, col, r):
    d = sc.get(col)
    if "m_EdgeRadius:" in d:
        d = re.sub(r"m_EdgeRadius: [-\d.e]+", "m_EdgeRadius: %s" % fmt(r), d)
    sc.set(col, d)


def set_circle(sc, col, radius, oy):
    d = sc.get(col)
    d = re.sub(r"m_Radius: [-\d.e]+", "m_Radius: %s" % fmt(radius), d)
    d = re.sub(r"(?m)^  m_Offset: \{[^}]*\}", "  m_Offset: {x: 0, y: %s}" % fmt(oy), d)
    sc.set(col, d)


def apply(room, cfg):
    sc = Scene(os.path.join(ASSETS, "Scenes", room + ".unity"))
    env = sc.transform_of(sc.gameobject_by_name("Environment"))

    # walls: the feet stop on the painted floor's edges
    fx0, fy0, fx1, fy1 = FLOOR
    top = (540 - (fy0 + 10)) / 108.0
    bottom = (540 - (fy1 - 8)) / 108.0
    left, right = (fx0 - 960) / 108.0, (fx1 - 960) / 108.0
    for name, x, y, w, h in (("Wall_Top", 0, top + 1.5, 20, 3.0),
                             ("Wall_Bottom", 0, bottom - 1.0, 20, 2.0),
                             ("Wall_Left", left - 1.1, 0, 2.2, 12),
                             ("Wall_Right", right + 1.1, 0, 2.2, 12)):
        go = set_pos(sc, name, round(x, 3), round(y, 3))
        col = components_of(sc, go, 61)[0]
        sc.set_box(col, w, h, trigger=False, offset=(0, 0))
    for name in cfg.get("drop", []):
        if re.search(r"(?m)^  m_Name: %s$" % re.escape(name), sc.render()):
            sc.remove_gameobject(sc.gameobject_by_name(name))

    # obstacles
    for d in list(sc.docs):
        m = re.match(r"--- !u!1 &(\d+)", d)
        if m and re.search(r"(?m)^  m_Name: Obstacle_", d):
            sc.remove_gameobject(int(m.group(1)))
    for name, box in cfg["obstacles"].items():
        cx, cy, w, h = px_box(*box)
        go, _ = sc.new_gameobject(name, env, cx, cy)
        col = sc.add_box(go, round(w - 0.1, 3), round(h - 0.1, 3), trigger=False)
        set_edge_radius(sc, col, 0.05)       # rounded corners: slide, don't snag

    # interactable triggers
    for name, box in cfg["triggers"].items():
        go = sc.gameobject_by_name(name)
        cx, cy, w, h = px_box(*box)
        sc.set_position(sc.transform_of(go), cx, cy)
        boxes = components_of(sc, go, 61)
        trig = [c for c in boxes if "m_IsTrigger: 1" in sc.get(c)]
        for c in boxes:
            if c not in trig:
                remove_component(sc, go, c)  # stray solid boxes on props
        sx, sy = local_scale(sc, go)
        sc.set_box(trig[0], round(w / sx, 3), round(h / sy, 3), offset=(0, 0))

    # characters by where their feet stand
    for name, (px, py) in cfg["feet"].items():
        go = sc.gameobject_by_name(name)
        if name in HEIGHT:
            for c in components_of(sc, go, 114):
                d = sc.get(c)
                if VISUAL_SCRIPT in d:
                    sc.set(c, re.sub(r"targetWorldHeight: [-\d.e]+",
                                     "targetWorldHeight: %s" % fmt(round(HEIGHT[name], 3)), d))
        x, y = px_point(px, py)
        sc.set_position(sc.transform_of(go), x, round(y + FEET[name], 3))
        sx, sy = local_scale(sc, go)
        feet = -FEET[name] / sy
        if name == "PlayerCharacter":
            col = [c for c in components_of(sc, go, 61)][0]
            # 0.6 x 0.3 world at the soles, rounded
            sc.set_box(col, round(0.44 / sx, 3), round(0.14 / sy, 3), trigger=False,
                       offset=(0, round(feet + 0.15 / sy, 3)))
            set_edge_radius(sc, col, round(0.08 / sx, 3))
        elif name == "Sena":
            pass  # her trigger box already hangs at her feet
        else:
            for c in components_of(sc, go, 58):
                set_circle(sc, c, round(1.05 / sx, 3), round(feet + 0.25 / sy, 3))
            for c in components_of(sc, go, 61):
                if "m_IsTrigger: 0" in sc.get(c):
                    remove_component(sc, go, c)   # NPCs are walk-through
    sc.save()


if __name__ == "__main__":
    for room, cfg in ROOMS.items():
        apply(room, cfg)
