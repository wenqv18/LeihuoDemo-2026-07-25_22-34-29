from __future__ import annotations

from pathlib import Path
import shutil

import numpy as np
from PIL import Image, ImageDraw, ImageFilter


PROJECT = Path("D:/UnityDemo/LeihuoDemo")
RESOURCES = PROJECT / "Assets" / "Resources"
NORMAL_SRC = Path("D:/unity\u8d44\u6599/\u96f7\u706b\u56fe\u7247\u8d44\u6e90/NewNormalWorld")
SPECIAL_SRC = Path("D:/unity\u8d44\u6599/\u96f7\u706b\u56fe\u7247\u8d44\u6e90/NewSpecialWorld")
REPORT_DIR = PROJECT / "art_study"


DIRECT: list[tuple[Path, Path, str]] = []


def target(rel: str) -> Path:
    return RESOURCES / Path(rel)


def add(src_dir: Path, src_name: str, rel: str, reason: str = "direct") -> None:
    DIRECT.append((src_dir / src_name, target(rel), reason))


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
    add(NORMAL_SRC, name, f"NormalWorld/InteractionEnvironment/{name}")

add(NORMAL_SRC, "Background.png", "NormalWorld/Environment/Background.png")
add(NORMAL_SRC, "LeftBookshelf.png", "NormalWorld/InteractionEnvironment/LeftBookshelf 1.png", "mapped_name")
add(NORMAL_SRC, "RightBookshelf.png", "NormalWorld/InteractionEnvironment/RightBookshelf 1.png", "mapped_name")
add(NORMAL_SRC, "Map.png", "NormalWorld/InteractionEnvironment/Map.png")
add(NORMAL_SRC, "Map.png", "NormalWorld/InteractionEnvironment/Map 1.png", "mapped_name")

for rel_prefix in ["UI/MainScence", "UI/MainScence/MainScence"]:
    for name in ["Logo.png", "UI01.png", "UI02.png", "UI03.png", "UI04.png", "UIBackground.jpg"]:
        add(NORMAL_SRC, name, f"{rel_prefix}/{name}")
add(NORMAL_SRC, "Logo.png", "UI/MainScence/MainScence/Logo 1.png", "mapped_name")

add(SPECIAL_SRC, "Background.png", "SpecialWorld/Environment/Background.png")
add(SPECIAL_SRC, "Blood.png", "SpecialWorld/Environment/Blood.png")
add(SPECIAL_SRC, "Blood2.png", "SpecialWorld/Environment/Blood2.png")
add(SPECIAL_SRC, "UIBackground.jpg", "SpecialWorld/Environment/UIBackground.jpg")

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
    add(SPECIAL_SRC, name, f"SpecialWorld/InteractionEnvironment/{name}")

add(SPECIAL_SRC, "LeftBookShelf.png", "SpecialWorld/InteractionEnvironment/LeftBookShelf 1.png", "mapped_name")
add(SPECIAL_SRC, "RightBookShelf.png", "SpecialWorld/InteractionEnvironment/RightBookShelf 1.png", "mapped_name")
add(SPECIAL_SRC, "Table.png", "SpecialWorld/InteractionEnvironment/SpecialTable_01.png", "mapped_name")


SELECTED = {
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


def alpha_bbox(img: Image.Image) -> tuple[int, int, int, int] | None:
    return img.getchannel("A").getbbox()


def content_bbox_excluding_white(img: Image.Image) -> tuple[int, int, int, int] | None:
    arr = np.array(img)
    alpha = arr[..., 3] > 16
    non_white = np.any(arr[..., :3] < 232, axis=2)
    mask = alpha & non_white
    ys, xs = np.where(mask)
    if len(xs) == 0:
        return alpha_bbox(img)
    return int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1


def make_selected(base_path: Path, old_target: Path) -> Image.Image:
    old = Image.open(old_target).convert("RGBA")
    base = Image.open(base_path).convert("RGBA")
    content_box = content_bbox_excluding_white(old)
    base_box = alpha_bbox(base)
    if not content_box or not base_box:
        return old

    x0, y0, x1, y1 = content_box
    target_w, target_h = x1 - x0, y1 - y0
    crop = base.crop(base_box)
    scale = min(target_w / crop.width, target_h / crop.height)
    new_w = max(1, round(crop.width * scale))
    new_h = max(1, round(crop.height * scale))
    resized = crop.resize((new_w, new_h), Image.Resampling.LANCZOS)
    x = x0 + (target_w - new_w) // 2
    y = y0 + (target_h - new_h) // 2

    old_arr = np.array(old)
    old_alpha = old_arr[..., 3]
    old_white = (
        (old_arr[..., 0] > 210)
        & (old_arr[..., 1] > 210)
        & (old_arr[..., 2] > 210)
        & (old_alpha > 16)
    )
    white_alpha = np.where(old_white, np.maximum(old_alpha, 210), 0).astype(np.uint8)

    canvas = Image.new("RGBA", old.size, (0, 0, 0, 0))
    if np.count_nonzero(white_alpha) >= 100:
        white_layer = Image.new("RGBA", old.size, (255, 255, 255, 0))
        white_layer.putalpha(Image.fromarray(white_alpha, "L"))
        canvas.alpha_composite(white_layer)
    else:
        stroke_alpha = resized.getchannel("A")
        for _ in range(max(3, round(max(new_w, new_h) * 0.035))):
            stroke_alpha = stroke_alpha.filter(ImageFilter.MaxFilter(5))
        stroke = Image.new("RGBA", resized.size, (255, 255, 255, 0))
        stroke.putalpha(stroke_alpha)
        canvas.alpha_composite(stroke, (x, y))

    canvas.alpha_composite(resized, (x, y))
    return canvas


def main() -> None:
    REPORT_DIR.mkdir(parents=True, exist_ok=True)
    rows = ["target\tsource\taction"]
    replaced: list[Path] = []

    seen: set[Path] = set()
    for src, dst, reason in DIRECT:
        if dst in seen:
            continue
        seen.add(dst)
        if not dst.exists():
            rows.append(f"{dst.relative_to(RESOURCES)}\t{src}\tskipped_missing_target")
            continue
        if not src.exists():
            rows.append(f"{dst.relative_to(RESOURCES)}\t{src}\tskipped_missing_source")
            continue
        shutil.copyfile(src, dst)
        replaced.append(dst)
        rows.append(f"{dst.relative_to(RESOURCES)}\t{src}\treplaced_{reason}")

    selected_dir = RESOURCES / "NormalWorld" / "InteractionEnvironment" / "Selected"
    for selected_name, base_name in SELECTED.items():
        dst = selected_dir / selected_name
        src = NORMAL_SRC / base_name
        if not dst.exists() or not src.exists():
            rows.append(f"{dst.relative_to(RESOURCES)}\t{src}\tskipped_selected")
            continue
        make_selected(src, dst).save(dst)
        replaced.append(dst)
        rows.append(f"{dst.relative_to(RESOURCES)}\t{src}\trebuilt_selected_state")

    report = REPORT_DIR / "project_resource_replace_from_new_report.tsv"
    report.write_text("\n".join(rows), encoding="utf-8")
    print(f"Replaced files: {len(replaced)}")
    print(f"Report: {report}")


if __name__ == "__main__":
    main()
