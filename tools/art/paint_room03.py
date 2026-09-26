"""Requires: pip install pillow numpy

Paint Room03 "The Hollow Parlor": a haunted top-down parlour matching the
dark, candle-lit look of Room01. Everything is drawn at 2x and downsampled.

Layout (final 1920x1080 pixels) is exported to room03_layout.json so the
scene colliders can be placed from the same numbers.
"""
import json
import math
import os
import random

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageChops

# Writes Room03_HauntedParlor.png and room03_layout.json next to this script;
# copy the PNG over Assets/Art/Backgrounds/Room03_HauntedParlor.png (keep its .meta).
SP = os.path.dirname(os.path.abspath(__file__))

# ROOM03_CALM=1 renders the room after the lullaby: the same picture with the
# candle flames burning an ordinary warm orange instead of ghost blue.
CALM = os.environ.get("ROOM03_CALM") == "1"
FLAME_FILL = (255, 190, 110, 255) if CALM else (150, 225, 255, 255)
FLAME_GLOW = (1.0, 0.6, 0.25) if CALM else (0.3, 0.7, 1.0)
FLAME_CORE = (1.0, 0.85, 0.55) if CALM else (0.7, 0.95, 1.0)
OUT_NAME = "Room03_HauntedParlor_Calm.png" if CALM else "Room03_HauntedParlor.png"

W, H = 1920, 1080
S = 2  # supersample
random.seed(3)
rng = np.random.default_rng(3)

# ----------------------------------------------------------------- layout (final px)
L = {
    "wall_top": (120, 40, 1800, 300),      # back wall face
    "floor": (150, 300, 1770, 960),
    "fireplace": (250, 70, 560, 330),      # includes hearth lip on floor
    "window": (640, 70, 790, 262),
    "mirror": (870, 62, 1050, 282),
    "portrait": (1130, 88, 1270, 238),
    "clock": (1330, 44, 1410, 318),
    "door": (1480, 66, 1660, 300),
    "dresser": (1570, 470, 1762, 690),     # vanity against right wall
    "music_box": (1618, 520, 1690, 578),
    "sofa": (175, 700, 455, 880),          # sheet-covered sofa
    "side_table": (470, 790, 560, 870),
    "rocking_chair": (590, 360, 700, 480),
    "rug": (700, 470, 1270, 830),
    "candelabra_a": (1110, 330, 1170, 390),
    "candelabra_b": (310, 560, 370, 620),
    "doll": (1300, 830, 1350, 880),
    "bookcase": (158, 372, 236, 600),     # narrow case against the left wall
    "toy_chest": (1540, 820, 1700, 930),
    "blocks": (1180, 860, 1260, 910),
}
json.dump(L, open(os.path.join(SP, "room03_layout.json"), "w"), indent=1)


def s(box):
    return tuple(int(v * S) for v in box)


def rgba(hexstr, a=255):
    hexstr = hexstr.lstrip("#")
    return tuple(int(hexstr[i:i + 2], 16) for i in (0, 2, 4)) + (a,)


def layer():
    return Image.new("RGBA", (W * S, H * S), (0, 0, 0, 0))


def noise_img(w, h, scale, octaves=3, seed=0):
    r = np.random.default_rng(seed)
    acc = np.zeros((h, w))
    amp, total = 1.0, 0.0
    for o in range(octaves):
        sw, sh = max(2, int(w / scale) + 2), max(2, int(h / scale) + 2)
        base = r.random((sh, sw))
        img = Image.fromarray((base * 255).astype(np.uint8)).resize((w, h), Image.BICUBIC)
        acc += np.asarray(img) / 255.0 * amp
        total += amp
        amp *= 0.5
        scale /= 2.0
    return acc / total


canvas = Image.new("RGBA", (W * S, H * S), rgba("#07090f"))
d = ImageDraw.Draw(canvas)

# ----------------------------------------------------------------- floor planks
fx0, fy0, fx1, fy1 = s(L["floor"])
floor = Image.new("RGBA", (fx1 - fx0, fy1 - fy0), rgba("#2a1d17"))
fd = ImageDraw.Draw(floor)
plank_h = 44 * S
y = 0
row = 0
while y < floor.height:
    x = -random.randint(0, 300) * S
    while x < floor.width:
        length = random.randint(220, 420) * S
        tone = random.randint(-12, 10)
        base = (52 + tone, 37 + tone, 30 + tone, 255)
        fd.rectangle([x, y, x + length, y + plank_h], fill=base)
        fd.line([x, y, x, y + plank_h], fill=(18, 12, 10, 255), width=2 * S)
        x += length
    fd.line([0, y, floor.width, y], fill=(16, 11, 9, 255), width=3 * S)
    y += plank_h
    row += 1
grain = noise_img(floor.width, floor.height, 60, 4, seed=1)
streak = noise_img(floor.width // 12 + 1, floor.height, 6, 2, seed=2)
streak = np.asarray(Image.fromarray((streak * 255).astype(np.uint8)).resize((floor.width, floor.height), Image.BICUBIC)) / 255.0
fa = np.asarray(floor).astype(np.float64)
shade = 0.72 + 0.35 * grain + 0.18 * (streak - 0.5)
fa[..., :3] *= shade[..., None]
floor = Image.fromarray(np.clip(fa, 0, 255).astype(np.uint8), "RGBA")
canvas.alpha_composite(floor, (fx0, fy0))

# ----------------------------------------------------------------- back wall
wx0, wy0, wx1, wy1 = s(L["wall_top"])
wall = Image.new("RGBA", (wx1 - wx0, wy1 - wy0), rgba("#16262a"))
wd = ImageDraw.Draw(wall)
# damask wallpaper motif
tile = 64 * S
for ty in range(0, wall.height, tile):
    for tx in range(0, wall.width, tile):
        ox = tx + (tile // 2 if (ty // tile) % 2 else 0)
        cx, cy = ox + tile // 2, ty + tile // 2
        col = (34, 56, 58, 255)
        wd.ellipse([cx - 9 * S, cy - 16 * S, cx + 9 * S, cy + 16 * S], outline=col, width=2 * S)
        wd.ellipse([cx - 3 * S, cy - 3 * S, cx + 3 * S, cy + 3 * S], fill=col)
        wd.arc([cx - 18 * S, cy - 12 * S, cx, cy + 12 * S], 200, 340, fill=col, width=2 * S)
        wd.arc([cx, cy - 12 * S, cx + 18 * S, cy + 12 * S], 200, 340, fill=col, width=2 * S)
# wainscot
wain_top = int((230 - 40) * S)
wd.rectangle([0, wain_top, wall.width, wall.height], fill=(38, 26, 22, 255))
for px in range(0, wall.width, 120 * S):
    wd.rectangle([px + 10 * S, wain_top + 12 * S, px + 110 * S, wall.height - 10 * S],
                 outline=(24, 16, 13, 255), width=3 * S)
wd.line([0, wain_top, wall.width, wain_top], fill=(70, 50, 38, 255), width=5 * S)
wa = np.asarray(wall).astype(np.float64)
stain = noise_img(wall.width, wall.height, 180, 4, seed=5)
drip = noise_img(wall.width // 20 + 1, wall.height, 3, 2, seed=6)
drip = np.asarray(Image.fromarray((drip * 255).astype(np.uint8)).resize((wall.width, wall.height), Image.BICUBIC)) / 255.0
wa[..., :3] *= (0.7 + 0.45 * stain - 0.25 * np.clip(drip - 0.55, 0, 1) * 3)[..., None]
wall = Image.fromarray(np.clip(wa, 0, 255).astype(np.uint8), "RGBA")
canvas.alpha_composite(wall, (wx0, wy0))
# peeling wallpaper patches
for _ in range(7):
    px = random.randint(wx0 + 40 * S, wx1 - 80 * S)
    py = random.randint(wy0 + 20 * S, wy0 + 150 * S)
    pts = [(px + random.randint(-30, 30) * S, py + random.randint(-20, 20) * S) for _ in range(6)]
    d.polygon(pts, fill=(58, 48, 38, 200))

# side walls (thin top-down strips) and beams
for x0, x1 in ((100, 150), (1770, 1820)):
    d.rectangle(s((x0, 300, x1, 960)), fill=(20, 32, 36, 255))
beam = (44, 30, 24, 255)
beam_hi = (74, 52, 40, 255)
d.rectangle(s((96, 20, 1824, 44)), fill=beam)
d.line(s((96, 44, 1824, 44)), fill=beam_hi, width=2 * S)
d.rectangle(s((96, 958, 1824, 1000)), fill=beam)
d.line(s((96, 958, 1824, 958)), fill=beam_hi, width=2 * S)
d.rectangle(s((96, 20, 124, 1000)), fill=beam)
d.rectangle(s((1796, 20, 1824, 1000)), fill=beam)
d.rectangle(s((146, 296, 154, 960)), fill=(30, 21, 17, 255))
d.rectangle(s((1766, 296, 1774, 960)), fill=(30, 21, 17, 255))
for cx, cy in ((110, 32), (1810, 32), (110, 980), (1810, 980), (560, 980), (1360, 980)):
    d.rectangle(s((cx - 26, cy - 24, cx + 26, cy + 24)), fill=(36, 25, 20, 255), outline=(78, 56, 42, 255), width=2 * S)
# wall/floor contact shadow
shadow = layer()
sd = ImageDraw.Draw(shadow)
sd.rectangle(s((150, 300, 1770, 330)), fill=(0, 0, 0, 150))
sd.rectangle(s((150, 300, 180, 960)), fill=(0, 0, 0, 110))
sd.rectangle(s((1740, 300, 1770, 960)), fill=(0, 0, 0, 110))
canvas.alpha_composite(shadow.filter(ImageFilter.GaussianBlur(14 * S)))


def drop_shadow(box, radius=10, alpha=150, offset=(6, 10)):
    sh = layer()
    ImageDraw.Draw(sh).rectangle(s((box[0] + offset[0], box[1] + offset[1], box[2] + offset[0], box[3] + offset[1])), fill=(0, 0, 0, alpha))
    canvas.alpha_composite(sh.filter(ImageFilter.GaussianBlur(radius * S)))


# ----------------------------------------------------------------- rug
rx0, ry0, rx1, ry1 = L["rug"]
drop_shadow(L["rug"], 6, 90, (0, 4))
rug = layer()
rd = ImageDraw.Draw(rug)
rd.rounded_rectangle(s(L["rug"]), radius=18 * S, fill=(70, 24, 30, 255))
rd.rounded_rectangle(s((rx0 + 18, ry0 + 18, rx1 - 18, ry1 - 18)), radius=12 * S, outline=(150, 112, 70, 255), width=4 * S)
rd.rounded_rectangle(s((rx0 + 34, ry0 + 34, rx1 - 34, ry1 - 34)), radius=10 * S, outline=(40, 12, 18, 255), width=6 * S)
cx, cy = (rx0 + rx1) / 2, (ry0 + ry1) / 2
for r_, col in ((120, (110, 44, 44, 255)), (86, (46, 16, 22, 255)), (50, (140, 104, 64, 255)), (22, (60, 18, 24, 255))):
    rd.ellipse(s((cx - r_ * 1.5, cy - r_, cx + r_ * 1.5, cy + r_)), outline=col, width=5 * S)
for k in range(8):
    a = k * math.pi / 4
    px, py = cx + math.cos(a) * 200, cy + math.sin(a) * 130
    rd.ellipse(s((px - 12, py - 12, px + 12, py + 12)), fill=(140, 104, 64, 220))
ra = np.asarray(rug).astype(np.float64)
wear = noise_img(W * S, H * S, 140, 4, seed=9)
ra[..., :3] *= (0.55 + 0.6 * wear)[..., None]
# a torn, dusty corner
ra[..., 3] *= np.where(((np.mgrid[0:H * S, 0:W * S][1] - rx1 * S) ** 2 + (np.mgrid[0:H * S, 0:W * S][0] - ry1 * S) ** 2) < (60 * S) ** 2, 0.25, 1.0)
canvas.alpha_composite(Image.fromarray(np.clip(ra, 0, 255).astype(np.uint8), "RGBA"))

# ----------------------------------------------------------------- fireplace
x0, y0, x1, y1 = L["fireplace"]
drop_shadow((x0, y0, x1, y1), 12, 170)
d.rectangle(s((x0, y0 + 20, x1, y1 - 30)), fill=(58, 56, 60, 255))                    # stone surround
for by in range(y0 + 26, y1 - 30, 26):
    off = 0 if ((by - y0) // 26) % 2 else 22
    for bx in range(x0 + off, x1, 44):
        d.rectangle(s((bx, by, min(bx + 42, x1), by + 24)), outline=(36, 34, 38, 255), width=2 * S)
d.rectangle(s((x0 - 14, y0 + 6, x1 + 14, y0 + 30)), fill=(66, 44, 34, 255), outline=(96, 68, 50, 255), width=2 * S)  # mantel
ox0, oy0, ox1, oy1 = x0 + 70, y0 + 70, x1 - 70, y1 - 30
d.rectangle(s((ox0, oy0, ox1, oy1)), fill=(10, 9, 10, 255))                            # opening
d.pieslice(s((ox0, oy0 - 50, ox1, oy0 + 60)), 180, 360, fill=(10, 9, 10, 255))
d.rectangle(s((x0 - 10, y1 - 34, x1 + 10, y1)), fill=(46, 44, 48, 255), outline=(70, 68, 72, 255), width=2 * S)  # hearth lip
ash = layer()
ad = ImageDraw.Draw(ash)
for _ in range(90):
    ax = random.uniform(ox0 + 10, ox1 - 10)
    ay = random.uniform(oy1 - 55, oy1 - 4)
    rr = random.uniform(4, 11)
    g_ = random.randint(60, 95)
    ad.ellipse(s((ax - rr, ay - rr * 0.6, ax + rr, ay + rr * 0.6)), fill=(g_, g_, g_ + 4, 230))
ad.line(s((ox0 + 40, oy1 - 30, ox0 + 120, oy1 - 42)), fill=(30, 22, 18, 255), width=9 * S)   # charred logs
ad.line(s((ox0 + 70, oy1 - 18, ox1 - 30, oy1 - 32)), fill=(26, 20, 16, 255), width=8 * S)
canvas.alpha_composite(ash)
# the tiny brass glint of the winding key half-buried in the ash
gx, gy = ox0 + 118, oy1 - 16
glint = layer()
gd = ImageDraw.Draw(glint)
gd.ellipse(s((gx - 5, gy - 3, gx + 5, gy + 3)), fill=(214, 170, 80, 255))
gd.line(s((gx - 10, gy, gx + 10, gy)), fill=(255, 230, 160, 180), width=1 * S)
gd.line(s((gx, gy - 7, gx, gy + 7)), fill=(255, 230, 160, 150), width=1 * S)
canvas.alpha_composite(glint)
L["key_glint"] = (gx, gy)

# ----------------------------------------------------------------- window + moonlight
x0, y0, x1, y1 = L["window"]
d.rectangle(s((x0 - 10, y0 - 8, x1 + 10, y1 + 10)), fill=(52, 38, 30, 255))
d.rectangle(s((x0, y0, x1, y1)), fill=(22, 40, 66, 255))
glass = layer()
gld = ImageDraw.Draw(glass)
gld.ellipse(s((x0 + 70, y0 + 20, x0 + 118, y0 + 68)), fill=(210, 225, 240, 230))        # moon
canvas.alpha_composite(glass.filter(ImageFilter.GaussianBlur(3 * S)))
d.line(s(((x0 + x1) / 2, y0, (x0 + x1) / 2, y1)), fill=(40, 30, 24, 255), width=6 * S)
d.line(s((x0, (y0 + y1) / 2, x1, (y0 + y1) / 2)), fill=(40, 30, 24, 255), width=6 * S)
d.line(s((x0 + 20, y0 + 120, x0 + 60, y0 + 180)), fill=(120, 150, 180, 255), width=2 * S)  # crack
d.line(s((x0 + 60, y0 + 180, x0 + 30, y1)), fill=(120, 150, 180, 255), width=2 * S)
for side in (0, 1):                                                                     # torn curtains
    cxs = x0 - 34 if side == 0 else x1 + 4
    pts = [(cxs, y0 - 14), (cxs + 30, y0 - 14), (cxs + 34 - side * 10, y1 + 30), (cxs + 10, y1 + 16), (cxs - 4, y1 + 40)]
    d.polygon(s(sum(pts, ())), fill=(62, 18, 26, 255))

# ----------------------------------------------------------------- mirror
x0, y0, x1, y1 = L["mirror"]
drop_shadow(L["mirror"], 8, 150, (4, 8))
d.rounded_rectangle(s((x0, y0, x1, y1)), radius=40 * S, fill=(140, 104, 50, 255))
d.rounded_rectangle(s((x0 + 6, y0 + 6, x1 - 6, y1 - 6)), radius=36 * S, outline=(205, 162, 86, 255), width=3 * S)
gx0, gy0, gx1, gy1 = x0 + 18, y0 + 18, x1 - 18, y1 - 18
mirror_glass = layer()
md = ImageDraw.Draw(mirror_glass)
md.rounded_rectangle(s((gx0, gy0, gx1, gy1)), radius=28 * S, fill=(40, 58, 70, 255))
canvas.alpha_composite(mirror_glass)
# a pale child's silhouette that is not standing in the room
ghost = layer()
gh = ImageDraw.Draw(ghost)
mx = (gx0 + gx1) / 2 + 22
gh.ellipse(s((mx - 18, gy0 + 60, mx + 18, gy0 + 98)), fill=(200, 225, 235, 70))
gh.polygon(s((mx - 26, gy1 - 4, mx - 14, gy0 + 96, mx + 14, gy0 + 96, mx + 26, gy1 - 4)), fill=(200, 225, 235, 55))
gh.line(s((mx - 16, gy0 + 76, mx - 30, gy0 + 120)), fill=(200, 225, 235, 60), width=5 * S)   # braids
gh.line(s((mx + 16, gy0 + 76, mx + 30, gy0 + 120)), fill=(200, 225, 235, 60), width=5 * S)
canvas.alpha_composite(ghost.filter(ImageFilter.GaussianBlur(4 * S)))
# spider-web cracks from an impact point
cr = layer()
cd_ = ImageDraw.Draw(cr)
ix, iy = gx0 + 50, gy0 + 70
for k in range(11):
    a = k * 2 * math.pi / 11 + random.uniform(-0.2, 0.2)
    ln = random.uniform(40, 120)
    px, py = ix, iy
    for seg in range(4):
        nx = px + math.cos(a + random.uniform(-0.3, 0.3)) * ln / 4
        ny = py + math.sin(a + random.uniform(-0.3, 0.3)) * ln / 4
        cd_.line(s((px, py, nx, ny)), fill=(210, 230, 240, 170), width=1 * S)
        px, py = nx, ny
for rr in (12, 26, 44):
    cd_.ellipse(s((ix - rr, iy - rr, ix + rr, iy + rr)), outline=(210, 230, 240, 90), width=1 * S)
mask = layer()
ImageDraw.Draw(mask).rounded_rectangle(s((gx0, gy0, gx1, gy1)), radius=28 * S, fill=(255, 255, 255, 255))
cr.putalpha(ImageChops.multiply(cr.split()[3], mask.split()[3]))
canvas.alpha_composite(cr)
# small misted handprint
hp = layer()
hd = ImageDraw.Draw(hp)
hx, hy = gx1 - 44, gy1 - 60
hd.ellipse(s((hx - 10, hy - 8, hx + 10, hy + 12)), fill=(220, 235, 240, 90))
for k in range(5):
    a = math.radians(-150 + k * 30)
    hd.line(s((hx + math.cos(a) * 8, hy + math.sin(a) * 8, hx + math.cos(a) * 22, hy + math.sin(a) * 22)), fill=(220, 235, 240, 90), width=4 * S)
canvas.alpha_composite(hp.filter(ImageFilter.GaussianBlur(1.5 * S)))

# ----------------------------------------------------------------- girl portrait
x0, y0, x1, y1 = L["portrait"]
drop_shadow(L["portrait"], 6, 150, (4, 6))
d.rectangle(s((x0, y0, x1, y1)), fill=(150, 112, 56, 255))
d.rectangle(s((x0 + 10, y0 + 10, x1 - 10, y1 - 10)), fill=(46, 40, 52, 255))
pcx = (x0 + x1) / 2
d.polygon(s((pcx - 34, y1 - 10, pcx - 18, y0 + 78, pcx + 18, y0 + 78, pcx + 34, y1 - 10)), fill=(196, 196, 210, 255))  # nightgown
d.ellipse(s((pcx - 20, y0 + 34, pcx + 20, y0 + 80)), fill=(222, 206, 196, 255))       # face
d.pieslice(s((pcx - 24, y0 + 28, pcx + 24, y0 + 74)), 180, 360, fill=(92, 62, 40, 255))  # hair
d.line(s((pcx - 20, y0 + 56, pcx - 28, y0 + 104)), fill=(92, 62, 40, 255), width=6 * S)
d.line(s((pcx + 20, y0 + 56, pcx + 28, y0 + 104)), fill=(92, 62, 40, 255), width=6 * S)
d.ellipse(s((pcx - 11, y0 + 52, pcx - 4, y0 + 60)), fill=(20, 16, 20, 255))            # hollow eyes
d.ellipse(s((pcx + 4, y0 + 52, pcx + 11, y0 + 60)), fill=(20, 16, 20, 255))
d.rectangle(s((pcx - 16, y0 + 100, pcx + 16, y0 + 122)), fill=(120, 80, 60, 255))    # music box in her arms
d.rectangle(s((pcx - 16, y0 + 100, pcx + 16, y0 + 104)), fill=(180, 140, 80, 255))
d.rectangle(s((pcx - 26, y1 + 4, pcx + 26, y1 + 14)), fill=(160, 124, 60, 255))       # name plate
# a slow drip from one eye
d.line(s((pcx + 7, y0 + 60, pcx + 8, y0 + 84)), fill=(120, 20, 26, 220), width=2 * S)

# ----------------------------------------------------------------- grandfather clock
x0, y0, x1, y1 = L["clock"]
drop_shadow(L["clock"], 8, 160)
d.rectangle(s((x0, y0 + 20, x1, y1)), fill=(58, 36, 26, 255), outline=(90, 60, 42, 255), width=2 * S)
d.pieslice(s((x0, y0, x1, y0 + 50)), 180, 360, fill=(58, 36, 26, 255))
d.ellipse(s((x0 + 10, y0 + 22, x1 - 10, y0 + 82)), fill=(210, 200, 170, 255), outline=(160, 120, 60, 255), width=3 * S)
ccx, ccy = (x0 + x1) / 2, y0 + 52
d.line(s((ccx, ccy, ccx, ccy - 22)), fill=(20, 20, 20, 255), width=2 * S)            # 3 o'clock
d.line(s((ccx, ccy, ccx + 16, ccy)), fill=(20, 20, 20, 255), width=3 * S)
d.rectangle(s((x0 + 16, y0 + 100, x1 - 16, y1 - 30)), fill=(24, 16, 12, 255))
d.line(s((ccx, y0 + 104, ccx - 6, y1 - 70)), fill=(180, 140, 70, 255), width=2 * S)
d.ellipse(s((ccx - 16, y1 - 82, ccx + 4, y1 - 62)), fill=(190, 150, 70, 255))

# ----------------------------------------------------------------- exit door
x0, y0, x1, y1 = L["door"]
drop_shadow(L["door"], 8, 170)
d.rectangle(s((x0 - 12, y0 - 10, x1 + 12, y1)), fill=(40, 28, 22, 255))
d.rectangle(s((x0, y0, x1, y1)), fill=(64, 42, 30, 255))
for py0, py1 in ((y0 + 16, y0 + 104), (y0 + 124, y1 - 16)):
    d.rectangle(s((x0 + 16, py0, (x0 + x1) / 2 - 8, py1)), outline=(40, 26, 18, 255), width=3 * S)
    d.rectangle(s(((x0 + x1) / 2 + 8, py0, x1 - 16, py1)), outline=(40, 26, 18, 255), width=3 * S)
d.ellipse(s((x1 - 34, (y0 + y1) / 2 - 8, x1 - 18, (y0 + y1) / 2 + 8)), fill=(150, 150, 160, 255))
frost = layer()
fr = ImageDraw.Draw(frost)
for _ in range(9):
    hx = random.uniform(x0 + 20, x1 - 20)
    hy = random.uniform(y0 + 20, y1 - 30)
    sc = random.uniform(0.6, 1.0)
    fr.ellipse(s((hx - 8 * sc, hy - 6 * sc, hx + 8 * sc, hy + 10 * sc)), fill=(215, 235, 245, 110))
    for k in range(5):
        a = math.radians(-150 + k * 30)
        fr.line(s((hx + math.cos(a) * 6 * sc, hy + math.sin(a) * 6 * sc, hx + math.cos(a) * 17 * sc, hy + math.sin(a) * 17 * sc)), fill=(215, 235, 245, 110), width=3 * S)
canvas.alpha_composite(frost.filter(ImageFilter.GaussianBlur(1.2 * S)))
d.rectangle(s((x0 - 10, y1 - 4, x1 + 10, y1 + 14)), fill=(34, 24, 20, 255))            # threshold

# ----------------------------------------------------------------- vanity + music box
x0, y0, x1, y1 = L["dresser"]
drop_shadow(L["dresser"], 10, 170, (-8, 10))
d.rectangle(s((x0, y0, x1, y1)), fill=(70, 46, 34, 255), outline=(104, 72, 52, 255), width=3 * S)
d.rectangle(s((x0 + 12, y0 + 12, x1 - 12, y1 - 12)), fill=(84, 56, 40, 255))
d.rectangle(s((x1 - 34, y0 + 16, x1 - 8, y1 - 16)), fill=(56, 70, 80, 255), outline=(150, 112, 60, 255), width=2 * S)  # tilted mirror edge
for dy in (y0 + 150, y0 + 190):
    d.line(s((x0 + 20, dy, x1 - 44, dy)), fill=(52, 34, 26, 255), width=2 * S)
d.ellipse(s((x0 + 26, y0 + 150, x0 + 40, y0 + 164)), fill=(170, 130, 70, 255))
mx0, my0, mx1, my1 = L["music_box"]
d.rectangle(s((mx0, my0 + 10, mx1, my1)), fill=(112, 56, 60, 255), outline=(200, 160, 90, 255), width=2 * S)
d.polygon(s((mx0 - 4, my0 + 12, mx1 + 4, my0 + 12, mx1 - 6, my0 - 8, mx0 + 6, my0 - 8)), fill=(132, 70, 74, 255), outline=(200, 160, 90, 255))  # open lid
d.ellipse(s(((mx0 + mx1) / 2 - 4, my0 + 20, (mx0 + mx1) / 2 + 4, my0 + 28)), fill=(236, 220, 220, 255))  # ballerina
d.line(s(((mx0 + mx1) / 2, my0 + 28, (mx0 + mx1) / 2, my0 + 44)), fill=(236, 220, 220, 255), width=3 * S)
d.ellipse(s(((mx0 + mx1) / 2 - 9, my0 + 36, (mx0 + mx1) / 2 + 9, my0 + 44)), fill=(236, 200, 210, 255))
d.ellipse(s((mx1 + 2, my1 - 22, mx1 + 10, my1 - 14)), fill=(20, 12, 10, 255))           # empty key hole with soot
d.ellipse(s((mx1, my1 - 24, mx1 + 12, my1 - 12)), outline=(30, 26, 26, 180), width=2 * S)
# brush, perfume bottles
d.ellipse(s((x0 + 30, y0 + 40, x0 + 50, y0 + 60)), fill=(90, 130, 140, 220))
d.ellipse(s((x0 + 56, y0 + 30, x0 + 70, y0 + 44)), fill=(140, 90, 120, 220))

# ----------------------------------------------------------------- covered sofa + side table
x0, y0, x1, y1 = L["sofa"]
drop_shadow(L["sofa"], 12, 170)
sheet = layer()
shd = ImageDraw.Draw(sheet)
shd.rounded_rectangle(s((x0, y0, x1, y1)), radius=34 * S, fill=(150, 150, 146, 255))
shd.rounded_rectangle(s((x0 + 16, y0 + 12, x1 - 16, y0 + 70)), radius=24 * S, fill=(128, 128, 126, 255))   # back cushion hump
shd.ellipse(s((x0 - 6, y0 + 40, x0 + 50, y1 - 10)), fill=(136, 136, 132, 255))                             # arms
shd.ellipse(s((x1 - 50, y0 + 40, x1 + 6, y1 - 10)), fill=(136, 136, 132, 255))
shd.ellipse(s((x0 + 120, y0 + 96, x0 + 170, y0 + 132)), fill=(104, 104, 102, 255))                         # the dent where someone small sits
for k in range(7):                                                                                         # sheet folds
    fx_ = x0 + 30 + k * 38
    shd.arc(s((fx_ - 30, y0 + 40, fx_ + 30, y1 + 30)), 200, 250, fill=(96, 96, 94, 255), width=3 * S)
shd.line(s((x0 + 10, y1 - 8, x1 - 10, y1 - 8)), fill=(110, 110, 108, 255), width=4 * S)
sa = np.asarray(sheet).astype(np.float64)
folds = noise_img(W * S, H * S, 30, 3, seed=12)
sa[..., :3] *= (0.62 + 0.24 * folds)[..., None]
canvas.alpha_composite(Image.fromarray(np.clip(sa, 0, 255).astype(np.uint8), "RGBA"))
x0, y0, x1, y1 = L["side_table"]
drop_shadow(L["side_table"], 6, 150)
d.ellipse(s((x0, y0, x1, y1)), fill=(66, 44, 32, 255), outline=(100, 70, 50, 255), width=2 * S)
d.rectangle(s((x0 + 30, y0 + 20, x0 + 44, y0 + 50)), fill=(200, 196, 186, 255))       # candle stub
d.ellipse(s((x0 + 50, y0 + 26, x0 + 74, y0 + 50)), fill=(160, 150, 130, 200))          # saucer

# ----------------------------------------------------------------- rocking chair (double-exposed: it is moving)
x0, y0, x1, y1 = L["rocking_chair"]


def chair(dr, dx, alpha):
    # runners first so the seat sits on top of them
    dr.line(s((x0 + 12 + dx, y0 - 6, x0 + 18 + dx, y1 + 12)), fill=(58, 38, 28, alpha), width=6 * S)
    dr.line(s((x1 - 12 + dx, y0 - 6, x1 - 18 + dx, y1 + 12)), fill=(58, 38, 28, alpha), width=6 * S)
    dr.rounded_rectangle(s((x0 + 16 + dx, y0 + 34, x1 - 16 + dx, y1 - 14)), radius=10 * S, fill=(92, 60, 42, alpha), outline=(120, 84, 58, alpha), width=2 * S)
    dr.rounded_rectangle(s((x0 + 30 + dx, y0 + 46, x1 - 30 + dx, y1 - 26)), radius=8 * S, fill=(86, 34, 38, alpha))  # cushion
    dr.rounded_rectangle(s((x0 + 14 + dx, y0 + 6, x1 - 14 + dx, y0 + 34)), radius=8 * S, fill=(74, 48, 34, alpha), outline=(110, 76, 52, alpha), width=2 * S)
    for k in range(5):
        sx = x0 + 26 + dx + k * 14
        dr.line(s((sx, y0 + 10, sx, y0 + 30)), fill=(46, 30, 22, alpha), width=2 * S)


drop_shadow(L["rocking_chair"], 8, 140)
ghost_chair = layer()
chair(ImageDraw.Draw(ghost_chair), 9, 110)
canvas.alpha_composite(ghost_chair.filter(ImageFilter.GaussianBlur(2 * S)))
chair(d, 0, 255)

# ----------------------------------------------------------------- doll
x0, y0, x1, y1 = L["doll"]
d.ellipse(s((x0 + 14, y0, x0 + 34, y0 + 20)), fill=(226, 210, 196, 255))
d.pieslice(s((x0 + 12, y0 - 2, x0 + 36, y0 + 18)), 180, 360, fill=(160, 70, 40, 255))
d.polygon(s((x0 + 10, y1, x0 + 16, y0 + 18, x0 + 32, y0 + 18, x0 + 40, y1)), fill=(90, 110, 140, 255))
d.line(s((x0 + 18, y0 + 8, x0 + 21, y0 + 11)), fill=(20, 20, 20, 255), width=2 * S)     # stitched X eye
d.line(s((x0 + 21, y0 + 8, x0 + 18, y0 + 11)), fill=(20, 20, 20, 255), width=2 * S)

# ----------------------------------------------------------------- bookcase, toy chest, blocks
x0, y0, x1, y1 = L["bookcase"]
drop_shadow(L["bookcase"], 10, 170, (10, 6))
d.rectangle(s((x0, y0, x1, y1)), fill=(56, 36, 26, 255), outline=(92, 62, 44, 255), width=3 * S)
for shelf_y in range(y0 + 12, y1 - 10, 44):
    bx = x0 + 10
    while bx < x1 - 14:
        bw = random.randint(7, 13)
        col = random.choice([(90, 40, 40), (40, 60, 80), (70, 70, 40), (60, 44, 70), (100, 84, 60)])
        lean = random.random() < 0.15
        if lean:
            d.polygon(s((bx, shelf_y + 36, bx + bw, shelf_y + 36, bx + bw + 8, shelf_y + 6, bx + 8, shelf_y + 6)), fill=col + (255,))
        else:
            d.rectangle(s((bx, shelf_y + 4, bx + bw, shelf_y + 36)), fill=col + (255,))
        bx += bw + 2
    d.line(s((x0 + 4, shelf_y + 38, x1 - 4, shelf_y + 38)), fill=(36, 24, 18, 255), width=3 * S)
x0, y0, x1, y1 = L["toy_chest"]
drop_shadow(L["toy_chest"], 10, 170)
d.rounded_rectangle(s((x0, y0, x1, y1)), radius=10 * S, fill=(88, 58, 40, 255), outline=(128, 90, 60, 255), width=3 * S)
d.rectangle(s((x0 + 6, y0 + 36, x1 - 6, y0 + 46)), fill=(120, 96, 60, 255))
d.rectangle(s(((x0 + x1) / 2 - 10, y0 + 30, (x0 + x1) / 2 + 10, y0 + 52)), fill=(170, 140, 80, 255))
d.ellipse(s((x0 + 20, y0 + 62, x0 + 46, y0 + 88)), outline=(200, 170, 110, 255), width=2 * S)   # painted star
d.ellipse(s((x1 - 46, y0 + 62, x1 - 20, y0 + 88)), outline=(200, 170, 110, 255), width=2 * S)
x0, y0, x1, y1 = L["blocks"]
for k, (bx, by, col) in enumerate(((x0, y0 + 20, (150, 60, 50)), (x0 + 26, y0 + 10, (60, 100, 150)), (x0 + 52, y0 + 24, (170, 150, 70)), (x0 + 18, y0 - 8, (80, 130, 80)))):
    d.rectangle(s((bx, by, bx + 22, by + 22)), fill=col + (255,), outline=(30, 20, 16, 255), width=1 * S)

# ----------------------------------------------------------------- wet child footprints: mirror -> vanity
fp = layer()
fpd = ImageDraw.Draw(fp)
path = [(960, 318), (1000, 380), (1070, 420), (1180, 440), (1300, 450), (1420, 470), (1500, 510), (1560, 560)]
pts = []
for (ax, ay), (bx, by) in zip(path, path[1:]):
    n = max(1, int(math.hypot(bx - ax, by - ay) / 34))
    for k in range(n):
        t = k / n
        pts.append((ax + (bx - ax) * t, ay + (by - ay) * t, math.atan2(by - ay, bx - ax)))
for i, (px, py, a) in enumerate(pts):
    side = 1 if i % 2 else -1
    ox, oy = -math.sin(a) * 7 * side, math.cos(a) * 7 * side
    cx_, cy_ = px + ox, py + oy
    ca, sa_ = math.cos(a), math.sin(a)
    foot = [(cx_ + ca * dx - sa_ * dy, cy_ + sa_ * dx + ca * dy) for dx, dy in ((-8, -4), (6, -5), (9, 0), (6, 5), (-8, 4))]
    fpd.polygon(s(sum(foot, ())), fill=(130, 185, 215, 95))
    fpd.ellipse(s((cx_ + ca * 12 - 3, cy_ + sa_ * 12 - 3, cx_ + ca * 12 + 3, cy_ + sa_ * 12 + 3)), fill=(140, 190, 215, 70))
canvas.alpha_composite(fp.filter(ImageFilter.GaussianBlur(1.2 * S)))

# ----------------------------------------------------------------- candelabras (blue ghost flames)
flames = []


def candelabra(box):
    x0, y0, x1, y1 = box
    cx_, cy_ = (x0 + x1) / 2, (y0 + y1) / 2
    drop_shadow((x0 + 10, y0 + 10, x1 - 10, y1 - 10), 5, 150)
    d.ellipse(s((cx_ - 22, cy_ - 22, cx_ + 22, cy_ + 22)), fill=(120, 96, 52, 255), outline=(180, 150, 80, 255), width=2 * S)
    for k in range(3):
        a = k * 2 * math.pi / 3 - math.pi / 2
        px, py = cx_ + math.cos(a) * 16, cy_ + math.sin(a) * 16
        d.ellipse(s((px - 5, py - 5, px + 5, py + 5)), fill=(210, 206, 196, 255))
        d.polygon(s((px - 3, py - 1, px + 3, py - 1, px, py - 11)), fill=FLAME_FILL)
        flames.append((px, py - 5))
    d.ellipse(s((cx_ - 5, cy_ - 5, cx_ + 5, cy_ + 5)), fill=(210, 206, 196, 255))
    d.polygon(s((cx_ - 3, cy_ - 1, cx_ + 3, cy_ - 1, cx_, cy_ - 12)), fill=FLAME_FILL)
    flames.append((cx_, cy_ - 5))


candelabra(L["candelabra_a"])
candelabra(L["candelabra_b"])
mantel = L["fireplace"]
for mx in (mantel[0] + 20, mantel[2] - 20):
    d.rectangle(s((mx - 5, mantel[1] - 6, mx + 5, mantel[1] + 18)), fill=(226, 222, 210, 255))
    flames.append((mx, mantel[1] - 8))
L["flames"] = flames

# ----------------------------------------------------------------- cobwebs in corners
web = layer()
wbd = ImageDraw.Draw(web)
for (cx_, cy_, a0) in ((150, 300, 0), (1770, 300, 90), (150, 960, 270), (1770, 960, 180), (124, 44, 0), (1796, 44, 90)):
    for k in range(7):
        a = math.radians(a0 + k * 15)
        wbd.line(s((cx_, cy_, cx_ + math.cos(a) * 110, cy_ + math.sin(a) * 110)), fill=(200, 205, 210, 60), width=1 * S)
    for rr in range(20, 110, 18):
        pts_ = [(cx_ + math.cos(math.radians(a0 + k * 15)) * rr, cy_ + math.sin(math.radians(a0 + k * 15)) * rr) for k in range(7)]
        wbd.line(s(sum(pts_, ())), fill=(200, 205, 210, 45), width=1 * S)
canvas.alpha_composite(web)

# ----------------------------------------------------------------- downsample and light
img = canvas.resize((W, H), Image.LANCZOS)
arr = np.asarray(img).astype(np.float64)[..., :3] / 255.0
yy, xx = np.mgrid[0:H, 0:W].astype(np.float64)

# cold ambient grade: crush and push toward blue-green
lum = arr.mean(axis=2, keepdims=True)
arr = arr * 0.62 + lum * 0.12
arr *= np.array([0.78, 0.9, 1.08])

light = np.zeros((H, W, 3))


def glow(cx_, cy_, radius, color, strength):
    dist2 = ((xx - cx_) ** 2 + (yy - cy_) ** 2) / (radius ** 2)
    light[...] += np.exp(-dist2)[..., None] * np.array(color) * strength


for fx, fy in flames:
    glow(fx, fy, 170, FLAME_GLOW, 0.42)
    glow(fx, fy, 12, FLAME_CORE, 0.45)
# moonlight shaft from the window, falling down-right across the floor
wx0, wy0, wx1, wy1 = L["window"]
t = np.clip((yy - wy0) / 620.0, 0, 1)
cxs = (wx0 + wx1) / 2 + t * 260
half = 70 + t * 110
shaft = np.clip(1 - np.abs(xx - cxs) / half, 0, 1) ** 1.5 * (yy > wy0) * (1 - t) ** 0.6
light += shaft[..., None] * np.array([0.55, 0.7, 0.9]) * 0.32
glow((wx0 + wx1) / 2, (wy0 + wy1) / 2, 90, (0.6, 0.75, 0.95), 0.35)
# the mirror gives off a faint light of its own
mx0, my0, mx1, my1 = L["mirror"]
glow((mx0 + mx1) / 2, (my0 + my1) / 2 + 10, 140, (0.5, 0.8, 0.9), 0.22)
# the key glint in the ash catches the candlelight
kx, ky = L["key_glint"]
glow(kx, ky, 14, (1.0, 0.85, 0.5), 0.5)
arr = arr * (0.72 + light) + light * 0.05

# drifting fog
fog = noise_img(W, H, 260, 4, seed=21)
fog = np.clip((fog - 0.45) * 2.2, 0, 1)
arr = arr * (1 - fog[..., None] * 0.18) + fog[..., None] * np.array([0.28, 0.36, 0.42]) * 0.18

# vignette
v = np.sqrt(((xx - W / 2) / (W * 0.62)) ** 2 + ((yy - H / 2) / (H * 0.66)) ** 2)
arr *= np.clip(1.15 - v ** 2.2, 0.12, 1)[..., None]

# painterly grain + dust motes
arr += (rng.random((H, W, 1)) - 0.5) * 0.035
out = Image.fromarray((np.clip(arr, 0, 1) * 255).astype(np.uint8), "RGB")
motes = Image.new("RGBA", (W, H), (0, 0, 0, 0))
md_ = ImageDraw.Draw(motes)
for _ in range(160):
    px, py = random.uniform(150, 1770), random.uniform(60, 960)
    r_ = random.uniform(0.8, 2.2)
    md_.ellipse((px - r_, py - r_, px + r_, py + r_), fill=(200, 225, 240, random.randint(40, 120)))
out = out.convert("RGBA")
out.alpha_composite(motes.filter(ImageFilter.GaussianBlur(0.6)))
out = out.convert("RGB").filter(ImageFilter.UnsharpMask(radius=1.2, percent=40, threshold=2))
out.save(os.path.join(SP, OUT_NAME))
json.dump(L, open(os.path.join(SP, "room03_layout.json"), "w"), indent=1)
print("saved")
