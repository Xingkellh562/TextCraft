#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""TextCraft UI 皮肤图集生成器。

生成 RequiredResources/Resources/atlas/uiSkin/texture.png 与 spirits.xml。
所有精灵都用 Pillow 以 4x 超采样绘制后缩小，因此圆角与描边带抗锯齿；
九宫格边距（border）随精灵一起写进 spirits.xml，运行时由 Atlas 读取。

用法（在仓库根目录）：
    python docs/tools/ui_skin_generator.py

输出：
    RequiredResources/Resources/atlas/uiSkin/texture.png
    RequiredResources/Resources/atlas/uiSkin/spirits.xml
"""

from __future__ import annotations

import math
import os
from xml.sax.saxutils import escape

from PIL import Image, ImageDraw

# ---------------------------------------------------------------- 基础工具

S = 4  # 超采样倍数

Color = tuple[int, int, int, int]


def lerp(a: tuple[float, ...], b: tuple[float, ...], t: float) -> Color:
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(len(a)))  # type: ignore[return-value]


def vertical_gradient(width: int, height: int, top: Color, bottom: Color) -> Image.Image:
    img = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    for y in range(height):
        t = y / (height - 1) if height > 1 else 0.0
        draw.line([(0, y), (width - 1, y)], fill=lerp(top, bottom, t))
    return img


def rounded_mask(width: int, height: int, radius: int) -> Image.Image:
    mask = Image.new("L", (width, height), 0)
    ImageDraw.Draw(mask).rounded_rectangle(
        [0, 0, width - 1, height - 1], radius=radius, fill=255
    )
    return mask


def rounded_sprite(
    width: int,
    height: int,
    radius: float,
    top: Color,
    bottom: Color,
    border: Color | None = None,
    border_width: float = 1.6,
    highlight: Color | None = None,
    inner_dark: bool = False,
) -> Image.Image:
    """圆角矩形（竖向渐变 + 描边 + 顶部高光），超采样后缩小。"""
    w, h = width * S, height * S
    r = max(0, int(radius * S))

    base = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    grad = vertical_gradient(w, h, top, bottom)
    mask = rounded_mask(w, h, r)
    base.paste(grad, (0, 0), mask)

    if inner_dark:
        shade = Image.new("RGBA", (w, h), (0, 0, 0, 0))
        sd = ImageDraw.Draw(shade)
        for i in range(int(4 * S)):
            alpha = int(70 * (1 - i / (4 * S)))
            sd.rounded_rectangle(
                [0, 0, w - 1, h - 1 - i], radius=r, outline=(0, 0, 0, alpha), width=1
            )
        base = Image.alpha_composite(base, Image.composite(shade, Image.new("RGBA", (w, h), (0, 0, 0, 0)), mask))

    if highlight is not None and h > 8 * S:
        hi = Image.new("RGBA", (w, h), (0, 0, 0, 0))
        hd = ImageDraw.Draw(hi)
        inset = max(2, int(2 * S))
        hd.rounded_rectangle(
            [inset, inset, w - 1 - inset, h - 1 - inset],
            radius=max(1, r - inset),
            outline=highlight,
            width=max(1, int(1 * S)),
        )
        base = Image.alpha_composite(base, Image.composite(hi, Image.new("RGBA", (w, h), (0, 0, 0, 0)), mask))

    if border is not None:
        bd = Image.new("RGBA", (w, h), (0, 0, 0, 0))
        ImageDraw.Draw(bd).rounded_rectangle(
            [0, 0, w - 1, h - 1], radius=r, outline=border, width=max(1, int(border_width * S))
        )
        base = Image.alpha_composite(base, bd)

    return base.resize((width, height), Image.LANCZOS)


def shadow_sprite(width: int, height: int, radius: float, spread: int, max_alpha: int = 60) -> Image.Image:
    """柔和投影：一圈圈由外向内叠加透明度。"""
    w, h = width * S, height * S
    r = max(0, int(radius * S))
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    steps = max(2, spread * S)
    for i in range(steps):
        t = i / (steps - 1)
        inset = int(spread * S * (1 - t))
        alpha = int(max_alpha * (t ** 2.8) * 0.18)
        if alpha <= 0:
            continue
        layer = Image.new("RGBA", (w, h), (0, 0, 0, 0))
        ImageDraw.Draw(layer).rounded_rectangle(
            [inset, inset, w - 1 - inset, h - 1 - inset], radius=max(1, r - inset // 2), fill=(0, 0, 0, alpha)
        )
        img = Image.alpha_composite(img, layer)
    return img.resize((width, height), Image.LANCZOS)


def triangle_sprite(size: int, color: Color, flip: bool = False) -> Image.Image:
    """一个朝下的实心三角形（下拉箭头）。"""
    d = size * S
    img = Image.new("RGBA", (d, d), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    pad = int(d * 0.22)
    if flip:
        points = [(pad, d - pad), (d - pad, d - pad), (d // 2, pad)]
    else:
        points = [(pad, pad), (d - pad, pad), (d // 2, d - pad)]
    draw.polygon(points, fill=color)
    return img.resize((size, size), Image.LANCZOS)


def circle_sprite(diameter: int, fill_top: Color, fill_bottom: Color, border: Color) -> Image.Image:
    d = diameter * S
    img = Image.new("RGBA", (d, d), (0, 0, 0, 0))
    grad = vertical_gradient(d, d, fill_top, fill_bottom)
    mask = Image.new("L", (d, d), 0)
    ImageDraw.Draw(mask).ellipse([0, 0, d - 1, d - 1], fill=255)
    img.paste(grad, (0, 0), mask)
    bd = Image.new("RGBA", (d, d), (0, 0, 0, 0))
    ImageDraw.Draw(bd).ellipse([0, 0, d - 1, d - 1], outline=border, width=int(1.6 * S))
    img = Image.alpha_composite(img, bd)
    return img.resize((diameter, diameter), Image.LANCZOS)


# ---------------------------------------------------------------- 精灵定义

PALETTE = {
    "panel_top": (30, 35, 50, 236),
    "panel_bottom": (20, 24, 35, 236),
    "panel_border": (60, 70, 96, 255),
    "button_top": (58, 88, 146, 255),
    "button_bottom": (36, 58, 102, 255),
    "button_border": (86, 126, 194, 255),
    "button_hi": (120, 160, 226, 60),
    "hover_top": (76, 116, 190, 255),
    "hover_bottom": (48, 78, 136, 255),
    "hover_border": (128, 172, 246, 255),
    "press_top": (26, 42, 74, 255),
    "press_bottom": (18, 30, 54, 255),
    "press_border": (64, 96, 152, 255),
    "disabled_top": (52, 56, 66, 255),
    "disabled_bottom": (38, 41, 49, 255),
    "disabled_border": (72, 76, 88, 255),
    "window_top": (28, 34, 50, 255),
    "window_bottom": (17, 21, 32, 255),
    "window_border": (66, 80, 112, 255),
    "title_top": (58, 80, 130, 255),
    "title_bottom": (34, 48, 82, 255),
    "slot_fill": (14, 18, 26, 210),
    "slot_border": (48, 57, 78, 255),
    "accent_top": (86, 150, 250, 255),
    "accent_bottom": (46, 104, 208, 255),
    "accent_border": (120, 176, 255, 255),
    "track_fill": (12, 16, 24, 230),
    "track_border": (44, 53, 72, 255),
    "text_bright": (226, 234, 252, 255),
    "select": (255, 212, 121, 255),
}


def build_sprites() -> list[dict]:
    p = PALETTE
    sprites: list[dict] = []

    def add(name: str, image: Image.Image, border: tuple[int, int, int, int] = (0, 0, 0, 0)) -> None:
        sprites.append({"name": name, "image": image, "border": border})

    # 纯白 1x1（纯色矩形控件用）
    white = Image.new("RGBA", (4, 4), (255, 255, 255, 255))
    add("white", white)

    add("panel", rounded_sprite(48, 48, 9, p["panel_top"], p["panel_bottom"], p["panel_border"], 1.5), (14, 14, 14, 14))
    add("tooltip", rounded_sprite(36, 36, 8, (18, 22, 32, 240), (12, 16, 24, 240), (54, 66, 94, 255), 1.4), (11, 11, 11, 11))
    add("divider", Image.new("RGBA", (8, 2), (62, 74, 100, 150)))

    add("button_normal", rounded_sprite(48, 48, 8, p["button_top"], p["button_bottom"], p["button_border"], 1.6, p["button_hi"]), (14, 14, 14, 14))
    add("button_hover", rounded_sprite(48, 48, 8, p["hover_top"], p["hover_bottom"], p["hover_border"], 1.8, (170, 205, 255, 90)), (14, 14, 14, 14))
    add("button_pressed", rounded_sprite(48, 48, 8, p["press_top"], p["press_bottom"], p["press_border"], 1.6, None, True), (14, 14, 14, 14))
    add("button_disabled", rounded_sprite(48, 48, 8, p["disabled_top"], p["disabled_bottom"], p["disabled_border"], 1.5), (14, 14, 14, 14))

    add("window", rounded_sprite(64, 64, 11, p["window_top"], p["window_bottom"], p["window_border"], 1.8, (110, 140, 196, 40)), (18, 18, 18, 18))
    add("windowTitle", rounded_sprite(64, 36, 10, p["title_top"], p["title_bottom"], (92, 120, 178, 255), 1.4, (150, 186, 246, 55)), (16, 10, 16, 10))
    add("windowShadow", shadow_sprite(72, 72, 14, 12, 90), (24, 24, 24, 24))

    close = rounded_sprite(22, 22, 6, (170, 74, 84, 255), (120, 46, 56, 255), (222, 128, 136, 255), 1.4)
    cd = ImageDraw.Draw(close)
    cd.line([(7, 7), (15, 15)], fill=(255, 236, 238, 255), width=2)
    cd.line([(15, 7), (7, 15)], fill=(255, 236, 238, 255), width=2)
    add("close", close)

    add("checkboxOff", rounded_sprite(22, 22, 5, p["track_fill"], (10, 13, 20, 240), p["track_border"], 1.4), (6, 6, 6, 6))
    on = rounded_sprite(22, 22, 5, p["accent_top"], p["accent_bottom"], p["accent_border"], 1.4)
    od = ImageDraw.Draw(on)
    od.line([(6, 11), (10, 15), (17, 7)], fill=(255, 255, 255, 255), width=2, joint="curve")
    add("checkboxOn", on, (6, 6, 6, 6))

    add("progressBg", rounded_sprite(24, 16, 6, (10, 13, 20, 235), (8, 10, 16, 235), p["track_border"], 1.4), (7, 7, 7, 7))
    add("progressFill", rounded_sprite(24, 16, 6, p["accent_top"], p["accent_bottom"], p["accent_border"], 1.2), (7, 7, 7, 7))

    add("sliderTrack", rounded_sprite(24, 12, 5, p["track_fill"], (8, 11, 17, 235), p["track_border"], 1.3), (6, 6, 6, 6))
    add("sliderFill", rounded_sprite(24, 12, 5, p["accent_top"], p["accent_bottom"], None, 1.0), (6, 6, 6, 6))
    add("sliderHandle", circle_sprite(18, p["text_bright"], (186, 202, 236, 255), (86, 120, 190, 255)))

    add("slot", rounded_sprite(40, 40, 6, p["slot_fill"], (10, 13, 19, 220), p["slot_border"], 1.4, None, True), (9, 9, 9, 9))
    selected = rounded_sprite(44, 44, 8, (255, 212, 121, 0), (255, 212, 121, 0), p["select"], 2.0)
    sd = ImageDraw.Draw(selected)
    sd.rounded_rectangle([1, 1, 42, 42], radius=8, outline=(255, 232, 176, 90), width=1)
    add("slotSelected", selected, (10, 10, 10, 10))

    # ---- 输入框 / 下拉框（存档界面、新建世界） ----
    add("textField",
        rounded_sprite(32, 32, 6, (12, 15, 22, 240), (9, 11, 17, 240), (54, 64, 88, 255), 1.3, None, True),
        (8, 8, 8, 8))
    add("textFieldFocused",
        rounded_sprite(32, 32, 6, (14, 18, 27, 245), (10, 13, 20, 245), (86, 150, 250, 255), 1.8, (120, 176, 255, 70)),
        (8, 8, 8, 8))
    add("arrowDown", triangle_sprite(14, (206, 216, 236, 255)))
    add("listItem",
        rounded_sprite(24, 24, 5, (255, 255, 255, 8), (255, 255, 255, 8), None, 1.0),
        (6, 6, 6, 6))
    add("listItemHover",
        rounded_sprite(24, 24, 5, (86, 150, 250, 90), (46, 104, 208, 90), (120, 176, 255, 140), 1.2),
        (6, 6, 6, 6))

    return sprites

# ---------------------------------------------------------------- 装箱与输出

def pack(sprites: list[dict], width: int = 512, gap: int = 2) -> tuple[Image.Image, int, int]:
    order = sorted(sprites, key=lambda s: (-s["image"].height, -s["image"].width))
    x = gap
    y = gap
    shelf_h = 0
    for sprite in order:
        w, h = sprite["image"].size
        if x + w + gap > width:
            x = gap
            y += shelf_h + gap
            shelf_h = 0
        sprite["x"] = x
        sprite["y"] = y
        x += w + gap
        shelf_h = max(shelf_h, h)

    height = y + shelf_h + gap
    # 高度取 4 的倍数，便于对齐
    height = (height + 3) // 4 * 4
    sheet = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    for sprite in sprites:
        sheet.paste(sprite["image"], (sprite["x"], sprite["y"]), sprite["image"])
    return sheet, width, height


def write_spirits_xml(sprites: list[dict], path: str) -> None:
    lines = [
        '<?xml version="1.0" encoding="utf-8"?>',
        '<ArrayOfSpirit xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema">',
    ]
    for sprite in sorted(sprites, key=lambda s: s["name"]):
        x, y = sprite["x"], sprite["y"]
        w, h = sprite["image"].size
        bl, bt, br, bb = sprite["border"]
        lines.append("  <Spirit>")
        lines.append(f"    <name>{escape(sprite['name'])}</name>")
        lines.append("    <rect>")
        for value in (x, y, x + w, y + h):
            lines.append(f"      <int>{value}</int>")
        lines.append("    </rect>")
        lines.append("    <border>")
        for value in (bl, bt, br, bb):
            lines.append(f"      <int>{value}</int>")
        lines.append("    </border>")
        lines.append("  </Spirit>")
    lines.append("</ArrayOfSpirit>")
    with open(path, "w", encoding="utf-8", newline="\n") as handle:
        handle.write("\n".join(lines) + "\n")


def main() -> None:
    root = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
    out_dir = os.path.join(root, "RequiredResources", "Resources", "atlas", "uiSkin")
    os.makedirs(out_dir, exist_ok=True)

    sprites = build_sprites()
    sheet, width, height = pack(sprites)

    texture_path = os.path.join(out_dir, "texture.png")
    spirits_path = os.path.join(out_dir, "spirits.xml")
    sheet.save(texture_path)
    write_spirits_xml(sprites, spirits_path)

    print(f"sheet {width}x{height}, {len(sprites)} sprites")
    for sprite in sorted(sprites, key=lambda s: s["name"]):
        w, h = sprite["image"].size
        print(f"  {sprite['name']:<16} {w:>3}x{h:<3} at ({sprite['x']},{sprite['y']}) border={sprite['border']}")
    print(f"written: {texture_path}")
    print(f"written: {spirits_path}")


if __name__ == "__main__":
    main()
