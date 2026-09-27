#!/usr/bin/env python3
"""Generates the app's bundled assets so no third-party audio/image files are needed.

    python tools/generate_assets.py

Writes:
  src/Hourglass.App/Assets/ring.wav      - one loop of a two-strike bell (played on repeat by the app)
  src/Hourglass.App/Assets/hourglass.ico - window/exe icon (16-256 px)
"""

import math
import struct
import wave
import zlib
from pathlib import Path

ASSETS = Path(__file__).resolve().parent.parent / "src" / "Hourglass.App" / "Assets"

SAMPLE_RATE = 44_100


def bell(duration, fundamental):
    """A struck bell: inharmonic partials with exponential decay and a short attack."""
    partials = [(1.0, 1.0, 3.0), (2.0, 0.6, 4.5), (2.76, 0.4, 6.0), (5.4, 0.25, 9.0), (8.93, 0.12, 12.0)]
    samples = []
    for i in range(int(duration * SAMPLE_RATE)):
        t = i / SAMPLE_RATE
        attack = min(1.0, t / 0.004)
        value = sum(amp * math.exp(-decay * t) * math.sin(2 * math.pi * fundamental * ratio * t)
                    for ratio, amp, decay in partials)
        samples.append(attack * value)
    return samples


def write_ring(path):
    strike = bell(0.55, 880.0)
    gap = [0.0] * int(0.05 * SAMPLE_RATE)
    second = bell(0.9, 880.0)
    tail = [0.0] * int(0.3 * SAMPLE_RATE)
    samples = strike + gap + second + tail
    peak = max(abs(s) for s in samples)
    frames = b"".join(struct.pack("<h", int(s / peak * 0.8 * 32767)) for s in samples)
    with wave.open(str(path), "wb") as wav:
        wav.setnchannels(1)
        wav.setsampwidth(2)
        wav.setframerate(SAMPLE_RATE)
        wav.writeframes(frames)


def hourglass_coverage(x, y):
    """RGBA colour of the icon at (x, y) in 0..1 space, or None if transparent."""
    frame = (60, 42, 30, 255)
    sand = (232, 176, 62, 255)
    glass = (214, 234, 248, 255)
    # Top and bottom wooden caps.
    if 0.14 <= x <= 0.86 and (0.06 <= y <= 0.16 or 0.84 <= y <= 0.94):
        return frame
    if not 0.16 < y < 0.84:
        return None
    # Glass bulbs: width narrows linearly toward the neck at y = 0.5.
    half_width = 0.05 + 0.28 * abs(y - 0.5) / 0.34
    if abs(x - 0.5) > half_width:
        return None
    if abs(x - 0.5) > half_width - 0.035:
        return frame
    # Sand: bottom bulb mostly full, a little left on top, plus the falling stream.
    if y > 0.62 or 0.38 < y <= 0.5 or (0.5 < y <= 0.62 and abs(x - 0.5) < 0.012):
        return sand
    return glass


def render_png(size):
    ss = 4  # supersampling for smooth edges
    rows = []
    for py in range(size):
        row = bytearray([0])
        for px in range(size):
            acc = [0, 0, 0, 0]
            for sy in range(ss):
                for sx in range(ss):
                    colour = hourglass_coverage((px + (sx + 0.5) / ss) / size, (py + (sy + 0.5) / ss) / size)
                    if colour:
                        for c in range(3):
                            acc[c] += colour[c] * colour[3]
                        acc[3] += colour[3]
            n = ss * ss
            alpha = acc[3] / n
            if acc[3]:
                row += bytes(int(acc[c] / acc[3]) for c in range(3)) + bytes([int(alpha)])
            else:
                row += bytes(4)
        rows.append(bytes(row))

    def chunk(kind, data):
        return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data) & 0xFFFFFFFF)

    header = struct.pack(">IIBBBBB", size, size, 8, 6, 0, 0, 0)
    return (b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", header)
            + chunk(b"IDAT", zlib.compress(b"".join(rows), 9)) + chunk(b"IEND", b""))


def write_icon(path, sizes=(16, 24, 32, 48, 64, 128, 256)):
    images = [render_png(s) for s in sizes]
    offset = 6 + 16 * len(sizes)
    directory = b""
    for size, png in zip(sizes, images):
        dim = 0 if size == 256 else size
        directory += struct.pack("<BBBBHHII", dim, dim, 0, 0, 1, 32, len(png), offset)
        offset += len(png)
    path.write_bytes(struct.pack("<HHH", 0, 1, len(sizes)) + directory + b"".join(images))


if __name__ == "__main__":
    ASSETS.mkdir(parents=True, exist_ok=True)
    write_ring(ASSETS / "ring.wav")
    write_icon(ASSETS / "hourglass.ico")
    print(f"Wrote assets to {ASSETS}")
