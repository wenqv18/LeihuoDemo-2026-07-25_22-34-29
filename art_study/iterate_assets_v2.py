from __future__ import annotations

from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageEnhance, ImageFilter, ImageFont, ImageOps


ROOT = Path("D:/unity\u8d44\u6599/\u96f7\u706b\u56fe\u7247\u8d44\u6e90")
SPECIAL_SRC = ROOT / "SPecialWorld"
NORMAL_OUT = ROOT / "NewNormalWorld"
SPECIAL_OUT = ROOT / "NewSpecialWorld"
STUDY_OUT = Path("D:/UnityDemo/LeihuoDemo/art_study")

EXTS = {".png", ".jpg", ".jpeg", ".webp", ".tga"}

SPECIAL_TO_NORMAL = {
    "books": "book",
    "cup": "cup",
    "cup2": "cup",
    "leftbookshelf": "leftbookshelf",
    "rightbookshelf": "rightbookshelf",
    "table": "table",
    "water1": "water",
    "window": "window",
    "window2": "window",
    "window3": "window",
}

PAPER = {"map", "memo_01", "memo_02", "memo_03", "memo_04"}
BACKGROUND = {"background", "uibackground", "mainscence", "2d unity项目设置与效果"}
UI = {"logo", "ui01", "ui02", "ui03", "ui04"}
SOFT_EFFECT = {"blood", "blood1", "blood2"}


def assets(folder: Path) -> list[Path]:
    return [
        p
        for p in folder.iterdir()
        if p.is_file()
        and p.suffix.lower() in EXTS
        and "player" not in p.name.lower()
    ]


def key(path: Path | str) -> str:
    return Path(path).stem.lower().replace(" ", "")


def rgba(path: Path) -> Image.Image:
    return Image.open(path).convert("RGBA")


def bbox(img: Image.Image) -> tuple[int, int, int, int] | None:
    return img.getchannel("A").getbbox()


def luma(rgb: np.ndarray) -> np.ndarray:
    arr = rgb.astype(np.float32)
    return 0.2126 * arr[..., 0] + 0.7152 * arr[..., 1] + 0.0722 * arr[..., 2]


def alpha_stats(alpha: np.ndarray) -> tuple[float, float]:
    total = alpha.size
    return (
        float(np.count_nonzero(alpha < 255)) / total,
        float(np.count_nonzero((alpha > 0) & (alpha < 255))) / total,
    )


def harden_alpha(alpha: np.ndarray, k: str, special: bool = False) -> np.ndarray:
    if alpha.min() == 255 and alpha.max() == 255:
        return alpha
    if k in SOFT_EFFECT:
        low, high, gamma = 4.0, 252.0, 0.92
    elif special:
        low, high, gamma = 30.0, 202.0, 0.58
    else:
        low, high, gamma = 30.0, 210.0, 0.64
    a = alpha.astype(np.float32)
    t = np.clip((a - low) / (high - low), 0.0, 1.0)
    t = np.power(t, gamma)
    out = np.rint(t * 255).astype(np.uint8)
    out[a <= low] = 0
    out[a >= high] = 255
    return out


def color_bleed(rgb: np.ndarray, alpha: np.ndarray, iterations: int = 24) -> np.ndarray:
    out = rgb.astype(np.float32).copy()
    valid = alpha > 160
    if not np.any(valid):
        valid = alpha > 20
    h, w = alpha.shape
    for _ in range(iterations):
        missing = ~valid
        if not np.any(missing):
            break
        sums = np.zeros_like(out)
        counts = np.zeros((h, w), dtype=np.float32)
        for dy, dx in [(-1, 0), (1, 0), (0, -1), (0, 1), (-1, -1), (-1, 1), (1, -1), (1, 1)]:
            ys = slice(max(0, dy), min(h, h + dy))
            xs = slice(max(0, dx), min(w, w + dx))
            yd = slice(max(0, -dy), min(h, h - dy))
            xd = slice(max(0, -dx), min(w, w - dx))
            v = valid[ys, xs]
            sums[yd, xd] += out[ys, xs] * v[..., None]
            counts[yd, xd] += v
        fill = missing & (counts > 0)
        out[fill] = sums[fill] / counts[fill, None]
        valid[fill] = True
    return np.clip(out, 0, 255).astype(np.uint8)


def target_luma(rgb: Image.Image, alpha: np.ndarray, target: float) -> Image.Image:
    arr = np.array(rgb.convert("RGB"))
    mask = alpha > 24
    if not np.any(mask):
        return rgb
    med = float(np.median(luma(arr)[mask]))
    factor = np.clip(target / max(med, 1.0), 0.70, 1.38)
    return ImageEnhance.Brightness(rgb).enhance(float(factor))


def tint(rgb: Image.Image, alpha: np.ndarray, color: tuple[int, int, int], strength: float) -> Image.Image:
    arr = np.array(rgb.convert("RGB")).astype(np.float32)
    lum = luma(arr) / 255.0
    weight = strength * np.clip(1.08 - np.abs(lum - 0.48) * 1.55, 0.0, 1.0)
    weight *= (alpha.astype(np.float32) / 255.0)
    c = np.array(color, dtype=np.float32)
    out = arr * (1 - weight[..., None]) + c * weight[..., None]
    return Image.fromarray(np.clip(out, 0, 255).astype(np.uint8), "RGB")


def reinforce_lines(rgb: Image.Image, alpha: np.ndarray, special: bool = False) -> Image.Image:
    gray = ImageOps.grayscale(rgb)
    edges = gray.filter(ImageFilter.FIND_EDGES).filter(ImageFilter.MaxFilter(3))
    edge = np.array(edges).astype(np.float32)
    arr = np.array(rgb.convert("RGB")).astype(np.float32)
    lum = luma(arr)
    mask = (alpha > 25) & ((edge > 24) | (lum < (92 if special else 102)))
    if special:
        color = np.array([18, 20, 18], dtype=np.float32)
        amount = 0.48
    else:
        color = np.array([38, 43, 38], dtype=np.float32)
        amount = 0.34
    arr[mask] = arr[mask] * (1 - amount) + color * amount
    return Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), "RGB")


def reinforce_key_lines(rgb: Image.Image, alpha: np.ndarray, special: bool = False) -> Image.Image:
    arr = np.array(rgb.convert("RGB")).astype(np.float32)
    lum = luma(arr)
    mask = (alpha > 25) & (lum < (78 if special else 88))
    color = np.array([20, 19, 16] if special else [44, 48, 42], dtype=np.float32)
    amount = 0.38 if special else 0.24
    arr[mask] = arr[mask] * (1 - amount) + color * amount
    return Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), "RGB")


def outline(img: Image.Image, special: bool = False) -> Image.Image:
    if bbox(img) is None:
        return img
    alpha = img.getchannel("A")
    a = np.array(alpha).astype(np.int16)
    dil = np.array(alpha.filter(ImageFilter.MaxFilter(3))).astype(np.int16)
    stroke = np.clip(dil - a, 0, 120 if not special else 160).astype(np.uint8)
    if not np.any(stroke):
        return img
    layer = Image.new("RGBA", img.size, ((36, 42, 36) if not special else (18, 15, 13)) + (0,))
    layer.putalpha(Image.fromarray(stroke, "L"))
    return Image.alpha_composite(layer, img)


def process_normal(src: Path) -> Image.Image:
    k = key(src)
    img = rgba(src)
    if k in BACKGROUND or src.suffix.lower() in {".jpg", ".jpeg"}:
        rgb = ImageEnhance.Color(img.convert("RGB")).enhance(0.92)
        rgb = ImageEnhance.Contrast(rgb).enhance(1.02)
        return rgb.convert("RGBA")

    arr = np.array(img)
    alpha = harden_alpha(arr[..., 3], k, special=False)
    rgb = Image.fromarray(arr[..., :3], "RGB")
    rgb = ImageEnhance.Color(rgb).enhance(0.82)
    target = 138.0
    if k in PAPER:
        target = 156.0
    elif "window" in k:
        target = 184.0
    elif k in {"shelf", "rightbookshelf"}:
        target = 126.0
    rgb = target_luma(rgb, alpha, target)
    rgb = tint(rgb, alpha, (148, 155, 134) if k not in PAPER else (196, 192, 166), 0.075)
    if k in PAPER:
        rgb = ImageEnhance.Contrast(rgb).enhance(0.98)
        rgb = Image.blend(rgb, ImageOps.posterize(rgb, 6), 0.04)
        rgb = reinforce_key_lines(rgb, alpha, special=False)
        rgb = rgb.filter(ImageFilter.UnsharpMask(radius=0.45, percent=90, threshold=5))
    else:
        rgb = ImageEnhance.Contrast(rgb).enhance(1.06)
        rgb = Image.blend(rgb, ImageOps.posterize(rgb, 6), 0.12)
        rgb = reinforce_lines(rgb, alpha, special=False)
        rgb = rgb.filter(ImageFilter.UnsharpMask(radius=0.55, percent=145, threshold=2))
    rgb_arr = color_bleed(np.array(rgb), alpha)
    out = Image.fromarray(np.dstack([rgb_arr, alpha]).astype(np.uint8), "RGBA")
    return outline(out, special=False)


def add_cracks(draw: ImageDraw.ImageDraw, rng: np.random.Generator, box: tuple[int, int, int, int], count: int, color: tuple[int, int, int, int]) -> None:
    x0, y0, x1, y1 = box
    for _ in range(count):
        x = int(rng.integers(x0, x1))
        y = int(rng.integers(y0, y1))
        pts = [(x, y)]
        angle = float(rng.uniform(-1.5, 1.5))
        for _ in range(int(rng.integers(2, 5))):
            x += int(np.cos(angle) * rng.integers(8, 34))
            y += int(np.sin(angle) * rng.integers(8, 34))
            pts.append((x, y))
            angle += float(rng.uniform(-0.9, 0.9))
        draw.line(pts, fill=color, width=1)


def special_from_normal(normal: Image.Image, name: str, old_special: Image.Image | None = None) -> Image.Image:
    k = key(name)
    arr = np.array(normal.convert("RGBA"))
    alpha = harden_alpha(arr[..., 3], k, special=True)
    rgb = Image.fromarray(arr[..., :3], "RGB")

    if k in PAPER or "memo" in k or "map" in k:
        target = 104.0
        base_tint = (132, 111, 68)
        tint_strength = 0.20
        sat = 0.48
    elif "window" in k:
        target = 70.0
        base_tint = (38, 66, 61)
        tint_strength = 0.24
        sat = 0.50
    else:
        target = 82.0
        base_tint = (36, 92, 82)
        tint_strength = 0.27
        sat = 0.54

    rgb = ImageEnhance.Color(rgb).enhance(sat)
    rgb = target_luma(rgb, alpha, target)
    rgb = tint(rgb, alpha, base_tint, tint_strength)
    rgb = ImageEnhance.Contrast(rgb).enhance(1.08 if k not in PAPER else 1.00)
    rgb = Image.blend(rgb, ImageOps.posterize(rgb, 5), 0.20 if k not in PAPER else 0.10)
    rgb = reinforce_key_lines(rgb, alpha, special=True) if k in PAPER else reinforce_lines(rgb, alpha, special=True)

    rgb_arr = np.array(rgb).astype(np.float32)
    rng = np.random.default_rng(abs(hash(name)) % (2**32))
    noise = rng.normal(0, 1, alpha.shape).astype(np.float32)
    stain = np.clip(0.86 + noise * 0.075, 0.58, 1.08)
    rgb_arr = np.where((alpha > 20)[..., None], rgb_arr * stain[..., None], rgb_arr)

    rgb_arr = color_bleed(np.clip(rgb_arr, 0, 255).astype(np.uint8), alpha)
    out = Image.fromarray(np.dstack([rgb_arr, alpha]).astype(np.uint8), "RGBA")
    box = bbox(out)
    if box:
        overlay = Image.new("RGBA", out.size, (0, 0, 0, 0))
        d = ImageDraw.Draw(overlay)
        x0, y0, x1, y1 = box
        w, h = x1 - x0, y1 - y0
        red = (108, 18, 12, 135)
        rust = (112, 62, 30, 120)
        dark = (14, 12, 10, 88)
        count = max(4, min(26, (w * h) // 24000))
        add_cracks(d, rng, box, count, dark)
        for _ in range(max(5, count)):
            x = int(rng.integers(x0, x1))
            y = int(rng.integers(y0, y1))
            r = int(rng.integers(2, max(4, min(w, h) // 18)))
            d.ellipse([x - r, y - r, x + r, y + r], fill=rust if rng.random() > 0.35 else red)
        for _ in range(max(2, count // 2)):
            x = int(rng.integers(x0, x1))
            y = int(rng.integers(y0, y1))
            d.line(
                [(x, y), (x + int(rng.integers(-8, 9)), y + int(rng.integers(12, 38)))],
                fill=(78, 22, 14, 95),
                width=1,
            )
        if k in {"cup", "cup2", "water1", "hanginglamp"}:
            glow = Image.new("RGBA", out.size, (0, 0, 0, 0))
            gd = ImageDraw.Draw(glow)
            cx, cy = x0 + w // 2, y0 + int(h * 0.66)
            rr = max(6, min(w, h) // 4)
            gd.ellipse([cx - rr, cy - rr, cx + rr, cy + rr], fill=(126, 196, 82, 88))
            glow = glow.filter(ImageFilter.GaussianBlur(max(2, rr // 4)))
            glow.putalpha(Image.fromarray(np.minimum(np.array(glow.getchannel("A")), alpha), "L"))
            out = Image.alpha_composite(out, glow)
        if "window" in k:
            sx = x0 + int(w * 0.50)
            sy = y0 + int(h * 0.30)
            d.ellipse([sx - w * 0.055, sy, sx + w * 0.055, sy + h * 0.13], fill=(8, 8, 9, 190))
            d.rectangle([sx - w * 0.065, sy + h * 0.10, sx + w * 0.065, sy + h * 0.55], fill=(8, 8, 9, 175))
            d.ellipse([sx - w * 0.018, sy + h * 0.045, sx, sy + h * 0.065], fill=(120, 11, 18, 210))
            d.ellipse([sx + w * 0.018, sy + h * 0.045, sx + w * 0.036, sy + h * 0.065], fill=(120, 11, 18, 210))
        overlay.putalpha(Image.fromarray(np.minimum(np.array(overlay.getchannel("A")), alpha), "L"))
        out = Image.alpha_composite(out, overlay)

    out = outline(out, special=True)
    return out


def process_special(src: Path, normal_lookup: dict[str, Image.Image]) -> Image.Image:
    k = key(src)
    if k in BACKGROUND or src.suffix.lower() in {".jpg", ".jpeg"}:
        img = rgba(src)
        rgb = ImageEnhance.Color(img.convert("RGB")).enhance(0.78)
        rgb = ImageEnhance.Contrast(rgb).enhance(1.08)
        return rgb.convert("RGBA")
    if k in SOFT_EFFECT:
        img = rgba(src)
        arr = np.array(img)
        alpha = harden_alpha(arr[..., 3], k, special=True)
        rgb = Image.fromarray(arr[..., :3], "RGB")
        rgb = ImageEnhance.Color(rgb).enhance(0.78)
        rgb = ImageEnhance.Contrast(rgb).enhance(1.06)
        return Image.fromarray(np.dstack([np.array(rgb), alpha]).astype(np.uint8), "RGBA")

    nk = SPECIAL_TO_NORMAL.get(k, k)
    if nk in normal_lookup:
        return special_from_normal(normal_lookup[nk], src.name, rgba(src))

    img = process_normal(src)
    return special_from_normal(img, src.name, rgba(src))


def save_contact(folder: Path, title: str, dst: Path, cols: int = 5) -> None:
    files = assets(folder)
    font = ImageFont.load_default()
    cell_w, cell_h, header = 184, 144, 34
    rows = max(1, (len(files) + cols - 1) // cols)
    sheet = Image.new("RGBA", (cols * cell_w, header + rows * cell_h), (35, 37, 41, 255))
    d = ImageDraw.Draw(sheet)
    d.text((10, 10), title, fill=(255, 255, 255, 255), font=font)
    for idx, path in enumerate(files):
        im = rgba(path)
        b = bbox(im)
        if b and path.suffix.lower() == ".png":
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
        cd.text((6, cell_h - 18), path.name[:28], fill=(255, 255, 255, 255), font=font)
        sheet.alpha_composite(cell, ((idx % cols) * cell_w, header + (idx // cols) * cell_h))
    sheet.convert("RGB").save(dst, quality=95)


def save_asset(img: Image.Image, dst: Path) -> None:
    if dst.suffix.lower() in {".jpg", ".jpeg"}:
        img.convert("RGB").save(dst, quality=95)
    else:
        img.save(dst)


def save_pair_check(normal_lookup: dict[str, Image.Image]) -> None:
    rows = ["special\tnormal_ref\tn_size\ts_size\tn_bbox\ts_bbox\tw_ratio\th_ratio\tbottom_delta"]
    pairs = []
    for sp in assets(SPECIAL_OUT):
        k = key(sp)
        nk = SPECIAL_TO_NORMAL.get(k, k)
        npath = ROOT / f"{nk}.png"
        if not npath.exists():
            name_map = {"leftbookshelf": "LeftBookshelf.png", "rightbookshelf": "RightBookshelf.png"}
            npath = ROOT / name_map.get(nk, f"{nk}.png")
        if npath.exists() or nk in normal_lookup:
            pairs.append((nk, npath if npath.exists() else None, sp))

    font = ImageFont.load_default()
    cell_w, cell_h, gap, header = 190, 150, 16, 56
    sheet = Image.new("RGBA", (cell_w * 2 + gap, header + max(1, len(pairs)) * cell_h), (36, 38, 42, 255))
    d = ImageDraw.Draw(sheet)
    d.text((10, 8), "V2 Normal / Special Shape Check", fill=(255, 255, 255, 255), font=font)
    d.text((58, 32), "Normal", fill=(200, 220, 255, 255), font=font)
    d.text((cell_w + gap + 58, 32), "Special", fill=(255, 205, 205, 255), font=font)

    def thumb(img: Image.Image, label: str) -> tuple[Image.Image, tuple[int, int]]:
        b = bbox(img)
        bb = (img.width, img.height)
        crop = img
        if b:
            bb = (b[2] - b[0], b[3] - b[1])
            crop = img.crop(b)
        crop.thumbnail((cell_w - 12, cell_h - 34), Image.Resampling.LANCZOS)
        cell = Image.new("RGBA", (cell_w, cell_h), (236, 236, 236, 255))
        cd = ImageDraw.Draw(cell)
        step = 12
        for y in range(0, cell_h - 24, step):
            for x in range(0, cell_w, step):
                col = (211, 211, 211, 255) if (x // step + y // step) % 2 else (246, 246, 246, 255)
                cd.rectangle([x, y, x + step - 1, y + step - 1], fill=col)
        cell.alpha_composite(crop, ((cell_w - crop.width) // 2, (cell_h - 24 - crop.height) // 2))
        cd.rectangle([0, cell_h - 24, cell_w, cell_h], fill=(28, 30, 34, 255))
        cd.text((6, cell_h - 18), label[:28], fill=(255, 255, 255, 255), font=font)
        return cell, bb

    for idx, (nk, npath, sp) in enumerate(pairs):
        nimg = process_normal(npath) if npath else normal_lookup[nk]
        simg = rgba(sp)
        y = header + idx * cell_h
        tn, nb = thumb(nimg.copy(), npath.name if npath else nk)
        ts, sb = thumb(simg.copy(), sp.name)
        sheet.alpha_composite(tn, (0, y))
        sheet.alpha_composite(ts, (cell_w + gap, y))
        nbx, sbx = bbox(nimg), bbox(simg)
        wr = round(sb[0] / nb[0], 3) if nb[0] else 0
        hr = round(sb[1] / nb[1], 3) if nb[1] else 0
        bd = (sbx[3] - nbx[3]) if nbx and sbx else 0
        rows.append(f"{sp.name}\t{npath.name if npath else nk}\t{nimg.size}\t{simg.size}\t{nb}\t{sb}\t{wr}\t{hr}\t{bd}")

    sheet.convert("RGB").save(STUDY_OUT / "v2_pair_contact.jpg", quality=95)
    (STUDY_OUT / "v2_pair_metrics.tsv").write_text("\n".join(rows), encoding="utf-8")


def main() -> None:
    NORMAL_OUT.mkdir(exist_ok=True)
    SPECIAL_OUT.mkdir(exist_ok=True)
    STUDY_OUT.mkdir(exist_ok=True)

    report = ["world\tname\tw\th\tbefore_soft\tafter_soft"]
    normal_lookup: dict[str, Image.Image] = {}
    for src in assets(ROOT):
        out = process_normal(src)
        save_asset(out, NORMAL_OUT / src.name)
        normal_lookup[key(src)] = out
        before = alpha_stats(np.array(rgba(src).getchannel("A")))[1]
        after = alpha_stats(np.array(out.getchannel("A")))[1]
        report.append(f"Normal\t{src.name}\t{out.width}\t{out.height}\t{before}\t{after}")

    for src in assets(SPECIAL_SRC):
        out = process_special(src, normal_lookup)
        save_asset(out, SPECIAL_OUT / src.name)
        before = alpha_stats(np.array(rgba(src).getchannel("A")))[1]
        after = alpha_stats(np.array(out.getchannel("A")))[1]
        report.append(f"Special\t{src.name}\t{out.width}\t{out.height}\t{before}\t{after}")

    (STUDY_OUT / "v2_iteration_report.tsv").write_text("\n".join(report), encoding="utf-8")
    save_contact(NORMAL_OUT, "V2 NewNormalWorld", STUDY_OUT / "v2_normal_contact.jpg")
    save_contact(SPECIAL_OUT, "V2 NewSpecialWorld", STUDY_OUT / "v2_special_contact.jpg")
    save_pair_check(normal_lookup)
    print(f"Normal out: {NORMAL_OUT}")
    print(f"Special out: {SPECIAL_OUT}")


if __name__ == "__main__":
    main()
