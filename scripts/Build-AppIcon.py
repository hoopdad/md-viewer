from __future__ import annotations

import io
import struct
from pathlib import Path

from PIL import Image, ImageDraw


REPOSITORY_ROOT = Path(__file__).resolve().parents[1]
ASSET_DIRECTORY = REPOSITORY_ROOT / "src" / "MdViewer.App" / "Assets"
MASTER_PATH = ASSET_DIRECTORY / "AppIcon.png"
ICON_PATH = ASSET_DIRECTORY / "AppIcon.ico"
ICON_SIZES = (16, 20, 24, 32, 40, 48, 64, 128, 256)
SUPERSAMPLING = 4


def scaled(value: float) -> int:
    return round(value * SUPERSAMPLING)


def render_micro_icon(size: int) -> Image.Image:
    canvas_size = size * SUPERSAMPLING
    image = Image.new("RGBA", (canvas_size, canvas_size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)

    draw.rounded_rectangle(
        (scaled(0.7), scaled(0.7), scaled(size - 0.7), scaled(size - 0.7)),
        radius=scaled(size * 0.22),
        fill="#0969DA",
    )

    if size <= 20:
        stroke = scaled(size * 0.10)
        middle = size * 0.50
        draw.line(
            [
                (scaled(size * 0.23), scaled(size * 0.55)),
                (scaled(size * 0.23), scaled(size * 0.27)),
                (scaled(middle), scaled(size * 0.55)),
                (scaled(size * 0.77), scaled(size * 0.27)),
                (scaled(size * 0.77), scaled(size * 0.55)),
            ],
            fill="#FFFFFF",
            width=stroke,
            joint="curve",
        )
        draw.line(
            [
                (scaled(middle), scaled(size * 0.62)),
                (scaled(middle), scaled(size * 0.79)),
                (scaled(size * 0.36), scaled(size * 0.67)),
                (scaled(middle), scaled(size * 0.79)),
                (scaled(size * 0.64), scaled(size * 0.67)),
            ],
            fill="#FFFFFF",
            width=stroke,
            joint="curve",
        )
        return image.resize((size, size), Image.Resampling.LANCZOS)

    left = size * 0.27
    top = size * 0.18
    right = size * 0.73
    bottom = size * 0.84
    fold = size * 0.15
    page = (
        (left, top),
        (right - fold, top),
        (right, top + fold),
        (right, bottom),
        (left, bottom),
    )
    draw.polygon([(scaled(x), scaled(y)) for x, y in page], fill="#FFFFFF")
    draw.polygon(
        [
            (scaled(right - fold), scaled(top)),
            (scaled(right - fold), scaled(top + fold)),
            (scaled(right), scaled(top + fold)),
        ],
        fill="#B6D7FF",
    )

    stroke = max(scaled(size * 0.07), SUPERSAMPLING)
    glyph_left = size * 0.35
    glyph_right = size * 0.65
    glyph_top = size * 0.40
    glyph_bottom = size * 0.59
    glyph_middle = size * 0.50
    draw.line(
        [
            (scaled(glyph_left), scaled(glyph_bottom)),
            (scaled(glyph_left), scaled(glyph_top)),
            (scaled(glyph_middle), scaled(glyph_bottom)),
            (scaled(glyph_right), scaled(glyph_top)),
            (scaled(glyph_right), scaled(glyph_bottom)),
        ],
        fill="#063B7A",
        width=stroke,
        joint="curve",
    )

    arrow_top = size * 0.64
    arrow_bottom = size * 0.75
    arrow_half_width = size * 0.09
    arrow_stroke = max(scaled(size * 0.065), SUPERSAMPLING)
    draw.line(
        [
            (scaled(glyph_middle), scaled(arrow_top)),
            (scaled(glyph_middle), scaled(arrow_bottom)),
        ],
        fill="#0969DA",
        width=arrow_stroke,
    )
    draw.line(
        [
            (scaled(glyph_middle - arrow_half_width), scaled(arrow_bottom - arrow_half_width)),
            (scaled(glyph_middle), scaled(arrow_bottom)),
            (scaled(glyph_middle + arrow_half_width), scaled(arrow_bottom - arrow_half_width)),
        ],
        fill="#0969DA",
        width=arrow_stroke,
        joint="curve",
    )

    return image.resize((size, size), Image.Resampling.LANCZOS)


def encode_png(image: Image.Image) -> bytes:
    output = io.BytesIO()
    image.save(output, format="PNG", optimize=True)
    return output.getvalue()


def write_icon(frames: list[tuple[int, bytes]]) -> None:
    directory_size = 6 + (16 * len(frames))
    offset = directory_size

    with ICON_PATH.open("wb") as icon:
        icon.write(struct.pack("<HHH", 0, 1, len(frames)))
        for size, payload in frames:
            encoded_size = 0 if size == 256 else size
            icon.write(
                struct.pack(
                    "<BBBBHHII",
                    encoded_size,
                    encoded_size,
                    0,
                    0,
                    1,
                    32,
                    len(payload),
                    offset,
                )
            )
            offset += len(payload)

        for _, payload in frames:
            icon.write(payload)


def main() -> None:
    master = Image.open(MASTER_PATH).convert("RGBA")
    frames: list[tuple[int, bytes]] = []

    for size in ICON_SIZES:
        frame = (
            render_micro_icon(size)
            if size <= 24
            else master.resize((size, size), Image.Resampling.LANCZOS)
        )
        frames.append((size, encode_png(frame)))

    write_icon(frames)


if __name__ == "__main__":
    main()
