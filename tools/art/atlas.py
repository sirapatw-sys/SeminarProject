"""Find sprites on the asset sheets and write labelled previews."""
import json
import os
import sys

import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage

from paths import PROJ, SP

CP = os.path.join(PROJ, "CopyPasteAssets")
SHEETS = {
    "C": "Assets Source 1/Inside_C.png",
    "D": "Assets Source 1/Inside_D.png",
    "E": "Assets Source 1/Inside_E.png",
    "A4": "Assets Source 1/A4.png",
    "A5": "Assets Source 1/A5.png",
    "RJ": "Assets Source 1/Bonus/Bonus/DeletedAssets/RejectedAssets1.png",
    "OUT": "Assets Source 1/Outside.png",
}

# Spooky Mansion sheets bake a pink/purple checker instead of alpha.
SPOOKY = "Spooky Mansion Asset Pack/Spooky Mansion Asset Pack"


def load(key):
    path = os.path.join(CP, *SHEETS[key].split("/")) if key in SHEETS else os.path.join(CP, *SPOOKY.split("/"), key + ".png")
    im = Image.open(path).convert("RGBA")
    if key not in SHEETS:
        a = np.asarray(im).copy()
        rgb = a[..., :3].astype(int)
        mask = np.zeros(rgb.shape[:2], bool)
        for c in ((198, 147, 209), (255, 182, 228)):
            mask |= (np.abs(rgb - np.array(c)).sum(axis=2) < 4)
        a[mask, 3] = 0
        im = Image.fromarray(a)
    return im


def components(im, gap=2, min_px=20):
    a = np.asarray(im)[..., 3] > 10
    grown = ndimage.binary_dilation(a, iterations=gap)
    lab, n = ndimage.label(grown)
    boxes = []
    for sl in ndimage.find_objects(lab):
        y0, y1, x0, x1 = sl[0].start, sl[0].stop, sl[1].start, sl[1].stop
        sub = a[y0:y1, x0:x1]
        if sub.sum() < min_px:
            continue
        ys, xs = np.nonzero(sub)
        boxes.append((int(x0 + xs.min()), int(y0 + ys.min()), int(x0 + xs.max() + 1), int(y0 + ys.max() + 1)))
    boxes.sort(key=lambda b: (b[1] // 40, b[0]))
    return boxes


def preview(key, scale=2, gap=2):
    im = load(key)
    boxes = components(im, gap=gap)
    big = im.resize((im.width * scale, im.height * scale), Image.NEAREST)
    bg = Image.new("RGBA", big.size, (40, 44, 52, 255))
    bg.alpha_composite(big)
    d = ImageDraw.Draw(bg)
    for i, (x0, y0, x1, y1) in enumerate(boxes):
        d.rectangle((x0 * scale, y0 * scale, x1 * scale, y1 * scale), outline=(255, 60, 60, 255))
        d.text((x0 * scale + 2, y0 * scale + 1), str(i), fill=(255, 255, 0, 255))
    bg.save(os.path.join(SP, "atlas_%s.png" % key))
    json.dump(boxes, open(os.path.join(SP, "atlas_%s.json" % key), "w"))
    print(key, im.size, len(boxes))


if __name__ == "__main__":
    for k in sys.argv[1:]:
        preview(k)
