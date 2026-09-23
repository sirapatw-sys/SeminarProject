"""Room01 "ห้องทำงานเก่า": an ordinary study, dark and quietly wrong.
Built from Assets Source 1 (Inside_C / Inside_E / A4 / A5)."""
import os
import random

import numpy as np
from PIL import Image, ImageDraw

from compose_lib import Room, sprite, region, up, trim, cobweb, pixel_canvas, PX, FLOOR
from paths import SP


def split_lower(s):
    """C105 holds two desks stacked; keep the lower one (with drawers)."""
    a = np.asarray(s)[..., 3] > 10
    rows = np.where(~a.any(axis=1))[0]
    mid = [r for r in rows if 20 < r < s.height - 20]
    cut = mid[0] if mid else s.height // 2
    return trim(s.crop((0, cut, s.width, s.height)))


def painting():
    """The puzzle painting: a small moonlit castle in a gilt frame, drawn as
    pixel art at 1x so it scales like the pack."""
    frame = sprite("C", 94)            # empty dark frame 42x35
    im, d = pixel_canvas(frame.width, frame.height)
    iw0, ih0, iw1, ih1 = 4, 4, frame.width - 5, frame.height - 5
    for y in range(ih0, ih1):
        t = (y - ih0) / (ih1 - ih0)
        c = (int(18 + 22 * t), int(24 + 18 * t), int(48 + 10 * t), 255)
        d.line((iw0, y, iw1, y), fill=c)
    d.ellipse((iw1 - 11, ih0 + 2, iw1 - 6, ih0 + 7), fill=(214, 220, 196, 255))       # moon
    d.polygon((iw0, ih1, iw0 + 10, ih1 - 9, iw0 + 20, ih1 - 4, iw1, ih1 - 11, iw1, ih1), fill=(20, 26, 30, 255))  # hills
    cx = iw0 + 13
    d.rectangle((cx - 4, ih1 - 16, cx + 4, ih1 - 7), fill=(28, 30, 40, 255))            # castle
    d.rectangle((cx - 6, ih1 - 19, cx - 3, ih1 - 7), fill=(28, 30, 40, 255))
    d.rectangle((cx + 3, ih1 - 21, cx + 6, ih1 - 7), fill=(28, 30, 40, 255))
    d.point((cx, ih1 - 12), fill=(230, 190, 90, 255))                                    # one lit window
    d.line((iw0, ih1 - 1, iw1, ih1 - 1), fill=(40, 60, 70, 255))                         # lake
    im.alpha_composite(frame)
    # a little gilt so it reads as a painting, not an empty frame
    g = ImageDraw.Draw(im)
    g.rectangle((0, 0, im.width - 1, im.height - 1), outline=(150, 112, 56, 255))
    return im


def padlock_on(s):
    """Hang a small brass padlock on the drawer chest."""
    s = s.copy()
    d = ImageDraw.Draw(s)
    cx, cy = s.width // 2, s.height // 2 + 4
    d.arc((cx - 3, cy - 6, cx + 3, cy), 180, 360, fill=(150, 150, 160, 255))
    d.rectangle((cx - 3, cy - 2, cx + 3, cy + 3), fill=(190, 150, 60, 255))
    d.point((cx, cy), fill=(40, 30, 20, 255))
    return s


def tally(img, x, y, groups, color=(210, 205, 190, 120)):
    """Days counted into the wallpaper by someone who was here before."""
    d = ImageDraw.Draw(img)
    for g in range(groups):
        gx = x + (g % 6) * 34
        gy = y + (g // 6) * 30
        for k in range(4):
            d.line((gx + k * 6, gy, gx + k * 6 + 1, gy + 20), fill=color, width=2)
        d.line((gx - 3, gy + 16, gx + 22, gy + 4), fill=color, width=2)


def build():
    r = Room(seed=11)
    # ---- structure
    wall = region("A4", (192, 383 + 32, 288, 479))       # green stripes + wainscot band
    r.wall(wall)
    wood = [region("A5", (0, 240, 48, 288)), region("A5", (48, 240, 96, 288))]
    r.floor(wood, darken=0.68)
    r.frame()

    # ---- back wall, left to right
    r.place(sprite("C", 112), 215, 336, name="bookshelf_a")
    r.place(sprite("C", 109), 345, 336, name="bookshelf_b")
    win = r.place(sprite("C", 2), 568, 272, name="window", shadow=False)
    curtain = trim(region("D", (48, 548, 96, 646)))       # red curtain
    r.place(curtain, win[0] - 6, win[3] + 12, k=2, shadow=False)
    r.place(curtain, win[2] + 6, win[3] + 12, k=2, shadow=False, flip=True)
    r.place(sprite("C", 22), 770, 214, name="wall_clock", shadow=False)
    paint = r.place(painting(), 960, 250, rotate=-4.0, name="painting", shadow=False)
    board = sprite("C", 95)                                  # pinned notes
    r.place(board, 1150, 200, k=3, name="notes", shadow=False)
    r.place(sprite("C", 14), 1300, 336, name="sideboard")
    door = r.place(sprite("E", 19), 1580, 304, k=3, name="door", shadow=False)
    tally(r.img, 1426, 70, 11)

    # ---- floor
    rug = sprite("E", 0)
    r.place(rug, 1010, 860, k=3, name="rug", shadow=False)
    r.place(sprite("C", 106), 500, 520, name="desk_chair", shadow=False)
    desk = split_lower(sprite("C", 105))
    r.place(desk, 470, 610, name="desk")
    book = sprite("C", 24)
    r.place(book, 430, 520, k=3, shadow=False)
    drawer = padlock_on(sprite("C", 101))
    r.place(drawer, 708, 600, name="drawer")
    # seen from behind, facing the side table and the back wall
    r.place(sprite("C", 27), 1470, 860, name="sofa")
    r.place(sprite("C", 113), 1470, 700, k=3, name="side_table")
    r.place(sprite("C", 21), 1712, 430, name="plant")
    r.place(sprite("C", 64), 214, 940, k=3, name="plant_pot")
    r.place(sprite("C", 88), 330, 936, k=3, name="book_pile")
    # a chair knocked onto its back, and papers drifting away from the desk
    pd = ImageDraw.Draw(r.img)
    for (px, py, a) in ((600, 660, 12), (660, 700, -20), (730, 760, 35), (560, 720, -8), (900, 700, 18)):
        paper = Image.new("RGBA", (26, 32), (206, 198, 176, 255))
        ImageDraw.Draw(paper).line((4, 8, 20, 8), fill=(120, 110, 100, 255))
        ImageDraw.Draw(paper).line((4, 14, 18, 14), fill=(120, 110, 100, 255))
        paper = paper.rotate(a, expand=True)
        r.img.alpha_composite(paper, (px, py))
    for c in ((150, 300, 0), (1770, 300, 90)):
        cobweb(r.img, *c, size=90, alpha=45)

    # ---- light: a desk lamp left on, candles on the sideboard, the moon
    r.glow(470, 470, 300, (1.0, 0.7, 0.36), 0.9)
    r.glow(470, 470, 40, (1.0, 0.85, 0.6), 0.6)
    sb = r.layout["sideboard"]
    for fx in (sb[0] + 18, sb[0] + 34):
        r.glow(fx, sb[1] + 20, 90, (1.0, 0.7, 0.35), 0.35)
    r.glow(1580, 150, 120, (0.4, 0.55, 0.8), 0.12)     # cold draught at the door
    out = os.path.join(SP, "Room01_Study_Dark.png")
    r.finish(out, ambient=0.46, grade=(0.8, 0.86, 1.0), desat=0.28, moon_strength=0.55,
             moon=(win[0] + 40, win[1] + 30, win[2] - 40, 640, 300))
    print("saved", out)


if __name__ == "__main__":
    build()
