from __future__ import annotations

from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont


ROOT = Path("D:/unity\u8d44\u6599/\u96f7\u706b\u56fe\u7247\u8d44\u6e90")
SPECIAL = ROOT / "SPecialWorld"
NORMAL_OUT = ROOT / "NewNormalWorld"
PROJECT_OUT = Path("D:/UnityDemo/LeihuoDemo/Assets/Resources/NormalWorld/Environment")
STUDY_OUT = Path("D:/UnityDemo/LeihuoDemo/art_study")


NAMES = ["Blood.png", "Blood1.png", "Blood2.png"]
OUTPUT_SIZE = 512
PALETTE = {
    "line": (38, 50, 47, 220),
    "line_dark": (20, 27, 25, 238),
    "pencil": (58, 69, 64, 170),
    "red_thread": (116, 34, 31, 190),
    "teal": (42, 82, 82, 150),
    "paper": (112, 120, 104, 72),
}


def source_bbox(path: Path) -> tuple[int, int, int, int]:
    img = Image.open(path).convert("RGBA")
    b = img.getchannel("A").getbbox()
    if b:
        return b
    return (0, 0, img.width, img.height)


def draw_jitter_line(draw: ImageDraw.ImageDraw, pts: list[tuple[float, float]], fill, width: int, rng: np.random.Generator) -> None:
    jittered = []
    for x, y in pts:
        jittered.append((x + rng.normal(0, width * 0.38), y + rng.normal(0, width * 0.38)))
    draw.line(jittered, fill=fill, width=width, joint="curve")
    if width >= 3:
        draw.line(
            [(x + rng.normal(0, 1.4), y + rng.normal(0, 1.4)) for x, y in pts],
            fill=tuple(list(fill[:3]) + [max(20, fill[3] // 2)]),
            width=max(1, width // 2),
            joint="curve",
        )


def make_doodle(name: str) -> Image.Image:
    w = h = OUTPUT_SIZE
    scale = 3
    canvas = Image.new("RGBA", (w * scale, h * scale), (0, 0, 0, 0))
    d = ImageDraw.Draw(canvas, "RGBA")
    rng = np.random.default_rng(abs(hash(name)) % (2**32))
    margin = 54 * scale
    x0, y0, x1, y1 = margin, margin, w * scale - margin, h * scale - margin
    bw, bh = x1 - x0, y1 - y0

    def sx(v: float) -> float:
        return x0 + bw * v

    def sy(v: float) -> float:
        return y0 + bh * v

    # Faint paper/old wall scuffs to make the decoration sit in NormalWorld.
    for _ in range(max(8, (w * h) // 180000)):
        cx = int(rng.integers(max(0, x0), min(canvas.width, x1)))
        cy = int(rng.integers(max(0, y0), min(canvas.height, y1)))
        rx = int(rng.integers(max(18, bw // 40), max(22, bw // 10)))
        ry = int(rng.integers(max(10, bh // 50), max(18, bh // 8)))
        d.ellipse([cx - rx, cy - ry, cx + rx, cy + ry], fill=PALETTE["paper"])

    if name == "Blood.png":
        # Clustered investigation-like graffiti: circles, arrows, branching pencil lines.
        anchors = [(sx(0.48), sy(0.38)), (sx(0.25), sy(0.58)), (sx(0.62), sy(0.70)), (sx(0.72), sy(0.26))]
        for idx, (cx, cy) in enumerate(anchors):
            r = int(min(bw, bh) * (0.055 + idx * 0.008))
            d.ellipse([cx - r, cy - r, cx + r, cy + r], outline=PALETTE["line_dark"], width=5 * scale)
            d.arc([cx - r * 1.35, cy - r * 1.2, cx + r * 1.35, cy + r * 1.2], 20, 330, fill=PALETTE["pencil"], width=3 * scale)
        center = anchors[0]
        for pt in anchors[1:]:
            draw_jitter_line(d, [center, pt], PALETTE["red_thread"], 4 * scale, rng)
        for _ in range(12):
            start = anchors[int(rng.integers(0, len(anchors)))]
            pts = [start]
            for _ in range(int(rng.integers(2, 5))):
                pts.append((pts[-1][0] + rng.normal(0, bw * 0.13), pts[-1][1] + rng.normal(0, bh * 0.11)))
            draw_jitter_line(d, pts, PALETTE["line"], int(rng.integers(2, 4)) * scale, rng)
        for _ in range(10):
            x, y = sx(rng.random()), sy(rng.random())
            r = int(rng.integers(4, 11)) * scale
            d.line([(x - r, y - r), (x + r, y + r)], fill=PALETTE["pencil"], width=3 * scale)
            d.line([(x - r, y + r), (x + r, y - r)], fill=PALETTE["pencil"], width=3 * scale)

    elif name == "Blood1.png":
        # Long background scrawl for the wide blood smear slot.
        baseline = sy(0.56)
        for i in range(8):
            x_start = sx(0.04 + i * 0.12)
            pts = []
            for step in range(7):
                x = x_start + bw * 0.018 * step
                y = baseline + np.sin(step * 0.8 + i) * bh * 0.03 + rng.normal(0, bh * 0.015)
                pts.append((x, y))
            draw_jitter_line(d, pts, PALETTE["line"], int(rng.integers(2, 4)) * scale, rng)
        for _ in range(11):
            x = sx(rng.uniform(0.04, 0.96))
            y = sy(rng.uniform(0.18, 0.82))
            length = bw * rng.uniform(0.025, 0.09)
            draw_jitter_line(
                d,
                [(x, y), (x + length, y + rng.normal(0, bh * 0.035))],
                PALETTE["red_thread"] if rng.random() < 0.32 else PALETTE["pencil"],
                int(rng.integers(2, 4)) * scale,
                rng,
            )
        for _ in range(16):
            x = sx(rng.uniform(0.03, 0.98))
            y = sy(rng.uniform(0.22, 0.78))
            d.rectangle([x, y, x + rng.uniform(8, 34) * scale, y + rng.uniform(3, 9) * scale], fill=PALETTE["teal"])
        for i in range(5):
            x = sx(0.10 + i * 0.18)
            y = sy(0.33 + (i % 2) * 0.24)
            r = int(bh * 0.035)
            d.ellipse([x - r, y - r, x + r, y + r], outline=PALETTE["line_dark"], width=3 * scale)

    else:
        # Diagonal scratchy marks corresponding to Blood2's decorative slot.
        for i in range(5):
            start = (sx(0.10 + i * 0.16), sy(0.18 + (i % 2) * 0.14))
            pts = [start]
            for _ in range(4):
                pts.append((pts[-1][0] + bw * rng.uniform(0.04, 0.13), pts[-1][1] + bh * rng.uniform(0.02, 0.13)))
            draw_jitter_line(d, pts, PALETTE["line_dark"], int(rng.integers(2, 4)) * scale, rng)
        for _ in range(9):
            cx = sx(rng.uniform(0.08, 0.92))
            cy = sy(rng.uniform(0.12, 0.82))
            r = int(rng.integers(8, 22)) * scale
            if rng.random() < 0.45:
                d.arc([cx - r, cy - r, cx + r, cy + r], 0, 300, fill=PALETTE["red_thread"], width=3 * scale)
            else:
                d.line([(cx - r, cy), (cx + r, cy)], fill=PALETTE["pencil"], width=3 * scale)
                d.line([(cx, cy - r), (cx, cy + r)], fill=PALETTE["pencil"], width=3 * scale)
        for _ in range(30):
            x = sx(rng.uniform(0.04, 0.96))
            y = sy(rng.uniform(0.08, 0.90))
            d.point((x, y), fill=PALETTE["line"])

    # Add hand-drawn softness, then downsample to crisp antialiased transparent PNG.
    canvas = canvas.filter(ImageFilter.GaussianBlur(0.18 * scale))
    out = canvas.resize((w, h), Image.Resampling.LANCZOS)
    arr = np.array(out)
    alpha = arr[..., 3]
    alpha = np.where(alpha < 8, 0, np.clip(alpha.astype(np.float32) * 1.55, 0, 255)).astype(np.uint8)
    arr[..., 3] = alpha
    return Image.fromarray(arr, "RGBA")


def make_contact(paths: list[Path], out: Path) -> None:
    font = ImageFont.load_default()
    cell_w, cell_h, cols, header = 220, 220, 3, 28
    rows = max(1, (len(paths) + cols - 1) // cols)
    sheet = Image.new("RGBA", (cell_w * cols, header + cell_h * rows), (36, 38, 42, 255))
    d = ImageDraw.Draw(sheet)
    d.text((10, 8), "NormalWorld Blood doodle counterparts", fill=(255, 255, 255, 255), font=font)
    for i, p in enumerate(paths):
        im = Image.open(p).convert("RGBA")
        b = im.getchannel("A").getbbox()
        if b:
            im = im.crop(b)
        im.thumbnail((cell_w - 12, cell_h - 30), Image.Resampling.LANCZOS)
        cell = Image.new("RGBA", (cell_w, cell_h), (236, 236, 236, 255))
        cd = ImageDraw.Draw(cell)
        step = 12
        for y in range(0, cell_h - 24, step):
            for x in range(0, cell_w, step):
                col = (211, 211, 211, 255) if (x // step + y // step) % 2 else (246, 246, 246, 255)
                cd.rectangle([x, y, x + step - 1, y + step - 1], fill=col)
        cell.alpha_composite(im, ((cell_w - im.width) // 2, (cell_h - 24 - im.height) // 2))
        cd.rectangle([0, cell_h - 24, cell_w, cell_h], fill=(28, 30, 34, 255))
        cd.text((6, cell_h - 18), p.name, fill=(255, 255, 255, 255), font=font)
        sheet.alpha_composite(cell, ((i % cols) * cell_w, header + (i // cols) * cell_h))
    sheet.convert("RGB").save(out, quality=95)


def main() -> None:
    NORMAL_OUT.mkdir(exist_ok=True)
    PROJECT_OUT.mkdir(parents=True, exist_ok=True)
    STUDY_OUT.mkdir(exist_ok=True)
    made = []
    for name in NAMES:
        img = make_doodle(name)
        ext_path = NORMAL_OUT / name
        project_path = PROJECT_OUT / name
        img.save(ext_path)
        img.save(project_path)
        made.append(ext_path)
    make_contact(made, STUDY_OUT / "normal_blood_doodles_contact.jpg")
    print("Created:")
    for p in made:
        print(p)
    print(f"Project folder: {PROJECT_OUT}")


if __name__ == "__main__":
    main()
