"""Room02 "หอสมุดเก่า": a dark old library around the star gate.
Pack sprites (Inside_C / Inside_D / Inside_E / RejectedAssets / A4 / A5)
plus a few bespoke pieces drawn as 1x pixel art so they scale the same."""
import math
import os
import random

from PIL import Image, ImageDraw

from compose_lib import Room, sprite, region, trim, cobweb, pixel_canvas
from paths import SP


def gate():
    """The celestial gate: a stone arch around two night-blue doors."""
    w, h = 66, 64
    im, d = pixel_canvas(w, h)
    stone, stone_dk, stone_hi = (86, 88, 100, 255), (54, 56, 66, 255), (120, 122, 136, 255)
    d.rectangle((0, 18, 9, h - 1), fill=stone)                 # pillars
    d.rectangle((w - 10, 18, w - 1, h - 1), fill=stone)
    d.pieslice((0, 0, w - 1, 44), 180, 360, fill=stone)        # arch
    d.rectangle((0, 22, w - 1, 24), fill=stone)
    d.pieslice((9, 9, w - 10, 40), 180, 360, fill=(14, 20, 44, 255))
    d.rectangle((10, 24, w - 11, h - 1), fill=(14, 20, 44, 255))
    for y in range(26, h - 2, 7):                               # pillar blocks
        d.line((0, y, 9, y), fill=stone_dk)
        d.line((w - 10, y, w - 1, y), fill=stone_dk)
    d.arc((0, 0, w - 1, 44), 180, 360, fill=stone_hi)
    mid = w // 2
    d.line((mid, 12, mid, h - 1), fill=(150, 210, 255, 255))   # the glowing seam
    d.line((mid - 1, 20, mid - 1, h - 1), fill=(60, 110, 180, 255))
    rnd = random.Random(5)
    stars = [(rnd.randint(12, w - 13), rnd.randint(16, h - 4)) for _ in range(22)]
    # two small constellations, one per door leaf
    left = sorted([s for s in stars if s[0] < mid - 2])[:4]
    right = sorted([s for s in stars if s[0] > mid + 2], key=lambda s: s[1])[:3]
    for group in (left, right):
        for a, b in zip(group, group[1:]):
            d.line(a + b, fill=(44, 72, 120, 255))
    for x, y in stars:
        d.point((x, y), fill=(210, 230, 255, 255))
    for y in range(28, h - 4, 6):                              # runes on the pillars
        d.point((4, y), fill=(120, 190, 255, 255))
        d.point((w - 5, y + 2), fill=(120, 190, 255, 255))
    d.ellipse((mid - 4, 5, mid + 4, 13), outline=(170, 210, 255, 255))   # keystone star
    d.point((mid, 9), fill=(230, 245, 255, 255))
    return im


def brazier():
    im, d = pixel_canvas(18, 26)
    d.rectangle((7, 12, 10, 23), fill=(92, 70, 46, 255))       # stem
    d.rectangle((4, 23, 13, 25), fill=(70, 52, 36, 255))       # foot
    d.pieslice((1, 5, 16, 17), 0, 180, fill=(120, 92, 52, 255))  # bowl
    d.line((1, 11, 16, 11), fill=(170, 130, 70, 255))
    for x, y0, c in ((5, 3, (255, 150, 40)), (8, 0, (255, 210, 90)), (11, 2, (255, 150, 40)),
                     (7, 5, (255, 240, 170)), (9, 4, (255, 200, 80))):
        d.line((x, y0, x, 10), fill=c + (255,))
    return im


def gear_panel():
    w, h = 52, 40
    im, d = pixel_canvas(w, h)
    d.rectangle((0, 0, w - 1, h - 1), fill=(58, 40, 30, 255), outline=(92, 66, 46, 255))
    brass, brass_dk = (176, 136, 64, 255), (110, 82, 38, 255)

    def gear(cx, cy, r, teeth):
        for k in range(teeth):
            a = 2 * math.pi * k / teeth
            d.rectangle((cx + math.cos(a) * r - 1, cy + math.sin(a) * r - 1,
                         cx + math.cos(a) * r + 1, cy + math.sin(a) * r + 1), fill=brass)
        d.ellipse((cx - r, cy - r, cx + r, cy + r), fill=brass, outline=brass_dk)
        d.ellipse((cx - r / 3, cy - r / 3, cx + r / 3, cy + r / 3), fill=(40, 28, 20, 255))

    gear(14, 15, 9, 10)
    gear(30, 24, 7, 8)
    gear(42, 12, 5, 7)
    gear(40, 31, 4, 6)
    d.rectangle((6, 34, 46, 37), fill=(40, 28, 20, 255))       # engraved plate
    return im


def stone_chest():
    im, d = pixel_canvas(28, 20)
    d.rectangle((0, 5, 27, 19), fill=(96, 96, 104, 255), outline=(60, 60, 68, 255))
    d.rectangle((0, 0, 27, 6), fill=(116, 116, 124, 255), outline=(60, 60, 68, 255))
    for x in range(4, 24, 3):                                   # tally scratches
        d.line((x, 9, x, 15), fill=(150, 150, 158, 255))
    d.line((3, 14, 23, 10), fill=(150, 150, 158, 255))
    return im


def armillary():
    im, d = pixel_canvas(14, 16)
    d.line((7, 11, 7, 15), fill=(120, 90, 44, 255))
    d.rectangle((4, 15, 10, 15), fill=(120, 90, 44, 255))
    d.ellipse((1, 0, 13, 12), outline=(196, 156, 74, 255))
    d.ellipse((4, 0, 10, 12), outline=(170, 130, 60, 255))
    d.line((1, 6, 13, 6), fill=(170, 130, 60, 255))
    d.point((7, 6), fill=(120, 170, 220, 255))
    return im


def small_painting(night):
    """The two identical landscapes beside the gears: day, and just before dawn."""
    im, d = pixel_canvas(18, 15)
    d.rectangle((0, 0, 17, 14), fill=(150, 112, 56, 255))
    sky = (40, 52, 96, 255) if night else (120, 160, 190, 255)
    d.rectangle((2, 2, 15, 12), fill=sky)
    d.polygon((2, 12, 6, 7, 10, 10, 15, 6, 15, 12), fill=(30, 40, 36, 255) if night else (70, 100, 60, 255))
    d.point((12, 4), fill=(230, 230, 200, 255) if night else (255, 220, 120, 255))
    return im


def candle():
    im, d = pixel_canvas(5, 12)
    d.rectangle((1, 5, 3, 11), fill=(222, 214, 196, 255))
    d.line((2, 1, 2, 4), fill=(255, 200, 90, 255))
    d.point((2, 0), fill=(255, 240, 180, 255))
    return im


def build():
    r = Room(seed=22)
    r.wall(region("A4", (96, 383 + 32, 192, 479)))                 # dark wood panelling
    stone = [region("A5", (192, 0, 240, 48)), region("A5", (240, 0, 288, 48))]
    r.floor(stone, darken=0.55)
    r.frame()

    # ---- back wall
    for cx, idx in ((215, 112), (345, 110), (475, 109)):
        r.place(sprite("C", idx), cx, 336, name="back_shelf_%d" % cx)
    r.place(sprite("RJ", 3), 640, 334, name="clock")
    g = r.place(gate(), 960, 304, name="gate", shadow=False)
    r.place(brazier(), 780, 352, name="brazier_left")
    r.place(brazier(), 1140, 352, name="brazier_right")
    r.place(gear_panel(), 1380, 250, name="gears", shadow=False)
    r.place(small_painting(False), 1224, 150, k=4, shadow=False)
    r.place(small_painting(True), 1224, 236, k=4, shadow=False)
    r.place(sprite("D", 30), 1640, 330, k=3, name="cubbies")

    # ---- the big bookcase on the west side: upper, middle, lowest sections
    for tag, bottom in (("shelf_upper", 556), ("shelf_middle", 752), ("shelf_lowest", 948)):
        left = r.place(sprite("C", 109), 226, bottom)
        right = r.place(sprite("C", 110), 354, bottom)
        r.layout[tag] = (left[0], left[1], right[2], right[3])

    # ---- floor
    r.place(sprite("E", 1), 980, 820, k=3, name="rug", shadow=False)
    r.place(sprite("C", 106), 1500, 590, name="desk_chair", shadow=False)
    desk = r.place(sprite("C", 115), 1500, 690, name="reading_desk")
    r.place(sprite("C", 24), 1460, 596, k=4, shadow=False)          # the open ledger
    r.place(armillary(), 1596, 604, shadow=False)
    r.place(candle(), 1548, 600, shadow=False)
    r.place(sprite("D", 23), 1500, 920, name="bench")
    r.place(stone_chest(), 690, 920, name="stone_chest")
    for cx, bottom in ((1180, 930), (1720, 520)):
        r.place(sprite("C", 88), cx, bottom, k=2, shadow=False)     # book towers
    for c in ((150, 300, 0), (1770, 300, 90), (150, 960, 270)):
        cobweb(r.img, *c, size=100, alpha=50)

    # ---- light: the gate, two braziers, one candle at the ledger
    gx = (g[0] + g[2]) / 2
    r.glow(gx, 200, 260, (0.35, 0.6, 1.0), 0.6)
    r.glow(gx, 280, 90, (0.6, 0.85, 1.0), 0.5)
    for key in ("brazier_left", "brazier_right"):
        b = r.layout[key]
        r.glow((b[0] + b[2]) / 2, b[1] + 10, 230, (1.0, 0.58, 0.22), 0.85)
        r.glow((b[0] + b[2]) / 2, b[1] + 8, 40, (1.0, 0.8, 0.45), 0.7)
    r.glow(1560, 600, 150, (1.0, 0.72, 0.4), 0.45)
    out = os.path.join(SP, "Room02_Library_Dark.png")
    r.finish(out, ambient=0.42, grade=(0.82, 0.86, 1.02), desat=0.25)
    print("saved", out)


if __name__ == "__main__":
    build()
