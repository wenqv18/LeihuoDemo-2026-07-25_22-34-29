from __future__ import annotations

from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageEnhance, ImageFilter, ImageFont, ImageOps


ROOT = Path("D:/unity\u8d44\u6599/\u96f7\u706b\u56fe\u7247\u8d44\u6e90")
SPECIAL_SRC = ROOT / "SPecialWorld"
NORMAL_FINAL = ROOT / "NewNormalWorld_Final"
SPECIAL_FINAL = ROOT / "NewSpecialWorld_Final"
STUDY_OUT = Path("D:/UnityDemo/LeihuoDemo/art_study")

IMAGE_EXTS = {".png", ".jpg", ".jpeg", ".webp", ".tga"}

ALIAS_TO_NORMAL = {
    "cup2": "cup",
    "water1": "water",
    "window2": "window",
    "window3": "window",
    "books": "book",
}

BACKGROUND_KEYS = ("background", "uibackground", "mainscence", "2d unity")
SOFT_EFFECT_KEYS = ("blood", "logo", "ui01", "ui02", "ui03", "ui04")
PAPER_KEYS = ("memo", "map")
NORMAL_SOFT_FIX = {"book", "box", "cup", "minibox", "printer", "map"}
SPECIAL_PHOTOISH = {"cup", "cup2", "books", "window", "window2", "window3", "water1"}


def assets_in(folder: Path) -> list[Path]:
    return [
        p
        for p in folder.iterdir()
        if p.is_file()
        and p.suffix.lower() in IMAGE_EXTS
        and "player" not in p.name.lower()
    ]


def key(path: Path) -> str:
    return path.stem.lower()


def is_background(path: Path) -> bool:
    name = path.name.lower()
    return any(token in name for token in BACKGROUND_KEYS)


def is_soft_effect(path: Path) -> bool:
    k = key(path)
    return any(token in k for token in SOFT_EFFECT_KEYS)


def is_paper(path: Path) -> bool:
    k = key(path)
    return any(token in k for token in PAPER_KEYS)


def visible(alpha: np.ndarray) -> np.ndarray:
    return alpha > 24


def luma(rgb: np.ndarray) -> np.ndarray:
    arr = rgb.astype(np.float32)
    return 0.2126 * arr[..., 0] + 0.7152 * arr[..., 1] + 0.0722 * arr[..., 2]


def median_luma(rgb: np.ndarray, alpha: np.ndarray | None = None) -> float:
    if alpha is None:
        lum = luma(rgb).reshape(-1)
    else:
        mask = visible(alpha)
        if not np.any(mask):
            return 128.0
        lum = luma(rgb)[mask]
    return float(np.median(lum))


def alpha_report(alpha: np.ndarray) -> tuple[float, float]:
    total = alpha.size
    return (
        float(np.count_nonzero(alpha < 255)) / total,
        float(np.count_nonzero((alpha > 0) & (alpha < 255))) / total,
    )


def remap_alpha(alpha: np.ndarray, world: str, path: Path) -> np.ndarray:
    if alpha.min() == 255 and alpha.max() == 255:
        return alpha

    a = alpha.astype(np.float32)
    k = key(path)
    soft_ratio = float(np.count_nonzero((alpha > 0) & (alpha < 255))) / alpha.size

    if is_soft_effect(path) or k.startswith("blood"):
        low, high, gamma = 4.0, 252.0, 0.92
    elif world == "normal":
        low, high, gamma = 24.0, 222.0, 0.70
        if k in NORMAL_SOFT_FIX or soft_ratio > 0.03:
            low, high, gamma = 34.0, 206.0, 0.62
    else:
        low, high, gamma = 22.0, 218.0, 0.66
        if soft_ratio > 0.03:
            low, high, gamma = 32.0, 204.0, 0.60

    t = np.clip((a - low) / (high - low), 0.0, 1.0)
    t = np.power(t, gamma)
    out = np.rint(t * 255.0).astype(np.uint8)
    out[a <= low] = 0
    out[a >= high] = 255
    return out


def blend_posterized(img: Image.Image, amount: float) -> Image.Image:
    poster = ImageOps.posterize(img, 6)
    return Image.blend(img, poster, amount)


def normalize_brightness(img: Image.Image, alpha: np.ndarray | None, target: float) -> Image.Image:
    rgb = np.array(img.convert("RGB"))
    med = median_luma(rgb, alpha)
    factor = target / max(med, 1.0)
    factor = max(0.72, min(1.34, factor))
    return ImageEnhance.Brightness(img).enhance(factor)


def apply_tint(rgb: np.ndarray, alpha: np.ndarray | None, tint: tuple[int, int, int], strength: float) -> np.ndarray:
    arr = rgb.astype(np.float32)
    lum = luma(rgb) / 255.0
    mid_weight = np.clip(lum * (1.25 - 0.45 * lum), 0.0, 1.0)
    if alpha is not None:
        mid_weight *= (alpha.astype(np.float32) / 255.0)
    weight = (strength * mid_weight)[..., None]
    target = np.array(tint, dtype=np.float32)
    out = arr * (1.0 - weight) + target * weight
    return np.clip(out, 0, 255).astype(np.uint8)


def grade_rgb(rgb_img: Image.Image, alpha: np.ndarray | None, world: str, path: Path) -> Image.Image:
    if is_background(path):
        if world == "special":
            rgb_img = ImageEnhance.Color(rgb_img).enhance(0.90)
            rgb_img = ImageEnhance.Contrast(rgb_img).enhance(1.04)
        else:
            rgb_img = ImageEnhance.Color(rgb_img).enhance(0.96)
            rgb_img = ImageEnhance.Contrast(rgb_img).enhance(1.015)
        return rgb_img

    k = key(path)
    if world == "normal":
        color_factor = 0.88
        contrast_factor = 1.045
        poster_amount = 0.10
        tint = (155, 160, 138) if not is_paper(path) else (198, 196, 170)
        tint_strength = 0.055
        target = 132.0
        if is_paper(path):
            target = 154.0
        if "window" in k:
            target = 184.0
        if k in {"shelf", "rightbookshelf"}:
            target = 122.0
    else:
        color_factor = 0.74
        contrast_factor = 1.035
        poster_amount = 0.18
        tint = (55, 86, 78) if not is_paper(path) else (150, 127, 76)
        tint_strength = 0.11
        target = 74.0
        if is_paper(path):
            target = 112.0
        if k in SPECIAL_PHOTOISH:
            color_factor = 0.54
            contrast_factor = 0.98
            poster_amount = 0.26
            target = 78.0 if "window" not in k else 66.0
            tint_strength = 0.15

    rgb_img = ImageEnhance.Color(rgb_img).enhance(color_factor)
    rgb_img = normalize_brightness(rgb_img, alpha, target)
    rgb_img = ImageEnhance.Contrast(rgb_img).enhance(contrast_factor)
    rgb_img = blend_posterized(rgb_img, poster_amount)

    arr = np.array(rgb_img.convert("RGB"))
    arr = apply_tint(arr, alpha, tint, tint_strength)
    rgb_img = Image.fromarray(arr, "RGB")

    rgb_img = rgb_img.filter(ImageFilter.MedianFilter(size=3))
    sharp = 120 if world == "normal" else 105
    if k in NORMAL_SOFT_FIX or k in SPECIAL_PHOTOISH:
        sharp += 20
    return rgb_img.filter(ImageFilter.UnsharpMask(radius=0.72, percent=sharp, threshold=3))


def bleed_rgb(rgb: np.ndarray, alpha: np.ndarray, iterations: int = 24) -> np.ndarray:
    out = rgb.astype(np.float32).copy()
    valid = alpha > 180
    if not np.any(valid):
        valid = alpha > 24
    if not np.any(valid):
        return rgb

    h, w = alpha.shape
    for _ in range(iterations):
        missing = ~valid
        if not np.any(missing):
            break

        sum_rgb = np.zeros_like(out)
        count = np.zeros((h, w), dtype=np.float32)
        for dy, dx in ((-1, 0), (1, 0), (0, -1), (0, 1), (-1, -1), (-1, 1), (1, -1), (1, 1)):
            ys = slice(max(0, dy), min(h, h + dy))
            xs = slice(max(0, dx), min(w, w + dx))
            yd = slice(max(0, -dy), min(h, h - dy))
            xd = slice(max(0, -dx), min(w, w - dx))
            neighbor_valid = valid[ys, xs]
            sum_rgb[yd, xd] += out[ys, xs] * neighbor_valid[..., None]
            count[yd, xd] += neighbor_valid

        fill = missing & (count > 0)
        out[fill] = sum_rgb[fill] / count[fill, None]
        valid[fill] = True

    semi = (alpha > 0) & (alpha < 180)
    out[semi] = out[semi] * 0.55 + rgb.astype(np.float32)[semi] * 0.45
    return np.clip(out, 0, 255).astype(np.uint8)


def add_outer_stroke(rgba: Image.Image, world: str, path: Path) -> Image.Image:
    if is_background(path) or is_soft_effect(path):
        return rgba

    alpha = rgba.getchannel("A")
    if alpha.getbbox() is None:
        return rgba

    dilated = alpha.filter(ImageFilter.MaxFilter(3))
    a = np.array(alpha).astype(np.int16)
    d = np.array(dilated).astype(np.int16)
    stroke_alpha = np.clip(d - a, 0, 110 if world == "normal" else 145).astype(np.uint8)
    if not np.any(stroke_alpha):
        return rgba

    color = (42, 48, 43) if world == "normal" else (25, 19, 17)
    stroke = Image.new("RGBA", rgba.size, color + (0,))
    stroke.putalpha(Image.fromarray(stroke_alpha, "L"))
    base = Image.alpha_composite(stroke, rgba)
    return base


def clip_overlay_to_alpha(overlay: Image.Image, alpha: Image.Image) -> Image.Image:
    oa = np.array(overlay.getchannel("A")).astype(np.uint16)
    aa = np.array(alpha).astype(np.uint16)
    clipped = np.minimum(oa, aa).astype(np.uint8)
    overlay.putalpha(Image.fromarray(clipped, "L"))
    return overlay


def grime_from_normal(normal_name: str, special_name: str, kind: str) -> None:
    src = NORMAL_FINAL / normal_name
    dst = SPECIAL_FINAL / special_name
    if not src.exists():
        return

    base = Image.open(src).convert("RGBA")
    arr = np.array(base)
    alpha = arr[..., 3]
    rgb_img = Image.fromarray(arr[..., :3], "RGB")
    rgb_img = grade_rgb(rgb_img, alpha, "special", dst)
    rgb = np.array(rgb_img).astype(np.float32)

    rng = np.random.default_rng(abs(hash(special_name)) % (2**32))
    noise = rng.normal(0.0, 1.0, alpha.shape).astype(np.float32)
    grime = np.clip(0.88 + noise * 0.045, 0.70, 1.03)
    mask = (alpha > 20)[..., None]
    rgb = np.where(mask, rgb * grime[..., None], rgb)
    out = Image.fromarray(np.dstack([np.clip(rgb, 0, 255).astype(np.uint8), alpha]).astype(np.uint8), "RGBA")

    bbox = out.getchannel("A").getbbox()
    if not bbox:
        out.save(dst)
        return

    overlay = Image.new("RGBA", out.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(overlay)
    x0, y0, x1, y1 = bbox
    w, h = x1 - x0, y1 - y0

    if kind == "books":
        for i in range(6):
            x = x0 + round((i + 0.5) * w / 6)
            d.line([(x, y0 + 6), (x + rng.integers(-2, 3), y1 - 8)], fill=(18, 22, 30, 90), width=2)
        for i in range(4):
            x = x0 + round((i + 1) * w / 5)
            d.line([(x, y0 + 10), (x, y1 - 12)], fill=(35, 95, 125, 55), width=1)
        for _ in range(18):
            x = int(rng.integers(x0, x1))
            y = int(rng.integers(y0, y1))
            r = int(rng.integers(1, 4))
            d.ellipse([x - r, y - r, x + r, y + r], fill=(85, 12, 9, int(rng.integers(35, 85))))
    elif kind == "water":
        bottle_h = round(h * 0.42)
        d.rectangle([x0 + 12, y0 + 6, x1 - 10, y0 + bottle_h], fill=(38, 95, 60, 58))
        d.rectangle([x0 + 14, y0 + bottle_h - 10, x1 - 12, y0 + bottle_h + 12], fill=(86, 12, 8, 38))
        for _ in range(26):
            x = int(rng.integers(x0 + 8, x1 - 8))
            y = int(rng.integers(y0 + 8, y1 - 8))
            d.line(
                [(x, y), (x + int(rng.integers(-14, 15)), y + int(rng.integers(4, 24)))],
                fill=(45, 20, 12, int(rng.integers(45, 105))),
                width=1,
            )

    overlay = clip_overlay_to_alpha(overlay, out.getchannel("A"))
    out = Image.alpha_composite(out, overlay)
    out = add_outer_stroke(out, "special", dst)
    out.save(dst)


def rebuild_structural_aliases() -> None:
    grime_from_normal("Book.png", "Books.png", "books")
    grime_from_normal("Water.png", "Water1.png", "water")


def process(src: Path, dst: Path, world: str) -> tuple[str, int, int, float, float, float, float, float]:
    dst.parent.mkdir(parents=True, exist_ok=True)
    img = Image.open(src)

    has_alpha = img.mode in {"RGBA", "LA"} or (img.mode == "P" and "transparency" in img.info)
    if not has_alpha:
        rgb = grade_rgb(img.convert("RGB"), None, world, src)
        save_kwargs = {"quality": 95} if dst.suffix.lower() in {".jpg", ".jpeg"} else {}
        rgb.save(dst, **save_kwargs)
        return (src.name, rgb.width, rgb.height, 0.0, 0.0, 0.0, 0.0, median_luma(np.array(rgb)))

    rgba = img.convert("RGBA")
    arr = np.array(rgba)
    before_a = arr[..., 3]
    before_all, before_soft = alpha_report(before_a)

    alpha = remap_alpha(before_a, world, src)
    rgb_img = Image.fromarray(arr[..., :3], "RGB")
    rgb_img = grade_rgb(rgb_img, alpha, world, src)
    rgb = np.array(rgb_img)

    edge = (alpha > 0) & (alpha < 250)
    if np.any(edge) and not is_soft_effect(src):
        rgb[edge] = np.clip(rgb[edge].astype(np.float32) * (0.88 if world == "normal" else 0.80), 0, 255).astype(np.uint8)

    rgb = bleed_rgb(rgb, alpha)
    out = Image.fromarray(np.dstack([rgb, alpha]).astype(np.uint8), "RGBA")
    out = add_outer_stroke(out, world, src)
    out.save(dst)

    final_alpha = np.array(out.getchannel("A"))
    after_all, after_soft = alpha_report(final_alpha)
    med = median_luma(np.array(out.convert("RGB")), final_alpha)
    return (src.name, out.width, out.height, before_all, before_soft, after_all, after_soft, med)


def normalize_special_pairs() -> list[tuple[str, str, tuple[int, int], tuple[int, int], tuple[int, int], tuple[int, int]]]:
    normal_map = {key(p): p for p in assets_in(NORMAL_FINAL)}
    special_files = assets_in(SPECIAL_FINAL)
    adjusted = []

    for sp in special_files:
        sk = key(sp)
        nk = ALIAS_TO_NORMAL.get(sk, sk)
        if nk not in normal_map or nk in {"background", "uibackground"}:
            continue

        npth = normal_map[nk]
        normal = Image.open(npth).convert("RGBA")
        special = Image.open(sp).convert("RGBA")
        nb = normal.getchannel("A").getbbox()
        sb = special.getchannel("A").getbbox()
        if not nb or not sb:
            continue

        nw, nh = nb[2] - nb[0], nb[3] - nb[1]
        sw, sh = sb[2] - sb[0], sb[3] - sb[1]
        wr, hr = sw / max(nw, 1), sh / max(nh, 1)
        canvas_wr = special.width / max(normal.width, 1)
        canvas_hr = special.height / max(normal.height, 1)
        needs = max(wr, hr, canvas_wr, canvas_hr) > 1.16 or min(wr, hr, canvas_wr, canvas_hr) < 0.84
        if not needs:
            continue

        crop = special.crop(sb)
        sx = nw / max(sw, 1)
        sy = nh / max(sh, 1)
        if sx > sy:
            sx = min(sx, sy * 1.18)
        else:
            sy = min(sy, sx * 1.18)

        target_w = max(1, round(sw * sx))
        target_h = max(1, round(sh * sy))
        if target_w > normal.width or target_h > normal.height:
            scale = min(normal.width / target_w, normal.height / target_h)
            target_w = max(1, round(target_w * scale))
            target_h = max(1, round(target_h * scale))

        resized = crop.resize((target_w, target_h), Image.Resampling.LANCZOS)
        canvas = Image.new("RGBA", normal.size, (0, 0, 0, 0))
        normal_cx = (nb[0] + nb[2]) // 2
        x = normal_cx - target_w // 2
        y = nb[3] - target_h
        x = max(0, min(normal.width - target_w, x))
        y = max(0, min(normal.height - target_h, y))
        canvas.alpha_composite(resized, (x, y))
        canvas.save(sp)
        adjusted.append((sp.name, npth.name, special.size, normal.size, (sw, sh), (target_w, target_h)))

    return adjusted


def make_contact(folder: Path, title: str, out_path: Path, cols: int = 5) -> None:
    files = assets_in(folder)
    font = ImageFont.load_default()
    cell_w, cell_h, header = 184, 144, 34
    rows = max(1, (len(files) + cols - 1) // cols)
    sheet = Image.new("RGBA", (cols * cell_w, header + rows * cell_h), (35, 37, 41, 255))
    draw = ImageDraw.Draw(sheet)
    draw.text((10, 10), title, fill=(255, 255, 255, 255), font=font)

    for idx, path in enumerate(files):
        im = Image.open(path).convert("RGBA")
        bbox = im.getchannel("A").getbbox()
        if bbox and path.suffix.lower() == ".png":
            im = im.crop(bbox)
        im.thumbnail((cell_w - 12, cell_h - 30), Image.Resampling.LANCZOS)

        cell = Image.new("RGBA", (cell_w, cell_h), (236, 236, 236, 255))
        cd = ImageDraw.Draw(cell)
        step = 12
        for y in range(0, cell_h - 24, step):
            for x in range(0, cell_w, step):
                c = (210, 210, 210, 255) if (x // step + y // step) % 2 else (245, 245, 245, 255)
                cd.rectangle([x, y, x + step - 1, y + step - 1], fill=c)

        cell.alpha_composite(im, ((cell_w - im.width) // 2, (cell_h - 24 - im.height) // 2))
        cd.rectangle([0, cell_h - 24, cell_w, cell_h], fill=(28, 30, 34, 255))
        cd.text((6, cell_h - 18), path.name[:28], fill=(255, 255, 255, 255), font=font)
        sheet.alpha_composite(cell, ((idx % cols) * cell_w, header + (idx // cols) * cell_h))

    sheet.convert("RGB").save(out_path, quality=95)


def make_pair_check() -> None:
    normal = {key(p): p for p in assets_in(NORMAL_FINAL)}
    pairs = []
    for sp in assets_in(SPECIAL_FINAL):
        nk = ALIAS_TO_NORMAL.get(key(sp), key(sp))
        if nk in normal:
            pairs.append((nk, normal[nk], sp))
    pairs.sort(key=lambda item: (item[0], item[2].name))

    font = ImageFont.load_default()
    cell_w, cell_h, gap, header = 190, 150, 16, 56
    sheet = Image.new("RGBA", (cell_w * 2 + gap, header + max(1, len(pairs)) * cell_h), (36, 38, 42, 255))
    d = ImageDraw.Draw(sheet)
    d.text((10, 8), "Final Normal / Special Pair Check", fill=(255, 255, 255, 255), font=font)
    d.text((58, 32), "Normal", fill=(200, 220, 255, 255), font=font)
    d.text((cell_w + gap + 58, 32), "Special", fill=(255, 205, 205, 255), font=font)

    lines = ["normal_key\tnormal_name\tspecial_name\tn_canvas\ts_canvas\tn_bbox\ts_bbox\tbbox_w_ratio\tbbox_h_ratio"]

    def thumb(path: Path) -> tuple[Image.Image, tuple[int, int]]:
        im = Image.open(path).convert("RGBA")
        bbox = im.getchannel("A").getbbox()
        if bbox:
            bw, bh = bbox[2] - bbox[0], bbox[3] - bbox[1]
            crop = im.crop(bbox)
        else:
            bw, bh = im.size
            crop = im
        crop.thumbnail((cell_w - 12, cell_h - 34), Image.Resampling.LANCZOS)
        cell = Image.new("RGBA", (cell_w, cell_h), (236, 236, 236, 255))
        cd = ImageDraw.Draw(cell)
        step = 12
        for y in range(0, cell_h - 24, step):
            for x in range(0, cell_w, step):
                c = (211, 211, 211, 255) if (x // step + y // step) % 2 else (246, 246, 246, 255)
                cd.rectangle([x, y, x + step - 1, y + step - 1], fill=c)
        cell.alpha_composite(crop, ((cell_w - crop.width) // 2, (cell_h - 24 - crop.height) // 2))
        cd.rectangle([0, cell_h - 24, cell_w, cell_h], fill=(28, 30, 34, 255))
        cd.text((6, cell_h - 18), path.name[:28], fill=(255, 255, 255, 255), font=font)
        return cell, (bw, bh)

    for idx, (nk, npth, sp) in enumerate(pairs):
        y = header + idx * cell_h
        tn, nb = thumb(npth)
        ts, sb = thumb(sp)
        sheet.alpha_composite(tn, (0, y))
        sheet.alpha_composite(ts, (cell_w + gap, y))
        wr = round(sb[0] / nb[0], 3) if nb[0] else 0
        hr = round(sb[1] / nb[1], 3) if nb[1] else 0
        lines.append(f"{nk}\t{npth.name}\t{sp.name}\t{Image.open(npth).size}\t{Image.open(sp).size}\t{nb}\t{sb}\t{wr}\t{hr}")

    sheet.convert("RGB").save(STUDY_OUT / "final_pair_contact.jpg", quality=95)
    (STUDY_OUT / "final_pair_metrics.tsv").write_text("\n".join(lines), encoding="utf-8")


def make_bg_previews() -> None:
    normal_places = [
        ("Map.png", (60, 150)), ("SecurityCamera.png", (650, 500)), ("Water.png", (1040, 610)),
        ("Table.png", (1530, 780)), ("Computer.png", (1650, 520)), ("Shelf.png", (2450, 500)),
        ("Window.png", (3320, 330)), ("ElectricalPanel.png", (2920, 470)),
    ]
    special_places = [
        ("Map.png", (60, 150)), ("SecurityCamera.png", (650, 500)), ("Water1.png", (1040, 610)),
        ("Table.png", (1530, 780)), ("Computer.png", (1650, 520)), ("Shelf.png", (2450, 500)),
        ("Window.png", (3320, 330)), ("ElectricalPanel.png", (2920, 470)),
        ("Blood.png", (300, 120)), ("Blood2.png", (2800, 95)),
    ]

    def composite(folder: Path, places: list[tuple[str, tuple[int, int]]], dst: Path) -> None:
        bg = Image.open(folder / "Background.png").convert("RGBA")
        canvas = bg.copy()
        for name, pos in places:
            p = folder / name
            if p.exists():
                canvas.alpha_composite(Image.open(p).convert("RGBA"), pos)
        preview = canvas.convert("RGB")
        preview.thumbnail((1600, 560), Image.Resampling.LANCZOS)
        preview.save(dst, quality=95)

    composite(NORMAL_FINAL, normal_places, STUDY_OUT / "final_normal_bg_preview.jpg")
    composite(SPECIAL_FINAL, special_places, STUDY_OUT / "final_special_bg_preview.jpg")


def main() -> None:
    NORMAL_FINAL.mkdir(exist_ok=True)
    SPECIAL_FINAL.mkdir(exist_ok=True)
    STUDY_OUT.mkdir(exist_ok=True)

    report_rows = []
    for src in assets_in(ROOT):
        report_rows.append(("Normal",) + process(src, NORMAL_FINAL / src.name, "normal"))
    for src in assets_in(SPECIAL_SRC):
        report_rows.append(("Special",) + process(src, SPECIAL_FINAL / src.name, "special"))

    rebuild_structural_aliases()
    adjusted = normalize_special_pairs()

    with (STUDY_OUT / "final_optimization_report.tsv").open("w", encoding="utf-8") as f:
        f.write("world\tname\tw\th\tbefore_transparent_or_soft\tbefore_soft_edge\tafter_transparent_or_soft\tafter_soft_edge\tmedian_luma\n")
        for row in report_rows:
            f.write("\t".join(map(str, row)) + "\n")

    with (STUDY_OUT / "final_pair_normalization_report.tsv").open("w", encoding="utf-8") as f:
        f.write("special_name\tnormal_ref\told_special_canvas\tnew_special_canvas\told_special_bbox\tnew_special_bbox\n")
        for row in adjusted:
            f.write("\t".join(map(str, row)) + "\n")

    make_contact(NORMAL_FINAL, "Final NormalWorld Assets", STUDY_OUT / "final_normal_contact.jpg")
    make_contact(SPECIAL_FINAL, "Final SpecialWorld Assets", STUDY_OUT / "final_special_contact.jpg")
    make_pair_check()
    make_bg_previews()

    print(f"Final Normal output: {NORMAL_FINAL}")
    print(f"Final Special output: {SPECIAL_FINAL}")
    print(f"Adjusted pairs: {len(adjusted)}")


if __name__ == "__main__":
    main()
