from __future__ import annotations

from datetime import datetime
from pathlib import Path
import shutil

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont


PROJECT = Path("D:/UnityDemo/LeihuoDemo")
RESOURCES = PROJECT / "Assets" / "Resources"
NORMAL_FINAL = Path("D:/unity\u8d44\u6599/\u96f7\u706b\u56fe\u7247\u8d44\u6e90/NewNormalWorld_Final")
SPECIAL_FINAL = Path("D:/unity\u8d44\u6599/\u96f7\u706b\u56fe\u7247\u8d44\u6e90/NewSpecialWorld_Final")
REPORT_DIR = PROJECT / "art_study"
BACKUP_ROOT = PROJECT / "_codex_backups"


def normalize(path: str) -> Path:
    return RESOURCES / Path(path)


DIRECT_REPLACEMENTS: list[tuple[Path, Path, str]] = []


def add(src_dir: Path, src_name: str, target_rel: str, reason: str = "direct") -> None:
    DIRECT_REPLACEMENTS.append((src_dir / src_name, normalize(target_rel), reason))


for name in [
    "Book.png",
    "Box.png",
    "Computer.png",
    "Cup.png",
    "Door.png",
    "ElectricalPanel.png",
    "HangingLamp.png",
    "Memo_01.png",
    "Memo_02.png",
    "Memo_03.png",
    "Memo_04.png",
    "MiniBox.png",
    "Printer.png",
    "SecurityCamera.png",
    "Shelf.png",
    "Table.png",
    "Water.png",
    "Window.png",
]:
    add(NORMAL_FINAL, name, f"NormalWorld/InteractionEnvironment/{name}")

add(NORMAL_FINAL, "Background.png", "NormalWorld/Environment/Background.png")
add(NORMAL_FINAL, "LeftBookshelf.png", "NormalWorld/InteractionEnvironment/LeftBookshelf 1.png", "mapped name")
add(NORMAL_FINAL, "RightBookshelf.png", "NormalWorld/InteractionEnvironment/RightBookshelf 1.png", "mapped name")
add(NORMAL_FINAL, "Map.png", "NormalWorld/InteractionEnvironment/Map.png")
add(NORMAL_FINAL, "Map.png", "NormalWorld/InteractionEnvironment/Map 1.png", "mapped name")

for rel_prefix in ["UI/MainScence", "UI/MainScence/MainScence"]:
    for name in ["Logo.png", "UI01.png", "UI02.png", "UI03.png", "UI04.png", "UIBackground.jpg"]:
        add(NORMAL_FINAL, name, f"{rel_prefix}/{name}")
add(NORMAL_FINAL, "Logo.png", "UI/MainScence/MainScence/Logo 1.png", "mapped name")

add(SPECIAL_FINAL, "Background.png", "SpecialWorld/Environment/Background.png")
add(SPECIAL_FINAL, "Blood.png", "SpecialWorld/Environment/Blood.png")
add(SPECIAL_FINAL, "Blood2.png", "SpecialWorld/Environment/Blood2.png")
add(SPECIAL_FINAL, "UIBackground.jpg", "SpecialWorld/Environment/UIBackground.jpg")

for name in [
    "Books.png",
    "Computer.png",
    "Cup.png",
    "Cup2.png",
    "ElectricalPanel.png",
    "HangingLamp.png",
    "Map.png",
    "Memo_01.png",
    "Memo_02.png",
    "Memo_03.png",
    "Memo_04.png",
    "Printer.png",
    "SecurityCamera.png",
    "Shelf.png",
    "Water1.png",
    "Window.png",
    "Window2.png",
    "Window3.png",
]:
    add(SPECIAL_FINAL, name, f"SpecialWorld/InteractionEnvironment/{name}")

add(SPECIAL_FINAL, "LeftBookShelf.png", "SpecialWorld/InteractionEnvironment/LeftBookShelf 1.png", "mapped name")
add(SPECIAL_FINAL, "RightBookShelf.png", "SpecialWorld/InteractionEnvironment/RightBookShelf 1.png", "mapped name")
add(SPECIAL_FINAL, "Table.png", "SpecialWorld/InteractionEnvironment/SpecialTable_01.png", "mapped name")


SELECTED_REPLACEMENTS = {
    "SelectedBook.png": "Book.png",
    "SelectedBox.png": "Box.png",
    "SelectedCup.png": "Cup.png",
    "SelectedLeftBookshelf.png": "LeftBookshelf.png",
    "SelectedMap.png": "Map.png",
    "SelectedMiniBox.png": "MiniBox.png",
    "SelectedPrinter.png": "Printer.png",
    "SelectedRightBookshelf.png": "RightBookshelf.png",
    "SelectedTable.png": "Table.png",
    "SelectedWater.png": "Water.png",
    "SelectedWindow.png": "Window.png",
}


def bbox_from_mask(mask: np.ndarray) -> tuple[int, int, int, int] | None:
    ys, xs = np.where(mask)
    if len(xs) == 0:
        return None
    return int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1


def content_bbox_excluding_white(rgba: Image.Image) -> tuple[int, int, int, int] | None:
    arr = np.array(rgba)
    alpha = arr[..., 3] > 16
    rgb = arr[..., :3]
    non_white = np.any(rgb < 232, axis=2)
    return bbox_from_mask(alpha & non_white)


def alpha_bbox(rgba: Image.Image) -> tuple[int, int, int, int] | None:
    return rgba.getchannel("A").getbbox()


def make_selected_sprite(base_path: Path, old_target: Path, reference_target: Path | None = None) -> Image.Image:
    old = Image.open(reference_target if reference_target and reference_target.exists() else old_target).convert("RGBA")
    base = Image.open(base_path).convert("RGBA")

    content_box = content_bbox_excluding_white(old) or alpha_bbox(old)
    if not content_box:
        return old

    bx = alpha_bbox(base)
    if not bx:
        return old

    crop = base.crop(bx)
    x0, y0, x1, y1 = content_box
    target_w = max(1, x1 - x0)
    target_h = max(1, y1 - y0)

    scale = min(target_w / crop.width, target_h / crop.height)
    new_w = max(1, round(crop.width * scale))
    new_h = max(1, round(crop.height * scale))
    resized = crop.resize((new_w, new_h), Image.Resampling.LANCZOS)

    x = x0 + (target_w - new_w) // 2
    y = y0 + (target_h - new_h) // 2

    canvas = Image.new("RGBA", old.size, (0, 0, 0, 0))

    old_arr = np.array(old)
    old_alpha = old_arr[..., 3]
    old_white = (
        (old_arr[..., 0] > 210)
        & (old_arr[..., 1] > 210)
        & (old_arr[..., 2] > 210)
        & (old_alpha > 16)
    )
    white_alpha = np.where(old_white, np.maximum(old_alpha, 210), 0).astype(np.uint8)

    if np.count_nonzero(white_alpha) < 100:
        alpha = resized.getchannel("A")
        stroke_alpha = alpha
        iterations = max(3, round(max(new_w, new_h) * 0.035))
        for _ in range(iterations):
            stroke_alpha = stroke_alpha.filter(ImageFilter.MaxFilter(5))
        white_patch = Image.new("RGBA", resized.size, (255, 255, 255, 0))
        white_patch.putalpha(stroke_alpha)
        canvas.alpha_composite(white_patch, (x, y))
    else:
        white_layer = Image.new("RGBA", old.size, (255, 255, 255, 0))
        white_layer.putalpha(Image.fromarray(white_alpha, "L"))
        canvas.alpha_composite(white_layer)

    canvas.alpha_composite(resized, (x, y))
    return canvas


def backup_file(path: Path, backup_dir: Path) -> None:
    rel = path.relative_to(RESOURCES)
    dst = backup_dir / rel
    dst.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(path, dst)


def make_contact(paths: list[Path], out_path: Path) -> None:
    font = ImageFont.load_default()
    cell_w, cell_h, cols, header = 220, 150, 4, 34
    rows = max(1, (len(paths) + cols - 1) // cols)
    sheet = Image.new("RGBA", (cols * cell_w, header + rows * cell_h), (34, 36, 40, 255))
    draw = ImageDraw.Draw(sheet)
    draw.text((10, 10), "Replaced project Resources assets", fill=(255, 255, 255, 255), font=font)

    for idx, path in enumerate(paths):
        im = Image.open(path).convert("RGBA")
        bbox = alpha_bbox(im)
        if bbox and path.suffix.lower() == ".png":
            im = im.crop(bbox)
        im.thumbnail((cell_w - 12, cell_h - 34), Image.Resampling.LANCZOS)

        cell = Image.new("RGBA", (cell_w, cell_h), (236, 236, 236, 255))
        cd = ImageDraw.Draw(cell)
        step = 12
        for y in range(0, cell_h - 24, step):
            for x in range(0, cell_w, step):
                c = (211, 211, 211, 255) if (x // step + y // step) % 2 else (246, 246, 246, 255)
                cd.rectangle([x, y, x + step - 1, y + step - 1], fill=c)
        cell.alpha_composite(im, ((cell_w - im.width) // 2, (cell_h - 24 - im.height) // 2))
        cd.rectangle([0, cell_h - 24, cell_w, cell_h], fill=(28, 30, 34, 255))
        label = str(path.relative_to(RESOURCES)).replace("\\", "/")[:42]
        cd.text((5, cell_h - 18), label, fill=(255, 255, 255, 255), font=font)
        sheet.alpha_composite(cell, ((idx % cols) * cell_w, header + (idx // cols) * cell_h))

    sheet.convert("RGB").save(out_path, quality=95)


def main() -> None:
    timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    prior_backups = sorted(BACKUP_ROOT.glob("resources_art_replace_*"))
    selected_reference_backup = prior_backups[0] if prior_backups else None
    backup_dir = BACKUP_ROOT / f"resources_art_replace_{timestamp}"
    backup_dir.mkdir(parents=True, exist_ok=False)
    REPORT_DIR.mkdir(parents=True, exist_ok=True)

    rows: list[str] = ["target\tsource\taction"]
    replaced: list[Path] = []

    seen: set[Path] = set()
    for src, target, reason in DIRECT_REPLACEMENTS:
        if target in seen:
            continue
        seen.add(target)
        if not target.exists():
            rows.append(f"{target.relative_to(RESOURCES)}\t{src}\tskipped_missing_target")
            continue
        if not src.exists():
            rows.append(f"{target.relative_to(RESOURCES)}\t{src}\tskipped_missing_source")
            continue
        backup_file(target, backup_dir)
        shutil.copyfile(src, target)
        replaced.append(target)
        rows.append(f"{target.relative_to(RESOURCES)}\t{src}\treplaced_{reason}")

    selected_dir = RESOURCES / "NormalWorld" / "InteractionEnvironment" / "Selected"
    for selected_name, base_name in SELECTED_REPLACEMENTS.items():
        target = selected_dir / selected_name
        src = NORMAL_FINAL / base_name
        if not target.exists() or not src.exists():
            rows.append(f"{target.relative_to(RESOURCES)}\t{src}\tskipped_selected")
            continue
        backup_file(target, backup_dir)
        reference = None
        if selected_reference_backup:
            candidate = selected_reference_backup / target.relative_to(RESOURCES)
            if candidate.exists():
                reference = candidate
        make_selected_sprite(src, target, reference).save(target)
        replaced.append(target)
        rows.append(f"{target.relative_to(RESOURCES)}\t{src}\trebuilt_selected_state")

    report = REPORT_DIR / "project_resource_replace_report.tsv"
    report.write_text("\n".join(rows), encoding="utf-8")
    make_contact(replaced, REPORT_DIR / "project_resources_after_replace_contact.jpg")

    print(f"Replaced files: {len(replaced)}")
    print(f"Backup: {backup_dir}")
    print(f"Report: {report}")


if __name__ == "__main__":
    main()
