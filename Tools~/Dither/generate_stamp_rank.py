"""Build the example lightning-bolt rank PNG using only Python's standard library.

Run: python Tools~/Dither/generate_stamp_rank.py
Authored polygon points are UV coordinates, bottom to top. Edit POLYGON for another
simple closed motif. Output ranks signed distance, not binary mask intensity.
No Unity importer or runtime dependency. Let Unity create the asset's metadata.
"""
from pathlib import Path
import math
import struct
import zlib

SIZE = 128
POLYGON = [(0.56, 0.94), (0.22, 0.47), (0.46, 0.47),
           (0.36, 0.06), (0.80, 0.60), (0.55, 0.60)]


def signed_distance(x, y):
    inside = False
    distance = math.inf
    for a, b in zip(POLYGON, POLYGON[1:] + POLYGON[:1]):
        dx, dy = b[0] - a[0], b[1] - a[1]
        t = max(0, min(1, ((x - a[0]) * dx + (y - a[1]) * dy) / (dx * dx + dy * dy)))
        distance = min(distance, math.hypot(x - a[0] - t * dx, y - a[1] - t * dy))
        if (a[1] > y) != (b[1] > y) and x < a[0] + (y - a[1]) * dx / dy:
            inside = not inside
    return -distance if inside else distance


def chunk(kind, payload):
    return struct.pack('>I', len(payload)) + kind + payload + struct.pack('>I', zlib.crc32(kind + payload))


def main():
    count = SIZE * SIZE
    ordered = sorted(range(count), key=lambda i: (
        signed_distance((i % SIZE + 0.5) / SIZE, 1 - (i // SIZE + 0.5) / SIZE), i))
    pixels = bytearray(count)
    for rank, index in enumerate(ordered):
        pixels[index] = min(255, int((rank + 0.5) * 256 / count))
    raw = b''.join(b'\0' + pixels[y * SIZE:(y + 1) * SIZE] for y in range(SIZE))
    png = b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', SIZE, SIZE, 8, 0, 0, 0, 0))
    png += chunk(b'IDAT', zlib.compress(raw)) + chunk(b'IEND', b'')
    path = Path(__file__).resolve().parents[2] / 'Runtime/Design/FP_Dither/Examples/Stamps/FP_BoltRank.png'
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(png)
    print(f'{path}: {SIZE}x{SIZE}, 256 rank bins, {count // 256} texels per bin')


if __name__ == '__main__':
    main()
