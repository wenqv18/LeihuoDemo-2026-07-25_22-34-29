from __future__ import annotations

from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageEnhance, ImageFilter, ImageFont


ROOT = Path("D:/unity\u8d44\u6599/\u96f7\u706b\u56fe\u7247\u8d44\u6e90")
SPECIAL_DIR = ROOT / "SPecialWorld"
NORMAL_OUT = ROOT / "NewNormalWorld"
SPECIAL_OUT = ROOT / "NewSpecialWorld"
STUDY_OUT = Path("D:/UnityDemo/LeihuoDemo/art_study")

IMAGE_EXTS = {".png", ".jpg", ".jpeg", ".webp", ".tga"}

SPECIAL_TRANSLUCENT = {"blood", "logo", "ui01", "ui02", "ui03", "ui04"}
BACKGROUND_LIKE = {
    "background",
    "uibackground",
    "mainscence",
    "2d unity",
}
HIGH_SAT_SPECIAL = {"cup", "cup2", "books", "water1", "window", "window2", "window3"}
SOFT_EDGE_NORMAL = {"cup", "book", "box", "minibox", "printer", "map"}


def list_assets(src_dir: Path) -> list[Path]:
    return [
        p
        for p in src_dir.iterdir()
        if p.is_file()
        and p.suffix.lower() in IMAGE_EXTS
        and "player" not in p.name.lower()
    ]


def stem_key(path: Path) -> str:
    return path.stem.lower()


def is_background_like(path: Path) -> bool:
    name = path.name.lower()
    return any(key in name for key in BACKGROUND_LIKE)


def is_translucent_effect(path: Path) -> bool:
    key = stem_key(path)
    return any(token in key for token in SPECIAL_TRANSLUCENT)


def visible_mask(alpha: np.ndarray) -> np.ndarray:
    return alpha > 32


def alpha_stats(alpha: np.ndarray) -> tuple[float, float]:
    total = alpha.size
    transparent_or_soft = float(np.count_nonzero(alpha < 255)) / total
    soft = float(np.count_nonzero((alpha > 0) & (alpha < 255))) / total
    return transparent_or_soft, soft


def harden_alpha(alpha: np.ndarray, world: str, path: Path) -> np.ndarray:
    if alpha.max() == 255 and alpha.min() == 255:
        return alpha

    key = stem_key(path)
    soft_ratio = float(np.count_nonzero((alpha > 0) & (alpha < 255))) / alpha.size

    if is_translucent_effect(path) or (world == "special" and key.startswith("blood")):
        low, high, gamma = 6.0, 252.0, 0.95
    else:
        if world == "normal":
            low, high, gamma = 22.0, 228.0, 0.76
            if key in SOFT_EDGE_NORMAL or soft_ratio > 0.035:
                low, high, gamma = 30.0, 214.0, 0.68
        else:
            low, high, gamma = 18.0, 226.0, 0.72
            if soft_ratio > 0.035:
                low, high, gamma = 26.0, 210.0, 0.66

    a = alpha.astype(np.float32)
    t = np.clip((a - low) / (high - low), 0.0, 1.0)
    t = np.power(t, gamma)
    out = np.round(t * 255.0).astype(np.uint8)
    out[a <= low] = 0
    out[a >= high] = 255
    return out


def mean_luma(rgb: np.ndarray, alpha: np.ndarray | None) -> float:
    if alpha is not None:
        mask = visible_mask(alpha)
        if not np.any(mask):
            return 128.0
        pixels = rgb[mask].astype(np.float32)
    else:
        pixels = rgb.reshape(-1, 3).astype(np.float32)
    luma = 0.2126 * pixels[:, 0] + 0.7152 * pixels[:, 1] + 0.0722 * pixels[:, 2]
    return float(np.mean(luma))


def enhance_rgb(rgb_img: Image.Image, alpha: np.ndarray | None, world: str, path: Path) -> Image.Image:
    if is_background_like(path):
        if world == "special":
            rgb_img = ImageEnhance.Contrast(rgb_img).enhance(1.02)
        return rgb_img

    key = stem_key(path)

    if world == "normal":
        color_factor = 0.96
        contrast_factor = 1.035
        sharp_percent = 95
        if key in SOFT_EDGE_NORMAL:
            sharp_percent = 125
    else:
        color_factor = 0.92
        contrast_factor = 1.055
        sharp_percent = 105
        if key in HIGH_SAT_SPECIAL:
            color_factor = 0.76
            contrast_factor = 1.035

    rgb_img = ImageEnhance.Color(rgb_img).enhance(color_factor)
    rgb_img = ImageEnhance.Contrast(rgb_img).enhance(contrast_factor)

    rgb_np = np.array(rgb_img)
    luma = mean_luma(rgb_np, alpha)
    if world == "normal":
        if luma < 92:
            rgb_img = ImageEnhance.Brightness(rgb_img).enhance(min(1.10, 104.0 / max(luma, 1.0)))
        elif luma > 184:
            rgb_img = ImageEnhance.Brightness(rgb_img).enhance(max(0.91, 170.0 / luma))
    else:
        if luma < 54:
            rgb_img = ImageEnhance.Brightness(rgb_img).enhance(min(1.08, 66.0 / max(luma, 1.0)))
        elif luma > 132:
            rgb_img = ImageEnhance.Brightness(rgb_img).enhance(max(0.84, 112.0 / luma))

    return rgb_img.filter(ImageFilter.UnsharpMask(radius=0.65, percent=sharp_percent, threshold=3))


def darken_edge_rgb(rgb: np.ndarray, alpha: np.ndarray, world: str, path: Path) -> np.ndarray:
    if is_background_like(path):
        return rgb

    out = rgb.astype(np.float32)
    edge = (alpha > 0) & (alpha < 245)
    if not np.any(edge):
        return rgb

    factor = 0.94 if world == "normal" else 0.88
    if is_translucent_effect(path):
        factor = 0.96
    out[edge] *= factor
    return np.clip(out, 0, 255).astype(np.uint8)


def process_image(src: Path, dst: Path, world: str) -> tuple[str, int, int, float, float, float, float]:
    dst.parent.mkdir(parents=True, exist_ok=True)
    img = Image.open(src)

    if img.mode in {"RGBA", "LA"} or (img.mode == "P" and "transparency" in img.info):
        rgba = img.convert("RGBA")
        arr = np.array(rgba)
        before_alpha = arr[:, :, 3]
        before_soft_total, before_soft_edge = alpha_stats(before_alpha)

        new_alpha = harden_alpha(before_alpha, world, src)
        rgb_img = Image.fromarray(arr[:, :, :3], "RGB")
        rgb_img = enhance_rgb(rgb_img, new_alpha, world, src)
        rgb = np.array(rgb_img)
        rgb = darken_edge_rgb(rgb, new_alpha, world, src)

        out = np.dstack([rgb, new_alpha]).astype(np.uint8)
        Image.fromarray(out, "RGBA").save(dst)

        _, after_soft_edge = alpha_stats(new_alpha)
        return (
            src.name,
            rgba.width,
            rgba.height,
            before_soft_total,
            before_soft_edge,
            float(np.count_nonzero(new_alpha < 255)) / new_alpha.size,
            after_soft_edge,
        )

    rgb = img.convert("RGB")
    rgb = enhance_rgb(rgb, None, world, src)
    save_kwargs = {"quality": 95} if dst.suffix.lower() in {".jpg", ".jpeg"} else {}
    rgb.save(dst, **save_kwargs)
    return (src.name, rgb.width, rgb.height, 0.0, 0.0, 0.0, 0.0)


def make_contact(paths: list[Path], title: str, out_path: Path, cols: int = 5) -> None:
    font = ImageFont.load_default()
    cell_w, cell_h = 184, 144
    header = 34
    rows = max(1, (len(paths) + cols - 1) // cols)
    sheet = Image.new("RGBA", (cols * cell_w, header + rows * cell_h), (35, 37, 41, 255))
    draw = ImageDraw.Draw(sheet)
    draw.text((10, 10), title, fill=(255, 255, 255, 255), font=font)

    for idx, path in enumerate(paths):
        im = Image.open(path).convert("RGBA")
        alpha = im.getchannel("A")
        bbox = alpha.getbbox()
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


def get_bbox(path: Path) -> tuple[int, int, int, int] | None:
    im = Image.open(path).convert("RGBA")
    return im.getchannel("A").getbbox()


def normalize_special_pairs() -> list[tuple[str, tuple[int, int], tuple[int, int], tuple[int, int], tuple[int, int]]]:
    normal_map = {stem_key(p): p for p in list_assets(NORMAL_OUT)}
    special_map = {stem_key(p): p for p in list_assets(SPECIAL_OUT)}
    adjusted = []

    for key, normal_path in normal_map.items():
        if key not in special_map or key in {"background", "uibackground"}:
            continue

        special_path = special_map[key]
        normal_img = Image.open(normal_path).convert("RGBA")
        special_img = Image.open(special_path).convert("RGBA")
        nb = normal_img.getchannel("A").getbbox()
        sb = special_img.getchannel("A").getbbox()
        if not nb or not sb:
            continue

        nw, nh = nb[2] - nb[0], nb[3] - nb[1]
        sw, sh = sb[2] - sb[0], sb[3] - sb[1]
        wr = sw / max(nw, 1)
        hr = sh / max(nh, 1)
        canvas_wr = special_img.width / max(normal_img.width, 1)
        canvas_hr = special_img.height / max(normal_img.height, 1)

        if max(wr, hr, canvas_wr, canvas_hr) <= 1.18 and min(wr, hr, canvas_wr, canvas_hr) >= 0.84:
            continue

        crop = special_img.crop(sb)
        uniform = min(nw / max(sw, 1), nh / max(sh, 1))
        sx = nw / max(sw, 1)
        sy = nh / max(sh, 1)
        if sx > sy:
            sx = min(sx, sy * 1.22)
        else:
            sy = min(sy, sx * 1.22)
        target_w = max(1, round(sw * sx))
        target_h = max(1, round(sh * sy))

        if target_w > nw * 1.08 or target_h > nh * 1.08:
            target_w = max(1, round(sw * uniform))
            target_h = max(1, round(sh * uniform))

        resized = crop.resize((target_w, target_h), Image.Resampling.LANCZOS)
        canvas = Image.new("RGBA", normal_img.size, (0, 0, 0, 0))

        normal_cx = (nb[0] + nb[2]) // 2
        x = normal_cx - target_w // 2
        y = nb[3] - target_h
        x = max(0, min(normal_img.width - target_w, x))
        y = max(0, min(normal_img.height - target_h, y))
        canvas.alpha_composite(resized, (x, y))
        canvas.save(special_path)

        adjusted.append((special_path.name, (special_img.width, special_img.height), normal_img.size, (sw, sh), (target_w, target_h)))

    return adjusted


def main() -> None:
    NORMAL_OUT.mkdir(exist_ok=True)
    SPECIAL_OUT.mkdir(exist_ok=True)
    STUDY_OUT.mkdir(exist_ok=True)

    report_rows = []
    for src in list_assets(ROOT):
        dst = NORMAL_OUT / src.name
        report_rows.append(("Normal",) + process_image(src, dst, "normal"))

    for src in list_assets(SPECIAL_DIR):
        dst = SPECIAL_OUT / src.name
        report_rows.append(("Special",) + process_image(src, dst, "special"))

    adjusted_pairs = normalize_special_pairs()

    report = STUDY_OUT / "optimization_report.tsv"
    with report.open("w", encoding="utf-8") as f:
        f.write(
            "world\tname\tw\th\tbefore_transparent_or_soft\tbefore_soft_edge\t"
            "after_transparent_or_soft\tafter_soft_edge\n"
        )
        for row in report_rows:
            f.write("\t".join(map(str, row)) + "\n")

    pair_report = STUDY_OUT / "pair_normalization_report.tsv"
    with pair_report.open("w", encoding="utf-8") as f:
        f.write("name\told_special_canvas\tnew_special_canvas\told_special_bbox\tnew_special_bbox\n")
        for row in adjusted_pairs:
            f.write("\t".join(map(str, row)) + "\n")

    make_contact(list_assets(NORMAL_OUT), "Optimized NormalWorld", STUDY_OUT / "optimized_normal_contact.jpg")
    make_contact(list_assets(SPECIAL_OUT), "Optimized SpecialWorld", STUDY_OUT / "optimized_special_contact.jpg")

    print(f"Normal output: {NORMAL_OUT}")
    print(f"Special output: {SPECIAL_OUT}")
    print(f"Report: {report}")
    print(f"Pair normalization report: {pair_report}")


if __name__ == "__main__":
    main()
