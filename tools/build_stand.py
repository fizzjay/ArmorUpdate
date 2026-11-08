"""Build the platform-neutral stand resource; never changes armor.pack.

Run: python tools/build_stand.py
Only Python's standard library is required. OBJ coordinates/UVs are retained;
faces are fan triangulated. PNG RGB/RGBA8 is decoded to bottom-up RGBA32 so
neither client needs Unity ImageConversion or a platform-specific asset bundle.
"""
from pathlib import Path
import math
import struct
import zlib

ROOT = Path(__file__).resolve().parents[1]


def png_rgba(data):
    if data[:8] != b'\x89PNG\r\n\x1a\n':
        raise ValueError('Not a PNG')
    offset, compressed = 8, bytearray()
    width = height = channels = None
    while offset < len(data):
        length = struct.unpack_from('>I', data, offset)[0]
        kind = data[offset + 4:offset + 8]
        block = data[offset + 8:offset + 8 + length]
        if zlib.crc32(kind + block) & 0xffffffff != struct.unpack_from('>I', data, offset + 8 + length)[0]:
            raise ValueError('PNG CRC mismatch')
        if kind == b'IHDR':
            width, height, depth, color, comp, filt, interlace = struct.unpack('>IIBBBBB', block)
            if depth != 8 or color not in (2, 6) or comp or filt or interlace:
                raise ValueError('Expected non-interlaced RGB/RGBA8 PNG')
            channels = 3 if color == 2 else 4
        elif kind == b'IDAT':
            compressed.extend(block)
        elif kind == b'IEND':
            break
        offset += length + 12
    if not width or not height or not channels:
        raise ValueError('Missing PNG header')
    raw = zlib.decompress(compressed)
    stride = width * channels
    if len(raw) != height * (stride + 1):
        raise ValueError('Invalid PNG scanline size')
    previous = bytearray(stride)
    rows = []
    for y in range(height):
        mode = raw[y * (stride + 1)]
        row = bytearray(raw[y * (stride + 1) + 1:(y + 1) * (stride + 1)])
        for x in range(stride):
            a = row[x - channels] if x >= channels else 0
            b = previous[x]
            c = previous[x - channels] if x >= channels else 0
            if mode == 1:
                prediction = a
            elif mode == 2:
                prediction = b
            elif mode == 3:
                prediction = (a + b) // 2
            elif mode == 4:
                p = a + b - c
                distances = (abs(p - a), abs(p - b), abs(p - c))
                prediction = (a, b, c)[distances.index(min(distances))]
            elif mode == 0:
                prediction = 0
            else:
                raise ValueError('Invalid PNG filter')
            row[x] = (row[x] + prediction) & 255
        previous = row
        rows.append(b''.join(bytes(row[x:x + channels]) + (b'\xff' if channels == 3 else b'')
                             for x in range(0, stride, channels)))
    return width, height, b''.join(reversed(rows))


def obj_mesh(text):
    positions, normals, uvs, vertices, indices = [], [], [], [], []
    unique = {}

    def resolve(index, values):
        index = int(index)
        if index == 0:
            raise ValueError('OBJ indices are one-based')
        return values[index - 1 if index > 0 else index]

    for line in text.splitlines():
        fields = line.split('#', 1)[0].split()
        if not fields:
            continue
        kind, values = fields[0], fields[1:]
        if kind in ('v', 'vn', 'vt'):
            target = {'v': positions, 'vn': normals, 'vt': uvs}[kind]
            target.append(tuple(float(x) for x in values[:2 if kind == 'vt' else 3]))
        elif kind == 'f':
            face = []
            points = [resolve(v.split('/')[0], positions) for v in values]
            a, b, c = points[:3]
            u, v = [b[i] - a[i] for i in range(3)], [c[i] - a[i] for i in range(3)]
            cross = (u[1]*v[2] - u[2]*v[1], u[2]*v[0] - u[0]*v[2], u[0]*v[1] - u[1]*v[0])
            length = math.sqrt(sum(x*x for x in cross))
            if length < 1e-12:
                raise ValueError('Degenerate OBJ face')
            flat_normal = tuple(x / length for x in cross)
            for value in values:
                parts = value.split('/')
                if len(parts) < 2 or not parts[1]:
                    raise ValueError('Stand faces must have positions and UVs')
                normal = resolve(parts[2], normals) if len(parts) == 3 and parts[2] else flat_normal
                vertex = resolve(parts[0], positions) + normal + resolve(parts[1], uvs)
                if not all(math.isfinite(v) for v in vertex):
                    raise ValueError('Nonfinite OBJ vertex')
                if vertex not in unique:
                    unique[vertex] = len(vertices)
                    vertices.append(vertex)
                face.append(unique[vertex])
            if len(face) < 3:
                raise ValueError('Invalid OBJ face')
            for i in range(1, len(face) - 1):
                indices.extend((face[0], face[i], face[i + 1]))
    if not vertices or not indices or len(vertices) > 65535:
        raise ValueError('Expected a nonempty 16-bit stand mesh')
    return vertices, indices


def build():
    vertices, indices = obj_mesh((ROOT / 'armorstand/armor_stand.obj').read_text())
    width, height, rgba = png_rgba((ROOT / 'armorstand/Stand_Atlas.png').read_bytes())
    return (b'ASTD' + struct.pack('<3i', 1, len(vertices), len(indices)) +
            b''.join(struct.pack('<8f', *v) for v in vertices) +
            struct.pack('<%di' % len(indices), *indices) +
            struct.pack('<3i', width, height, len(rgba)) + rgba)


if __name__ == '__main__':
    destination = ROOT / 'armorstand/stand.bin'
    data = build()
    destination.write_bytes(data)
    print('%s: %d bytes' % (destination.relative_to(ROOT), len(data)))
