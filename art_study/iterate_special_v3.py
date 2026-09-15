from __future__ import annotations

from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageEnhance, ImageFilter, ImageFont, ImageOps


ROOT = Path("D:/unity\u8d44\u6599/\u96f7\u706b\u56fe\u7247\u8d44\u6e90")
SPECIAL_SRC = ROOT / "SPecialWorld"
NORMAL_REF = ROOT / "NewNormalWorld"
SPECIAL_OUT = ROOT / "NewSpecialWorld"
STUDY_OUT = Path("D:/UnityDemo/LeihuoDemo/art_study")

EXTS = {".png", ".jpg", ".jpeg", ".webp", ".tga"}
EFFECTS = {"blood", "blood1", "blood2"}
BACKGROUNDS = {"background", "uibackground"}
PAPER = {"map", "memo_01", "memo_02", "memo_03", "memo_04"}
GREEN_GLOW = {"cup", "cup2", "hanginglamp", "water1"}

NORMAL_NAME_BY_SPECIAL = {
    "books": "Book.png",
    "cup": "Cup.png",
    "cup2": "Cup.png",
    "leftbookshelf": "LeftBookshelf.png",
    "rightbookshelf": "RightBookshelf.png",
    "table": "Table.png",
    "water1": "Water.png",
    "window": "Window.png",
    "window2": "Window.png",
    "window3": "Window.png",
}


def key(path: Path | str) -> str:
    return Path(path).stem.lower().replace(" ", "")


def files(folder: Path) -> list[Path]:
    return [
        p
        for p in folder.iterdir()
        if p.is_file()
        and p.suffix.lower() in EXTS
        and "player" not in p.name.lower()
    ]


def open_rgba(path: Path) -> Image.Image:
    return Image.open(path).convert("RGBA")


def bbox(img: Image.Image) -> tuple[int, int, int, int] | None:
    return img.getchannel("A").getbbox()


def luma(rgb: np.ndarray) -> np.ndarray:
    arr = rgb.astype(np.float32)
    return 0.2126 * arr[..., 0] + 0.7152 * arr[..., 1] + 0.0722 * arr[..., 2]


def alpha_harden(alpha: np.ndarray, k: str) -> np.ndarray:
    if alpha.min() == 255 and alpha.max() == 255:
        return alpha
    if k in EFFECTS:
        low, high, gamma = 4.0, 252.0, 0.94
    else:
        low, high, gamma = 18.0, 218.0, 0.70
    a = alpha.astype(np.float32)
    t = np.clip((a - low) / (high - low), 0.0, 1.0)
    t = np.power(t, gamma)
    out = np.rint(t * 255.0).astype(np.uint8)
    out[a <= low] = 0
    out[a >= high] = 255
    return out


def bleed_rgb(rgb: np.ndarray, alpha: np.ndarray, iterations: int = 22) -> np.ndarray:
    out = rgb.astype(np.float32).copy()
    valid = alpha > 150
    if not np.any(valid):
        valid = alpha > 18
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


def fit_to_reference_canvas(img: Image.Image, ref: Image.Image | None, k: str) -> Image.Image:
    if ref is None or k in BACKGROUNDS or k in EFFECTS:
        return img
    ib = bbox(img)
    rb = bbox(ref)
    if not ib or not rb:
        return img

    iw, ih = ib[2] - ib[0], ib[3] - ib[1]
    rw, rh = rb[2] - rb[0], rb[3] - rb[1]
    if iw <= 0 or ih <= 0 or rw <= 0 or rh <= 0:
        return img

    # Keep the original Special silhouette, but remove abnormal transparent padding
    # and align visible bottom/weight to the Normal reference.
    crop = img.crop(ib)
    scale = min(rw / iw, rh / ih)
    if k in {"cup", "cup2"}:
        scale = min(scale, ref.width / iw, ref.height / ih)
    elif k in {"window", "window2", "window3"}:
        scale *= 0.98
    elif k in {"leftbookshelf", "rightbookshelf", "table", "water1", "books"}:
        scale *= 1.00
    else:
        scale = min(scale * 1.03, max(rw / iw, rh / ih) * 1.02)

    new_w = max(1, min(ref.width, round(iw * scale)))
    new_h = max(1, min(ref.height, round(ih * scale)))
    resized = crop.resize((new_w, new_h), Image.Resampling.LANCZOS)

    canvas = Image.new("RGBA", ref.size, (0, 0, 0, 0))
    rx0, ry0, rx1, ry1 = rb
    cx = (rx0 + rx1) // 2
    x = cx - new_w // 2
    y = ry1 - new_h
    if k in {"window", "window2", "window3"}:
        y = max(0, min(ref.height - new_h, y + 2))
    x = max(0, min(ref.width - new_w, x))
    y = max(0, min(ref.height - new_h, y))
    canvas.alpha_composite(resized, (x, y))
    return canvas


def normalize_special_color(img: Image.Image, name: str) -> Image.Image:
    k = key(name)
    arr = np.array(img.convert("RGBA"))
    alpha = alpha_harden(arr[..., 3], k)
    rgb = arr[..., :3].astype(np.float32)

    if k in BACKGROUNDS:
        out = Image.fromarray(rgb.astype(np.uint8), "RGB")
        out = ImageEnhance.Color(out).enhance(0.88)
        out = ImageEnhance.Contrast(out).enhance(1.06)
        return Image.fromarray(np.dstack([np.array(out), alpha]).astype(np.uint8), "RGBA")

    if k in EFFECTS:
        out = Image.fromarray(rgb.astype(np.uint8), "RGB")
        out = ImageEnhance.Color(out).enhance(0.92)
        out = ImageEnhance.Contrast(out).enhance(1.04)
        return Image.fromarray(np.dstack([np.array(out), alpha]).astype(np.uint8), "RGBA")

    mask = alpha > 18
    if not np.any(mask):
        return img

    # Preserve red blood/rust and sick green glow; unify everything else.
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    red_mask = mask & (r > g * 1.18) & (r > b * 1.12) & (r > 45)
    green_mask = mask & (g > r * 1.08) & (g > b * 1.02) & (g > 55)

    if k in PAPER:
        target = 116.0
        tint_color = np.array([126, 110, 74], dtype=np.float32)
        tint_strength = 0.28
        sat = 0.66
        contrast = 1.02
    elif "window" in k:
        target = 72.0
        tint_color = np.array([38, 54, 50], dtype=np.float32)
        tint_strength = 0.24
        sat = 0.70
        contrast = 1.08
    elif k in GREEN_GLOW:
        target = 72.0
        tint_color = np.array([42, 82, 64], dtype=np.float32)
        tint_strength = 0.16
        sat = 0.78
        contrast = 1.09
    else:
        target = 66.0
        tint_color = np.array([38, 60, 58], dtype=np.float32)
        tint_strength = 0.18
        sat = 0.58
        contrast = 1.10

    pil_rgb = Image.fromarray(np.clip(rgb, 0, 255).astype(np.uint8), "RGB")
    pil_rgb = ImageEnhance.Color(pil_rgb).enhance(sat)
    rgb = np.array(pil_rgb).astype(np.float32)
    lum = luma(rgb)
    med = float(np.median(lum[mask]))
    factor = np.clip(target / max(med, 1.0), 0.70, 1.35)
    rgb *= factor
    lum = luma(rgb)
    mid = np.clip(1.1 - np.abs(lum / 255.0 - 0.45) * 1.55, 0.0, 1.0)
    weight = tint_strength * mid * (alpha.astype(np.float32) / 255.0)
    rgb = rgb * (1 - weight[..., None]) + tint_color * weight[..., None]

    # Restore accent colors after the global grade.
    if np.any(red_mask):
        red_target = np.array([94, 24, 16], dtype=np.float32)
        rgb[red_mask] = rgb[red_mask] * 0.34 + red_target * 0.66
    if np.any(green_mask):
        green_target = np.array([78, 126, 68], dtype=np.float32)
        keep = 0.55 if k in GREEN_GLOW else 0.28
        rgb[green_mask] = rgb[green_mask] * (1.0 - keep) + green_target * keep

    pil_rgb = Image.fromarray(np.clip(rgb, 0, 255).astype(np.uint8), "RGB")
    pil_rgb = ImageEnhance.Contrast(pil_rgb).enhance(contrast)

    # Rebuild line clarity without flattening the original Special shape.
    gray = ImageOps.grayscale(pil_rgb)
    edge = np.array(gray.filter(ImageFilter.FIND_EDGES).filter(ImageFilter.MaxFilter(3))).astype(np.float32)
    rgb = np.array(pil_rgb).astype(np.float32)
    lum = luma(rgb)
    line_mask = mask & ((edge > 26) | (lum < 46))
    rgb[line_mask] = rgb[line_mask] * 0.48 + np.array([16, 17, 15], dtype=np.float32) * 0.52

    if "window" in k:
        rgb = make_window_less_cartoony(rgb, alpha, name)

    rgb = bleed_rgb(np.clip(rgb, 0, 255).astype(np.uint8), alpha)
    out = Image.fromarray(np.dstack([rgb, alpha]).astype(np.uint8), "RGBA")
    out = add_dirty_outline(out, k)
    return out


def make_window_less_cartoony(rgb: np.ndarray, alpha: np.ndarray, name: str) -> np.ndarray:
    out = rgb.astype(np.float32)
    mask = alpha > 18
    if not np.any(mask):
        return rgb
    lum = luma(out)
    dark = mask & (lum < 72)
    out[dark] *= 0.74
    red = mask & (out[..., 0] > out[..., 1] * 1.2) & (out[..., 0] > 55)
    out[red, 0] = np.minimum(150, out[red, 0] * 1.22 + 18)
    out[red, 1] *= 0.55
    out[red, 2] *= 0.55
    return np.clip(out, 0, 255).astype(np.uint8)


def add_dirty_outline(img: Image.Image, k: str) -> Image.Image:
    if k in BACKGROUNDS or k in EFFECTS:
        return img
    alpha = img.getchannel("A")
    if alpha.getbbox() is None:
        return img
    a = np.array(alpha).astype(np.int16)
    dil = np.array(alpha.filter(ImageFilter.MaxFilter(3))).astype(np.int16)
    stroke = np.clip(dil - a, 0, 145).astype(np.uint8)
    layer = Image.new("RGBA", img.size, (18, 16, 14, 0))
    layer.putalpha(Image.fromarray(stroke, "L"))
    return Image.alpha_composite(layer, img)


def reference_for(src: Path) -> Image.Image | None:
    k = key(src)
    name = NORMAL_NAME_BY_SPECIAL.get(k, src.name)
    candidate = NORMAL_REF / name
    if candidate.exists():
        return open_rgba(candidate)
    return None


def process(src: Path) -> Image.Image:
    img = open_rgba(src)
    img = normalize_special_color(img, src.name)
    ref = reference_for(src)
    img = fit_to_reference_canvas(img, ref, key(src))
    return img


def save_asset(img: Image.Image, dst: Path) -> None:
    dst.parent.mkdir(parents=True, exist_ok=True)
    if dst.suffix.lower() in {".jpg", ".jpeg"}:
        img.convert("RGB").save(dst, quality=95)
    else:
        img.save(dst)


def save_contact(out_path: Path) -> None:
    paths = files(SPECIAL_OUT)
    font = ImageFont.load_default()
    cell_w, cell_h, cols, header = 184, 144, 5, 34
    rows = max(1, (len(paths) + cols - 1) // cols)
    sheet = Image.new("RGBA", (cols * cell_w, header + rows * cell_h), (35, 37, 41, 255))
    d = ImageDraw.Draw(sheet)
    d.text((10, 10), "V3 NewSpecialWorld - preserve Special designs", fill=(255, 255, 255, 255), font=font)
    for idx, path in enumerate(paths):
        im = open_rgba(path)
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
    sheet.convert("RGB").save(out_path, quality=95)


def save_pair_check() -> None:
    pairs = []
    for sp in files(SPECIAL_OUT):
        ref = reference_for(sp)
        if ref is not None:
            pairs.append((sp, ref))
    font = ImageFont.load_default()
    cell_w, cell_h, gap, header = 190, 150, 16, 56
    sheet = Image.new("RGBA", (cell_w * 2 + gap, header + max(1, len(pairs)) * cell_h), (36, 38, 42, 255))
    d = ImageDraw.Draw(sheet)
    d.text((10, 8), "V3 Pair Check - Special keeps own design", fill=(255, 255, 255, 255), font=font)
    d.text((58, 32), "Normal Anchor", fill=(200, 220, 255, 255), font=font)
    d.text((cell_w + gap + 52, 32), "Special", fill=(255, 205, 205, 255), font=font)
    rows = ["special\tn_size\ts_size\tn_bbox\ts_bbox\tw_ratio\th_ratio\tbottom_delta"]

    def thumb(img: Image.Image, label: str) -> tuple[Image.Image, tuple[int, int], tuple[int, int, int, int] | None]:
        b = bbox(img)
        bb = img.size
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
        return cell, bb, b

    for idx, (sp, ref) in enumerate(pairs):
        simg = open_rgba(sp)
        y = header + idx * cell_h
        tn, nb, nbx = thumb(ref, "Normal")
        ts, sb, sbx = thumb(simg, sp.name)
        sheet.alpha_composite(tn, (0, y))
        sheet.alpha_composite(ts, (cell_w + gap, y))
        wr = round(sb[0] / nb[0], 3) if nb[0] else 0
        hr = round(sb[1] / nb[1], 3) if nb[1] else 0
        bd = (sbx[3] - nbx[3]) if nbx and sbx else 0
        rows.append(f"{sp.name}\t{ref.size}\t{simg.size}\t{nb}\t{sb}\t{wr}\t{hr}\t{bd}")
    sheet.convert("RGB").save(STUDY_OUT / "v3_pair_contact.jpg", quality=95)
    (STUDY_OUT / "v3_pair_metrics.tsv").write_text("\n".join(rows), encoding="utf-8")


def main() -> None:
    SPECIAL_OUT.mkdir(exist_ok=True)
    STUDY_OUT.mkdir(exist_ok=True)
    for src in files(SPECIAL_SRC):
        save_asset(process(src), SPECIAL_OUT / src.name)
    save_contact(STUDY_OUT / "v3_special_contact.jpg")
    save_pair_check()
    print(f"Special out: {SPECIAL_OUT}")


if __name__ == "__main__":
    main()
