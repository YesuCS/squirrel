#!/usr/bin/env python3
"""Generate Squirrel's pixel-art icon assets from the 16x16 sprite below.

Requires Pillow:  pip install pillow
Outputs into src/Squirrel.App/Assets: squirrel.png (1024), squirrel-tray.png
(64), squirrel.ico, squirrel.icns. Run from anywhere.
"""
import os
from PIL import Image

# 16x16 sprite; one character per pixel.
# . transparent  O outline  B body  L belly  T tail  H tail highlight
# E eye  W eye glint  N nose  A acorn  C acorn cap
SPRITE = [
    "................",
    ".........OOO....",
    "....O...OTTTO...",
    "...OBO.OTHHTTO..",
    "...OBBOOTHHHTO..",
    "..OBEBBOTHHHTO..",
    "..OBBBBOTHHHTO..",
    "...OBBBOOTHHTO..",
    "..OBLLBBOTHHTO..",
    ".OBLLLLBOTHTO...",
    ".OBLLLLBBOTTO...",
    ".OBLLLLLBBTO....",
    ".OBBLLLLBBO.....",
    "..OBBBBBBO......",
    "...OO..OO.......",
    "................",
]

PALETTE = {
    "O": (74, 44, 21, 255),     # dark brown outline
    "B": (201, 111, 34, 255),   # chestnut body
    "L": (244, 196, 138, 255),  # cream belly
    "T": (168, 78, 24, 255),    # rust tail
    "H": (224, 138, 60, 255),   # tail highlight
    "E": (28, 18, 10, 255),     # eye
    "W": (255, 255, 255, 255),  # eye glint
    "N": (60, 34, 16, 255),     # nose
    "A": (170, 110, 50, 255),   # acorn
    "C": (100, 62, 30, 255),    # acorn cap
    ".": (0, 0, 0, 0),
}


def render(scale: int) -> Image.Image:
    img = Image.new("RGBA", (16, 16), (0, 0, 0, 0))
    for y, row in enumerate(SPRITE):
        for x, ch in enumerate(row):
            img.putpixel((x, y), PALETTE[ch])
    return img.resize((16 * scale, 16 * scale), Image.NEAREST)


def main() -> None:
    root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    assets = os.path.join(root, "src", "Squirrel.App", "Assets")
    os.makedirs(assets, exist_ok=True)

    render(64).save(os.path.join(assets, "squirrel.png"))       # 1024
    render(4).save(os.path.join(assets, "squirrel-tray.png"))   # 64
    render(16).save(
        os.path.join(assets, "squirrel.ico"),
        sizes=[(16, 16), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)],
    )
    render(64).save(
        os.path.join(assets, "squirrel.icns"),
        sizes=[(16, 16), (32, 32), (64, 64), (128, 128),
               (256, 256), (512, 512), (1024, 1024)],
    )
    print("icon assets written to", assets)


if __name__ == "__main__":
    main()
