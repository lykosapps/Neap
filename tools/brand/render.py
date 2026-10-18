"""Draws Neap's mark and tray glyph and writes the SVG masters and every raster.

Run from the repository root: python tools/brand/render.py (needs Pillow).
The shapes are drawn directly, at 8x and scaled down, so no SVG renderer is needed.
"""
import math
from pathlib import Path

from PIL import Image, ImageDraw

SS = 8
INK, WHITE, VIOLET = "#08080A", "#F2F2F4", "#6D4AFF"
DARK_MOON, DARK_WAVE = "#F2F2F4", "#9C88FF"
LIGHT_MOON, LIGHT_WAVE = "#1A1A1E", "#6D4AFF"

# The mark, in a 120 box. Strokes are evened out (5.5 and 7) and the whole
# composition is centred by its bounding box.
MARK_MOON = (60, 44, 18, 5.5)  # cx, cy, r, stroke
MARK_WAVE = ([(24, 85), (34, 77), (44, 77), (54, 85), (64, 93), (74, 93), (84, 85), (94, 77), (98, 79), (98, 79)], 7)

# The tray glyph, in a 32 box: a solid moon over a wave. The 16px form is
# bolder so it stays readable.
GLYPH = {32: ((16, 12, 6), ([(5, 24), (9, 21), (12, 21), (16, 24), (20, 27), (23, 27), (27, 24)], 3.5)),
         16: ((16, 11, 7), ([(4, 25), (8, 22), (12, 22), (16, 25), (20, 28), (24, 28), (28, 25)], 5))}


def bezier(points):
    """Points of a chain of cubic segments given as p0, then c1, c2, p3 repeating."""
    out = []
    for i in range(0, len(points) - 3, 3):
        p0, p1, p2, p3 = points[i:i + 4]
        for s in range(61):
            t = s / 60
            u = 1 - t
            out.append(tuple(u**3 * a + 3 * u * u * t * b + 3 * u * t * t * c + t**3 * d
                             for a, b, c, d in zip(p0, p1, p2, p3)))
    return out


class Canvas:
    def __init__(self, size, box, dx=0.0, dy=0.0):
        """A transparent square of size px showing box units, with the drawing shifted by dx, dy units."""
        self.size, self.k = size * SS, size * SS / box
        self.dx, self.dy = dx, dy
        self.image = Image.new("RGBA", (self.size, self.size), (0, 0, 0, 0))

    def _layer(self, draw_fn, colour):
        mask = Image.new("L", (self.size, self.size), 0)
        draw_fn(ImageDraw.Draw(mask))
        layer = Image.new("RGBA", (self.size, self.size), colour)
        layer.putalpha(mask)
        self.image.alpha_composite(layer)

    def xy(self, x, y):
        return (x + self.dx) * self.k, (y + self.dy) * self.k

    def disc(self, colour, cx, cy, r):
        x, y = self.xy(cx, cy)
        self._layer(lambda d: d.ellipse([x - r * self.k, y - r * self.k, x + r * self.k, y + r * self.k], fill=255), colour)

    def ring(self, colour, cx, cy, r, width):
        x, y = self.xy(cx, cy)
        ro, ri = (r + width / 2) * self.k, (r - width / 2) * self.k
        def fn(d):
            d.ellipse([x - ro, y - ro, x + ro, y + ro], fill=255)
            d.ellipse([x - ri, y - ri, x + ri, y + ri], fill=0)
        self._layer(fn, colour)

    def right_half(self, colour, cx, cy, r):
        x, y = self.xy(cx, cy)
        self._layer(lambda d: d.pieslice([x - r * self.k, y - r * self.k, x + r * self.k, y + r * self.k], -90, 90, fill=255), colour)

    def stroke(self, colour, points, width):
        pts = [self.xy(*p) for p in bezier(points)]
        h = width * self.k / 2
        def fn(d):
            for x, y in pts:
                d.ellipse([x - h, y - h, x + h, y + h], fill=255)
        self._layer(fn, colour)

    def done(self, size):
        return self.image.resize((size, size), Image.LANCZOS)


def mark_offset():
    cx, cy, r, w = MARK_MOON
    pts = bezier(MARK_WAVE[0])
    bottom = max(y for _, y in pts) + MARK_WAVE[1] / 2
    top = cy - r - w / 2
    return 0.0, 60 - (top + bottom) / 2


def mark(size, tile=True):
    dy = mark_offset()[1]
    c = Canvas(size, 120, 0, dy)
    if tile:
        c._layer(lambda d: d.rounded_rectangle([0, 0, c.size - 1, c.size - 1], 28 * c.k, fill=255), INK)
    cx, cy, r, w = MARK_MOON
    c.right_half(WHITE, cx, cy, r)
    c.ring(WHITE, cx, cy, r, w)
    c.stroke(VIOLET, *MARK_WAVE)
    return c.done(size)


def glyph_canvas(size, moon, wave, scale):
    form = GLYPH[16 if size <= 16 else 32]
    (mx, my, mr), (wp, ww) = form
    ys = [y for _, y in bezier(wp)]
    top, bottom = my - mr, max(ys) + ww / 2
    box = 32 / scale
    c = Canvas(size, box, box / 2 - 16, box / 2 - (top + bottom) / 2)
    c.disc(moon, mx, my, mr)
    c.stroke(wave, wp, ww)
    return c


def tray(size, moon, wave):
    return glyph_canvas(size, moon, wave, 1).done(size)


def small_mark(size):
    """The glyph on the dark tile, for the window icon below 48px."""
    c = glyph_canvas(size, WHITE, "#8B6BFF", 1.1)
    base = Canvas(size, 120)
    base._layer(lambda d: d.rounded_rectangle([0, 0, base.size - 1, base.size - 1], 28 * base.k, fill=255), INK)
    out = base.done(size)
    out.alpha_composite(c.done(size))
    return out


def ico(path, images):
    biggest = max(images, key=lambda i: i.width)
    biggest.save(path, format="ICO", sizes=[(i.width, i.width) for i in images], append_images=[i for i in images if i is not biggest])


def svg_files(root):
    dy = mark_offset()[1]
    cx, cy, r, w = MARK_MOON
    p = MARK_WAVE[0]
    d = f"M{p[0][0]} {p[0][1] + dy:g}" + "".join(
        f"C{p[i][0]} {p[i][1] + dy:g} {p[i+1][0]} {p[i+1][1] + dy:g} {p[i+2][0]} {p[i+2][1] + dy:g}" for i in (1, 4, 7))
    (root / "neap-mark.svg").write_text(
        '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 120 120">'
        f'<rect width="120" height="120" rx="28" fill="{INK}"/>'
        f'<circle cx="{cx}" cy="{cy + dy:g}" r="{r}" fill="none" stroke="{WHITE}" stroke-width="{w}"/>'
        f'<path d="M{cx} {cy - r + dy:g}a{r} {r} 0 0 1 0 {2 * r}z" fill="{WHITE}"/>'
        f'<path d="{d}" fill="none" stroke="{VIOLET}" stroke-width="{MARK_WAVE[1]}" stroke-linecap="round"/></svg>\n')
    (mx, my, mr), (wp, ww) = GLYPH[32]
    gd = "M" + f"{wp[0][0]} {wp[0][1]}" + "".join(
        f"C{wp[i][0]} {wp[i][1]} {wp[i+1][0]} {wp[i+1][1]} {wp[i+2][0]} {wp[i+2][1]}" for i in (1, 4))
    for name, moon, wv in (("dark", DARK_MOON, DARK_WAVE), ("light", LIGHT_MOON, LIGHT_WAVE)):
        (root / f"neap-tray-{name}.svg").write_text(
            '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 32 32">'
            f'<circle cx="{mx}" cy="{my}" r="{mr}" fill="{moon}"/>'
            f'<path d="{gd}" fill="none" stroke="{wv}" stroke-width="{ww}" stroke-linecap="round"/></svg>\n')


def main():
    root = Path.cwd()
    svg_files(root / "assets" / "brand")
    assets = root / "src" / "Neap.Desktop" / "Assets"
    app_sizes = (16, 20, 24, 32, 48, 64, 128, 256)
    ico(assets / "app.ico", [small_mark(s) if s < 48 else mark(s) for s in app_sizes])
    mark(40).save(assets / "logo.png")
    mark(512).save(root / "docs" / "neap.png")
    mark(256).save(root / "packaging" / "linux" / "neap.png")
    tray_sizes = (16, 20, 24, 32, 40, 48)
    for name, moon, wave in (("dark", DARK_MOON, DARK_WAVE), ("light", LIGHT_MOON, LIGHT_WAVE)):
        ico(assets / f"tray-{name}.ico", [tray(s, moon, wave) for s in tray_sizes])


if __name__ == "__main__":
    main()
