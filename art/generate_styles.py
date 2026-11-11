"""Generates the bar sprites in Assets/Styles.

Every style has the same four layers, all the same size, so a bar is drawn by stacking them:

  back   sits behind everything, drawn as is
  fill   white mask, tinted at run time and clipped from the left
  frame  sits on top; white when the style tints it with the game's UI color

Where the fill starts and ends is decided by the drawing, so this script also writes
src/BarArt.generated.cs. That keeps the numbers in one place: the code never repeats them.

Each style also has its team mark: mark1 to mark3, plus eyes for Bone (see the section below).

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


# ---------------------------------------------------------------------------------------------
# Team marks: what a bar shows while other players in a SULFUR Together session have their mark
# on the enemy. Each style has its own, one sprite per number of other players (1 to 3), drawn in
# the game's HUD orange. The skull in it is not drawn here: the mod cuts it out of the game's own
# portrait at run time, so no game art ships. This script only says where the skull goes.
#
# A mark is drawn in the bar's own coordinates (canvas units, y down, origin at the bar sprite's
# top left) and may reach outside the bar. MARKS records, per style, the mark sprite's box, the
# point it scales around, whether it sits behind the bar, and the skull's centre, height and
# angle (counter-clockwise, degrees; height 0 for none).

ORANGE = (250, 169, 64, 255)        # SulfurConstants.COLOR_SulfurYellow, the HUD's orange
ORANGE_LIGHT = (255, 196, 110, 255)
COPPER = (214, 120, 70, 255)
CASING_RIM = (200, 130, 45, 255)
CHAIN = (150, 140, 140, 255)
BACK_SHADES = (1.0, 0.78, 0.62)    # the front piece, then the ones behind it

MARKS = {}


def shade(color, factor):
    return tuple(min(255, round(c * factor)) for c in color[:3]) + (color[3],)


def mark_canvas(box):
    left, top, w, h = box
    return layer(w, h), (lambda x, y: ((x - left) * WORK, (y - top) * WORK))


def paste_rotated(canvas, piece, centre, angle):
    """Rotates a piece drawn at WORK scale about its centre and puts that centre at `centre` (WORK pixels)."""
    turned = piece.rotate(angle, resample=Image.BICUBIC, expand=True)
    canvas.alpha_composite(turned, (round(centre[0] - turned.width / 2), round(centre[1] - turned.height / 2)))


def turn(offset, angle):
    """An offset (y down) turned counter-clockwise on screen by `angle` degrees."""
    a = math.radians(angle)
    x, y = offset
    return x * math.cos(a) + y * math.sin(a), -x * math.sin(a) + y * math.cos(a)


def save_marks(style, frames, box, pivot, behind, skull_at=None):
    for count, img in enumerate(frames, start=1):
        save(style, "mark%d" % count, img)
    cx, cy, height, angle = skull_at if skull_at else (0, 0, 0, 0)
    MARKS[style] = dict(left=box[0], top=box[1], pivot=pivot, behind=behind,
                        skull=(cx, cy, height, angle))


def sulfur_marks():
    """Dog tags on a bead chain, hanging from under the bar's right end, like the game's dog tags item."""
    box = (90, 8, 50, 46)
    anchor = (122, 14)                    # where the chain leaves the bar
    tag_w, tag_h, hole_from_top = 19.5, 26, 3.9
    frames = []
    skull = None
    for count in (1, 2, 3):
        canvas, at = mark_canvas(box)
        for i in reversed(range(count)):
            color = shade(ORANGE, BACK_SHADES[i])
            angle = -4 + 12 * i
            centre = (121 - 8.5 * i, 34 - 0.5 * i)
            hole = turn((0, hole_from_top - tag_h / 2), angle)
            hole = (centre[0] + hole[0], centre[1] + hole[1])

            beads = ImageDraw.Draw(canvas)
            for step in range(7):
                t = step / 6
                x, y = at(anchor[0] + (hole[0] - anchor[0]) * t, anchor[1] + (hole[1] - anchor[1]) * t)
                beads.ellipse((x - 0.9 * WORK, y - 0.9 * WORK, x + 0.9 * WORK, y + 0.9 * WORK), fill=CHAIN)

            tag = mask(math.ceil(tag_w), tag_h)
            rounded(tag, (0, 0, tag_w, tag_h), 5.8)
            rim = mask(math.ceil(tag_w), tag_h)
            rounded(rim, (2.6, 2.6, tag_w - 2.6, tag_h - 2.6), 4)
            piece = solid(tag, color)
            piece.alpha_composite(solid(rim, shade(ORANGE_LIGHT, BACK_SHADES[i])))
            ImageDraw.Draw(piece).ellipse(((tag_w / 2 - 1.6) * WORK, (hole_from_top - 1.6) * WORK,
                                           (tag_w / 2 + 1.6) * WORK, (hole_from_top + 1.6) * WORK), fill=(0, 0, 0, 0))
            paste_rotated(canvas, piece, at(*centre), angle)
            if i == 0:
                s = turn((0, 2), angle)
                skull = (centre[0] + s[0], centre[1] + s[1], 9.75, angle)
        frames.append(canvas)
    save_marks("Sulfur", frames, box, anchor, False, skull)


def rounds_marks():
    """Cartridges standing past the bar's right end, one per player; the skull is stamped on the first."""
    w, h = 15, 35
    tip = h * 0.38
    first_x, pitch, top = 138, 17, 12.5 - h / 2 - 1
    box = (first_x - 1, top - 1, pitch * 2 + w + 2, h + 2)
    frames = []
    for count in (1, 2, 3):
        canvas, at = mark_canvas(box)
        d = ImageDraw.Draw(canvas)
        for i in range(count):
            f = 1.0 if i == 0 else 0.8
            x0, y0 = at(first_x + pitch * i, top)
            x1, y1 = at(first_x + pitch * i + w, top + h)
            d.ellipse((x0, y0, x1 - 1, y0 + tip * 2 * WORK), fill=shade(COPPER, f))
            d.rectangle((x0, y0 + tip * WORK, x1 - 1, y1 - 1), fill=shade(ORANGE, f))
            d.rectangle((x0, y0 + h * 0.9 * WORK, x1 - 1, y1 - 1), fill=shade(CASING_RIM, f))
        frames.append(canvas)
    # Smaller than the casing, so the brass shows on both sides of it.
    skull = (first_x + w / 2, top + (tip + h * 0.9) / 2, 6.0, 0)
    save_marks("Rounds", frames, box, (134, 12.5), False, skull)


def ichor_marks():
    """Orange drips running from under the bar's right end; the longest ends in the skull."""
    box = (98, 10, 46, 62)
    drips = ((128.2, 51.5), (115.8, 40.7), (108.3, 35))   # x, where the drip ends
    half = 2.9
    frames = []
    for count in (1, 2, 3):
        canvas, at = mark_canvas(box)
        d = ImageDraw.Draw(canvas)
        for x, end in drips[:count]:
            x0, y0 = at(x - half, 12)
            x1, y1 = at(x + half, end)
            d.rectangle((x0, y0, x1, y1), fill=ORANGE)
            cx, cy = at(x, end)
            r = half * 1.6 * WORK
            d.ellipse((cx - r, cy - r, cx + r, cy + r), fill=ORANGE)
        frames.append(canvas)
    # Behind the bar, so the drips come out from under it.
    save_marks("Ichor", frames, box, (124, 12), True, (128.2, 59.25, 19.5, 0))


def bone_marks():
    """Bones crossed behind the bar's skull: one, two crossed, three like an asterisk."""
    centre = (7.5, 11)
    box = (centre[0] - 25, centre[1] - 25, 50, 50)
    length, thick = 46, 9
    frames = []
    for count in (1, 2, 3):
        canvas, at = mark_canvas(box)
        for angle in (35, -35, 0)[:count]:
            piece = mask(length, thick)
            r = thick * 0.55 / 2
            rounded(piece, (r, thick * 0.3, length - r, thick * 0.7), 0)
            d = ImageDraw.Draw(piece)
            for x in (r, length - r):
                for y in (thick * 0.31, thick * 0.69):
                    d.ellipse(((x - r) * WORK, (y - r) * WORK, (x + r) * WORK, (y + r) * WORK), fill=255)
            paste_rotated(canvas, solid(piece, ORANGE), at(*centre), angle)
        frames.append(canvas)
    save_marks("Bone", frames, box, centre, True)

    # While marked, the bar's own skull looks out with orange eyes: the sockets of skull(), redrawn.
    w, h = 136, 22
    eyes = layer(w, h)
    d = ImageDraw.Draw(eyes)
    cx, cy, size = 7.5, 11, 0.8
    for x0, x1 in ((-5.2, -1.1), (1.1, 5.2)):
        d.ellipse(((cx + x0 * size) * WORK, (cy - 3.8 * size) * WORK,
                   (cx + x1 * size) * WORK, (cy + 1.3 * size) * WORK), fill=ORANGE)
    save("Bone", "eyes", eyes)


def classic_marks():
    """A plain orange block with the skull past the bar's right end, and a bar for each further player."""
    side, bar_w, gap = 22, 7, 2
    left, top = 124, 5.5 - side / 2
    box = (left, top, side + 2 * (gap + bar_w), side)
    frames = []
    for count in (1, 2, 3):
        canvas, at = mark_canvas(box)
        d = ImageDraw.Draw(canvas)
        x0, y0 = at(left, top)
        x1, y1 = at(left + side, top + side)
        d.rectangle((x0, y0, x1 - 1, y1 - 1), fill=ORANGE)
        for i in range(1, count):
            bx = left + side + gap + (i - 1) * (bar_w + gap)
            x0, y0 = at(bx, top)
            x1, y1 = at(bx + bar_w, top + side)
            d.rectangle((x0, y0, x1 - 1, y1 - 1), fill=shade(ORANGE, 0.85))
        frames.append(canvas)
    save_marks("Classic", frames, box, (122, 5.5), False, (left + side / 2, 5.5, 13.6, 0))


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
    lines.append("")
    lines.append("        // Team marks, in the bar's canvas units with y down from the bar's top left.")
    for style, m in MARKS.items():
        cx, cy, height, angle = m["skull"]
        for name, value in (("MarkLeft", m["left"]), ("MarkTop", m["top"]),
                            ("MarkPivotX", m["pivot"][0]), ("MarkPivotY", m["pivot"][1]),
                            ("SkullX", cx), ("SkullY", cy), ("SkullHeight", height), ("SkullAngle", angle)):
            lines.append("        public const float %s%s = %sf;" % (style, name, round(value, 3)))
        lines.append("        public const bool %sMarkBehind = %s;" % (style, "true" if m["behind"] else "false"))
    lines += ["    }", "}", ""]

    with open(GENERATED, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(lines))


if __name__ == "__main__":
    sulfur()
    rounds()
    ichor()
    bone()
    classic()
    sulfur_marks()
    rounds_marks()
    ichor_marks()
    bone_marks()
    classic_marks()
    write_generated()
    print("written to", OUT, "and", GENERATED)
