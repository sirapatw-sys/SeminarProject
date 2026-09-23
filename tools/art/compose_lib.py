"""Shared pieces for composing room backgrounds out of the project's pixel-art
asset packs, then lighting them the way Room03 is lit.

Coordinates are final pixels on a 1920x1080 canvas. Sprites are scaled with
nearest-neighbour by an integer factor (4 by default, so a 32 px tile of
Assets Source 1 becomes 128 px, about a character's height)."""
import json
import math
import os
import random

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

from atlas import components, load

W, H = 1920, 1080
PX = 4                        # pixel scale for sprites
FLOOR = (150, 300, 1770, 960)  # walkable floor, same frame as Room03
WALL = (120, 44, 1800, 300)    # back wall face

_sheets = {}


def sheet(key):
    if key not in _sheets:
        im = load(key)
        _sheets[key] = (im, [list(b) for b in components(im)])
    return _sheets[key]


def sprite(key, index, crop=None):
    """One sprite from a sheet (1x). crop=(x0,y0,x1,y1) is relative to its box."""
    im, boxes = sheet(key)
    x0, y0, x1, y1 = boxes[index]
    s = im.crop((x0, y0, x1, y1))
    if crop:
        s = s.crop(crop)
    return trim(s)


def region(key, box):
    im, _ = sheet(key)
    return im.crop(box)


def trim(s):
    bb = s.getbbox()
    return s.crop(bb) if bb else s


def up(s, k=PX):
    return s.resize((s.width * k, s.height * k), Image.NEAREST)


class Room:
    def __init__(self, seed=1):
        random.seed(seed)
        self.rng = np.random.default_rng(seed)
        self.img = Image.new("RGBA", (W, H), (7, 9, 14, 255))
        self.d = ImageDraw.Draw(self.img)
        self.layout = {}
        self.lights = []

    # ------------------------------------------------------------ structure
    def floor(self, tiles, k=PX, darken=1.0):
        """tiles: list of 1x RGBA tiles, used in a shuffled pattern."""
        x0, y0, x1, y1 = FLOOR
        layer = Image.new("RGBA", (x1 - x0, y1 - y0))
        big = [up(t, k) for t in tiles]
        tw, th = big[0].size
        for ty in range(0, y1 - y0, th):
            offset = (ty // th % 2) * (tw // 2)
            for tx in range(-offset, x1 - x0, tw):
                layer.alpha_composite(random.choice(big), (max(tx, 0), ty), (max(-tx, 0), 0))
        if darken != 1.0:
            a = np.asarray(layer).astype(np.float64)
            a[..., :3] *= darken
            layer = Image.fromarray(a.astype(np.uint8), "RGBA")
        self.img.alpha_composite(layer, (x0, y0))

    def wall(self, tile, k=PX):
        """tile: 1x RGBA wall strip whose bottom sits on the floor line."""
        x0, y0, x1, y1 = WALL
        t = up(tile, k)
        layer = Image.new("RGBA", (x1 - x0, y1 - y0))
        for tx in range(0, x1 - x0, t.width):
            layer.alpha_composite(t, (tx, (y1 - y0) - t.height))
        self.img.alpha_composite(layer, (x0, y0))

    def frame(self, side=(20, 26, 30), beam=(44, 30, 24), beam_hi=(74, 52, 40)):
        d = self.d
        for x0, x1 in ((100, 150), (1770, 1820)):
            d.rectangle((x0, 300, x1, 960), fill=side + (255,))
        d.rectangle((96, 20, 1824, 44), fill=beam + (255,))
        d.line((96, 44, 1824, 44), fill=beam_hi + (255,), width=2)
        d.rectangle((96, 958, 1824, 1000), fill=beam + (255,))
        d.line((96, 958, 1824, 958), fill=beam_hi + (255,), width=2)
        d.rectangle((96, 20, 124, 1000), fill=beam + (255,))
        d.rectangle((1796, 20, 1824, 1000), fill=beam + (255,))
        d.rectangle((146, 296, 154, 960), fill=(30, 21, 17, 255))
        d.rectangle((1766, 296, 1774, 960), fill=(30, 21, 17, 255))
        for cx, cy in ((110, 32), (1810, 32), (110, 980), (1810, 980), (560, 980), (1360, 980)):
            d.rectangle((cx - 26, cy - 24, cx + 26, cy + 24), fill=(36, 25, 20, 255), outline=(78, 56, 42, 255), width=2)
        sh = Image.new("RGBA", (W, H))
        sd = ImageDraw.Draw(sh)
        sd.rectangle((150, 300, 1770, 322), fill=(0, 0, 0, 150))
        sd.rectangle((150, 300, 176, 960), fill=(0, 0, 0, 110))
        sd.rectangle((1744, 300, 1770, 960), fill=(0, 0, 0, 110))
        self.img.alpha_composite(sh.filter(ImageFilter.GaussianBlur(12)))

    # ------------------------------------------------------------ props
    def place(self, s, cx, bottom, k=PX, name=None, shadow=True, rotate=0.0, flip=False):
        """Put a 1x sprite with its bottom-centre at (cx, bottom)."""
        big = up(s, k) if k != 1 else s
        if flip:
            big = big.transpose(Image.FLIP_LEFT_RIGHT)
        if rotate:
            big = big.rotate(rotate, resample=Image.BICUBIC, expand=True)
        x = int(round(cx - big.width / 2))
        y = int(round(bottom - big.height))
        if shadow:
            self.contact_shadow(x, y, big)
        self.img.alpha_composite(big, (x, y))
        box = (x, y, x + big.width, y + big.height)
        if name:
            self.layout[name] = box
        return box

    def contact_shadow(self, x, y, big, strength=150):
        """A soft dark pool under a standing prop."""
        w, h = big.size
        sh = Image.new("RGBA", (W, H))
        ImageDraw.Draw(sh).ellipse((x - 8, y + h - 22, x + w + 8, y + h + 14), fill=(0, 0, 0, strength))
        self.img.alpha_composite(sh.filter(ImageFilter.GaussianBlur(10)))

    def glow(self, x, y, radius, color, strength):
        self.lights.append((x, y, radius, color, strength))

    # ------------------------------------------------------------ finishing
    def finish(self, path, ambient=0.66, grade=(0.84, 0.92, 1.06), moon=None, fog_amount=0.16,
               motes=140, vignette=1.15, desat=0.1, moon_strength=0.3):
        img = self.img.convert("RGB")
        arr = np.asarray(img).astype(np.float64) / 255.0
        yy, xx = np.mgrid[0:H, 0:W].astype(np.float64)
        lum = arr.mean(axis=2, keepdims=True)
        arr = arr * (1 - desat) + lum * desat
        arr *= np.array(grade)

        light = np.zeros((H, W, 3))
        for x, y, r, color, strength in self.lights:
            d2 = ((xx - x) ** 2 + (yy - y) ** 2) / (r * r)
            light += np.exp(-d2)[..., None] * np.array(color) * strength
        if moon:
            wx0, wy0, wx1, reach, drift = moon
            t = np.clip((yy - wy0) / reach, 0, 1)
            cxs = (wx0 + wx1) / 2 + t * drift
            half = (wx1 - wx0) / 2 + t * 110
            shaft = np.clip(1 - np.abs(xx - cxs) / half, 0, 1) ** 1.5 * (yy > wy0) * (1 - t) ** 0.6
            light += shaft[..., None] * np.array([0.55, 0.7, 0.9]) * moon_strength
        arr = arr * (ambient + light) + light * 0.05

        if fog_amount:
            fog = _noise(W, H, 260, 4, 21)
            fog = np.clip((fog - 0.45) * 2.2, 0, 1)
            arr = arr * (1 - fog[..., None] * fog_amount) + fog[..., None] * np.array([0.26, 0.3, 0.36]) * fog_amount
        v = np.sqrt(((xx - W / 2) / (W * 0.62)) ** 2 + ((yy - H / 2) / (H * 0.66)) ** 2)
        arr *= np.clip(vignette - v ** 2.2, 0.12, 1)[..., None]
        arr += (self.rng.random((H, W, 1)) - 0.5) * 0.03
        out = Image.fromarray((np.clip(arr, 0, 1) * 255).astype(np.uint8), "RGB").convert("RGBA")
        m = Image.new("RGBA", (W, H))
        md = ImageDraw.Draw(m)
        for _ in range(motes):
            px, py = random.uniform(150, 1770), random.uniform(60, 960)
            rr = random.uniform(0.8, 2.0)
            md.ellipse((px - rr, py - rr, px + rr, py + rr), fill=(215, 225, 235, random.randint(35, 100)))
        out.alpha_composite(m.filter(ImageFilter.GaussianBlur(0.6)))
        out.convert("RGB").save(path)
        json.dump(self.layout, open(os.path.splitext(path)[0] + "_layout.json", "w"), indent=1)


def _noise(w, h, scale, octaves, seed):
    r = np.random.default_rng(seed)
    acc = np.zeros((h, w))
    amp, total = 1.0, 0.0
    for _ in range(octaves):
        sw, sh = max(2, int(w / scale) + 2), max(2, int(h / scale) + 2)
        base = r.random((sh, sw))
        acc += np.asarray(Image.fromarray((base * 255).astype(np.uint8)).resize((w, h), Image.BICUBIC)) / 255.0 * amp
        total += amp
        amp *= 0.5
        scale /= 2.0
    return acc / total


def cobweb(img, cx, cy, a0, size=110, alpha=55):
    web = Image.new("RGBA", img.size)
    wd = ImageDraw.Draw(web)
    for k in range(7):
        a = math.radians(a0 + k * 15)
        wd.line((cx, cy, cx + math.cos(a) * size, cy + math.sin(a) * size), fill=(200, 205, 210, alpha), width=1)
    for rr in range(20, size, 18):
        pts = [(cx + math.cos(math.radians(a0 + k * 15)) * rr, cy + math.sin(math.radians(a0 + k * 15)) * rr) for k in range(7)]
        wd.line(sum(pts, ()), fill=(200, 205, 210, alpha - 12), width=1)
    img.alpha_composite(web)


def pixel_canvas(w, h):
    """A 1x canvas for drawing bespoke pixel art that is then scaled like the packs."""
    im = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    return im, ImageDraw.Draw(im)
