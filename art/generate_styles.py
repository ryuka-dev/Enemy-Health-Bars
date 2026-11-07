"""Generates the bar sprites in Assets/Styles.

Every style has the same four layers, all the same size, so a bar is drawn by stacking them:

  back   sits behind everything, drawn as is
  fill   white mask, tinted at run time and clipped from the left
  frame  sits on top; white when the style tints it with the game's UI color

Where the fill starts and ends is decided by the drawing, so this script also writes
src/BarArt.generated.cs. That keeps the numbers in one place: the code never repeats them.

Run from the repository root: python art/generate_styles.py
"""
import math
import os
import random

from PIL import Image, ImageChops, ImageDraw, ImageFilter

OUT = os.path.join("Assets", "Styles")
GENERATED = os.path.join("src", "BarArt.generated.cs")
FINAL = 4       # stored pixels per canvas unit
WORK = 12       # drawn pixels per canvas unit, reduced for smooth edges

ROUNDS_COUNT = 10
FILL_RANGE = {}  # style name -> (fill start, fill end, width), in canvas units

WHITE = (255, 255, 255, 255)
SKULL = (240, 232, 160, 255)
INK = (14, 15, 15, 255)


def layer(w, h):
    return Image.new("RGBA", (w * WORK, h * WORK), (0, 0, 0, 0))


def mask(w, h):
    return Image.new("L", (w * WORK, h * WORK), 0)


def rounded(m, box, radius, value=255):
    x0, y0, x1, y1 = [v * WORK for v in box]
    ImageDraw.Draw(m).rounded_rectangle((x0, y0, x1 - 1, y1 - 1), radius * WORK, fill=value)


def ring(m, cx, cy, outer, inner):
    d = ImageDraw.Draw(m)
    d.ellipse([(cx - outer) * WORK, (cy - outer) * WORK, (cx + outer) * WORK, (cy + outer) * WORK], fill=255)
    d.ellipse([(cx - inner) * WORK, (cy - inner) * WORK, (cx + inner) * WORK, (cy + inner) * WORK], fill=0)


def solid(m, color):
    img = Image.new("RGBA", m.size, color)
    img.putalpha(ImageChops.multiply(m, Image.new("L", m.size, color[3])))
    return img


def save(style, name, img):
    w, h = img.size
    final = img.resize((w * FINAL // WORK, h * FINAL // WORK), Image.LANCZOS)
    os.makedirs(OUT, exist_ok=True)
    final.save(os.path.join(OUT, style + "." + name + ".png"), optimize=True)


def sulfur():
    """The HUD's own bar: thin rounded outline with a heavier bottom edge, flat colors."""
    w, h = 134, 17
    outer = mask(w, h)
    rounded(outer, (1, 1, w - 1, h - 1), 4)

    # The inner cut sits higher than the outer shape, which leaves the thick bottom edge.
    inner = mask(w, h)
    rounded(inner, (2.5, 2.5, w - 2.5, h - 4.5), 2.5)
    FILL_RANGE["Sulfur"] = (2.5, w - 2.5, w)

    save("Sulfur", "back", solid(inner, (10, 22, 19, 150)))
    save("Sulfur", "fill", solid(inner, WHITE))
    save("Sulfur", "frame", solid(ImageChops.subtract(outer, inner), WHITE))


def rounds():
    """The HUD's ammo gauge laid on its side: a row of rings, spent from the right."""
    w, h, count = 134, 21, ROUNDS_COUNT
    outer = mask(w, h)
    rounded(outer, (1, 4, w - 1, h - 1), 4)
    inner = mask(w, h)
    rounded(inner, (2.5, 5.5, w - 2.5, h - 2.5), 3)

    frame = ImageChops.subtract(outer, inner)
    for x in (10, w - 10):
        rounded(frame, (x - 1, 0, x + 1, 5), 0.8)

    fill = mask(w, h)
    rings = mask(w, h)
    pitch = (w - 6) / count
    FILL_RANGE["Rounds"] = (3, w - 3, w)
    for i in range(count):
        cx, cy = 3 + pitch * (i + 0.5), 12.5
        ring(rings, cx, cy, 4.8, 3.3)
        ImageDraw.Draw(fill).ellipse(
            [(cx - 3.3) * WORK, (cy - 3.3) * WORK, (cx + 3.3) * WORK, (cy + 3.3) * WORK], fill=255)

    save("Rounds", "back", solid(inner, (10, 22, 19, 150)))
    save("Rounds", "fill", solid(fill, WHITE))
    save("Rounds", "frame", solid(ImageChops.lighter(frame, rings), WHITE))


def blob(w, h, seed, inset):
    """A soft-edged run of liquid: circles along a capsule, blurred, then cut at a threshold."""
    rng = random.Random(seed)
    m = mask(w, h)
    d = ImageDraw.Draw(m)
    left, right = 5 + inset, w - 5 - inset
    top, bottom = 4 + inset, 15 - inset
    d.rounded_rectangle((left * WORK, top * WORK, right * WORK, bottom * WORK), (bottom - top) / 2 * WORK, fill=255)

    x = left
    while x < right:
        r = rng.uniform(2.2, 4.0) - inset * 0.6
        y = rng.choice((top + 0.5, bottom - 0.5))
        d.ellipse([(x - r) * WORK, (y - r) * WORK, (x + r) * WORK, (y + r) * WORK], fill=255)
        x += rng.uniform(5, 11)

    # Drips hanging under the bar, the way the game's blood decals run.
    for cx, length, width in ((26, 7.5, 2.4), (61, 4.5, 2.1), (104, 6.5, 2.3)):
        width -= inset * 0.45
        length -= inset * 0.7
        d.rounded_rectangle(((cx - width) * WORK, 11 * WORK, (cx + width) * WORK, (14 + length) * WORK),
                            width * WORK, fill=255)
        r = width + 0.8
        d.ellipse([(cx - r) * WORK, (14 + length - r) * WORK, (cx + r) * WORK, (14 + length + r) * WORK], fill=255)

    blurred = m.filter(ImageFilter.GaussianBlur(1.1 * WORK))
    return blurred.point(lambda v: 255 if v > 118 else 0)


def ichor():
    """Flat cartoon liquid, in the language of the world rather than of the HUD."""
    w, h = 142, 26
    outer = blob(w, h, 7, 0)
    inset = 1.5
    inner = blob(w, h, 7, inset)
    FILL_RANGE["Ichor"] = (5 + inset, w - 5 - inset, w)
    # The drips are part of the fill, so they run red, pale and empty along with the bar above them.
    save("Ichor", "back", solid(outer, INK))
    save("Ichor", "fill", solid(inner, WHITE))
    save("Ichor", "frame", layer(w, h))


def skull(img, cx, cy, size):
    """A plain cartoon skull; drawn for this mod."""
    d = ImageDraw.Draw(img)

    def px(*offsets):
        return [(c + o * size) * WORK for c, o in zip((cx, cy) * 2, offsets)]

    def r(value):
        return value * size * WORK

    d.rounded_rectangle(px(-8.6, -9.5, 8.6, 4.8), r(7.2), fill=INK)
    d.rounded_rectangle(px(-6.2, 1.5, 6.2, 10), r(2.6), fill=INK)
    d.rounded_rectangle(px(-7.2, -8.2, 7.2, 3.8), r(6.2), fill=SKULL)
    d.rounded_rectangle(px(-4.9, 1.4, 4.9, 8.7), r(1.6), fill=SKULL)
    d.ellipse(px(-5.2, -3.8, -1.1, 1.3), fill=INK)
    d.ellipse(px(1.1, -3.8, 5.2, 1.3), fill=INK)
    d.polygon(px(0, 1.2, -1.1, 3.2) + px(1.1, 3.2), fill=INK)
    for x in (-2.7, 0, 2.7):
        d.rounded_rectangle(px(x - 0.4, 5.2, x + 0.4, 8.7), r(0.4), fill=INK)


def bone():
    """A bone-colored capsule with a skull at the head, sized for the enemy's own palette."""
    # The capsule spans almost the whole width, so the bar stays centered over the enemy and the
    # skull only caps its end. A larger skull that sits outside the capsule pulls the bar sideways.
    w, h = 136, 22
    outer = mask(w, h)
    rounded(outer, (6, 5, w - 1, 18), 6.5)
    edge = mask(w, h)
    rounded(edge, (7.5, 6.5, w - 2.5, 16.5), 5)
    inner = mask(w, h)
    rounded(inner, (10, 8, w - 4.5, 15), 3.5)
    # The skull covers the start of the bar, so the fill is cut back to where it shows.
    fill_start = 15
    ImageDraw.Draw(inner).rectangle((0, 0, fill_start * WORK, h * WORK), fill=0)
    FILL_RANGE["Bone"] = (fill_start, w - 4.5, w)

    back = solid(outer, INK)
    bone_ring = solid(ImageChops.subtract(edge, inner), SKULL)
    frame = layer(w, h)
    frame.alpha_composite(bone_ring)
    skull(frame, 7.5, 11, 0.8)

    save("Bone", "back", back)
    save("Bone", "fill", solid(inner, WHITE))
    save("Bone", "frame", frame)


def classic():
    """The plain bar the mod started with."""
    w, h = 122, 11
    full = mask(w, h)
    rounded(full, (0, 0, w, h), 0)
    inner = mask(w, h)
    rounded(inner, (1, 1, w - 1, h - 1), 0)
    FILL_RANGE["Classic"] = (1, w - 1, w)
    save("Classic", "back", solid(full, (0, 0, 0, 166)))
    save("Classic", "fill", solid(inner, WHITE))
    save("Classic", "frame", layer(w, h))


def write_generated():
    lines = [
        "// Generated by art/generate_styles.py. Change that script, not this file.",
        "namespace EnemyHealthBars",
        "{",
        "    internal static class BarArt",
        "    {",
        "        public const float PixelsPerCanvasUnit = %df;" % FINAL,
        "        public const int RoundsCount = %d;" % ROUNDS_COUNT,
        "",
    ]
    for style, (start, end, width) in FILL_RANGE.items():
        lines.append("        public const float %sFillStart = %sf / %df;" % (style, start, width))
        lines.append("        public const float %sFillEnd = %sf / %df;" % (style, end, width))
    lines += ["    }", "}", ""]

    with open(GENERATED, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(lines))


if __name__ == "__main__":
    sulfur()
    rounds()
    ichor()
    bone()
    classic()
    write_generated()
    print("written to", OUT, "and", GENERATED)
