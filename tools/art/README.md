# Room background scripts

Requires `pip install pillow numpy scipy`.

| Script | Output | Copy over |
|---|---|---|
| `room01.py` | `Room01_Study_Dark.png` | `Assets/Art/Backgrounds/Room01_Study_Dark.png` |
| `room02.py` | `Room02_Library_Dark.png` | `Assets/Art/Backgrounds/Room02_Library_Dark.png` |
| `paint_room03.py` | `Room03_HauntedParlor.png` | `Assets/Art/Backgrounds/Room03_HauntedParlor.png` |

Room01/02 are assembled from the pixel-art packs in `CopyPasteAssets/`
(mostly `Assets Source 1`), scaled 4x, then lit by `compose_lib.Room.finish`
(same lighting idea as Room03). Each run also writes `*_layout.json` with the
pixel box of every named prop. Keep props where they are, or move the
colliders in the scenes to match (world_x = (px - 960) / 108,
world_y = (540 - py) / 108; the player collides at the feet).

Overwrite the PNG only; keep its `.meta` so references stay valid.
`python atlas.py C D E` writes labelled previews of the sprite sheets
(`atlas_C.png` ...) to find sprite indices.
