"""Re-rig worn metal plates to the leather skin; leave pickup meshes untouched.

Pure Python, no Unity or third-party packages needed. Run from repository root:
    python tools/skin_metal.py
Both clients embed the same version-1 pack. Existing mesh records, non-metal
pieces, pickup records and collider records are retained byte for byte.
"""
import math
import struct
from pathlib import Path


class Reader:
    def __init__(self, data):
        self.data, self.pos = data, 0

    def take(self, n):
        value = self.data[self.pos:self.pos + n]
        if len(value) != n:
            raise ValueError('Truncated armor pack')
        self.pos += n
        return value

    def number(self):
        return struct.unpack('<i', self.take(4))[0]

    def text(self):
        return self.take(self.number()).decode('utf-8')

    def array(self, fmt):
        count = self.number()
        size = struct.calcsize('<' + fmt)
        return [struct.unpack('<' + fmt, self.take(size)) for _ in range(count)]


def integer(value):
    return struct.pack('<i', value)


def text(value):
    data = value.encode('utf-8')
    return integer(len(data)) + data


def array(fmt, values):
    return integer(len(values)) + b''.join(struct.pack('<' + fmt, *v) for v in values)


def parse(data):
    r = Reader(data)
    if r.take(8) != b'ARMR\x01\x00\x00\x00':
        raise ValueError('Expected version-1 armor pack')
    for _ in range(r.number()):
        r.text()
        r.take(16)
        r.take(r.number())
    for _ in range(r.number()):
        r.text()
        r.take(40)
    prefix = data[:r.pos]
    meshes = []
    for _ in range(r.number()):
        start = r.pos
        m = {'name': r.text()}
        for key, fmt in [('v', '3f'), ('n', '3f'), ('t', '4f'), ('uv', '2f'),
                         ('w', '4i4f'), ('bind', '16f')]:
            m[key] = r.array(fmt)
        m['subs'] = [r.array('i') for _ in range(r.number())]
        m['raw'] = data[start:r.pos]
        meshes.append(m)
    pieces = []
    for _ in range(r.number()):
        start = r.pos
        p = {'id': r.text()}
        r.text(); r.text(); r.text()
        p['prefix'] = data[start:r.pos]
        p['parts'] = []
        for _ in range(r.number()):
            start = r.pos
            part = {'skin': r.number(), 'mesh': r.number(), 'mats': r.array('i')}
            if part['skin']:
                part['root'] = r.text()
                part['bones'] = [r.text() for _ in range(r.number())]
            else:
                part['bone'] = r.text()
                # Rigid matrices are column-major; bind poses are row-major.
                flat = struct.unpack('<16f', r.take(64))
                part['local'] = tuple(flat[c * 4 + row] for row in range(4) for c in range(4))
            part['raw'] = data[start:r.pos]
            p['parts'].append(part)
        start = r.pos
        for _ in range(r.number()):
            r.number(); r.array('i'); r.take(64)
        for _ in range(r.number()):
            shape = r.number()
            r.take(76)
            r.take(12 if shape in (0, 1) else 4)
        p['suffix'] = data[start:r.pos]
        pieces.append(p)
    if r.pos != len(data):
        raise ValueError('Unexpected trailing data')
    return prefix, meshes, pieces


def inverse(m):
    rows = [[float(m[r * 4 + c]) for c in range(4)] +
            [float(r == c) for c in range(4)] for r in range(4)]
    for c in range(4):
        pivot = max(range(c, 4), key=lambda r: abs(rows[r][c]))
        rows[c], rows[pivot] = rows[pivot], rows[c]
        divisor = rows[c][c]
        if abs(divisor) < 1e-10:
            raise ValueError('Singular bind pose')
        rows[c] = [x / divisor for x in rows[c]]
        for r in range(4):
            if r != c:
                factor = rows[r][c]
                rows[r] = [a - factor * b for a, b in zip(rows[r], rows[c])]
    return tuple(x for row in rows for x in row[4:])


def multiply(a, b):
    return tuple(sum(a[r * 4 + k] * b[k * 4 + c] for k in range(4))
                 for r in range(4) for c in range(4))


def point(m, v, w=1):
    return tuple(sum(m[r * 4 + c] * v[c] for c in range(3)) + m[r * 4 + 3] * w
                 for r in range(3))


def direction(m, v):
    value = point(m, v, 0)
    length = math.sqrt(sum(x * x for x in value)) or 1
    return tuple(x / length for x in value)


class Nearest:
    """Small balanced KD tree for offline skin-weight transfer."""
    def __init__(self, vertices):
        self.vertices = vertices
        def build(indices, depth):
            if not indices:
                return None
            axis = depth % 3
            indices.sort(key=lambda i: vertices[i][axis])
            mid = len(indices) // 2
            return (indices[mid], axis, build(indices[:mid], depth + 1),
                    build(indices[mid + 1:], depth + 1))
        self.root = build(list(range(len(vertices))), 0)

    def find(self, v):
        best = [float('inf'), 0]
        def visit(node):
            if node is None:
                return
            index, axis, left, right = node
            p = self.vertices[index]
            distance = sum((a - b) ** 2 for a, b in zip(p, v))
            if distance < best[0]:
                best[:] = distance, index
            delta = v[axis] - p[axis]
            near, far = (left, right) if delta < 0 else (right, left)
            visit(near)
            if delta * delta <= best[0]:
                visit(far)
        visit(self.root)
        return best[1]


def encode_mesh(m):
    result = text(m['name'])
    for key, fmt in [('v', '3f'), ('n', '3f'), ('t', '4f'), ('uv', '2f'),
                     ('w', '4i4f'), ('bind', '16f')]:
        result += array(fmt, m[key])
    return result + integer(len(m['subs'])) + b''.join(array('i', s) for s in m['subs'])


def convert(data):
    prefix, meshes, pieces = parse(data)
    leather = {p['id'].split('.', 1)[1]: p for p in pieces if p['id'].startswith('leather.')}
    trees = {}
    converted = 0
    for p in pieces:
        if not p['id'].startswith('metal.'):
            continue
        slot = p['id'].split('.', 1)[1]
        candidates = [part for part in leather[slot]['parts'] if part['skin']]
        if not candidates and slot.startswith('tasset_'):
            # Leather skirts are rigid; use the lower torso's existing skin.
            candidates = [part for part in leather['chest']['parts'] if part['skin']]
        if not candidates:
            # Helmets have no leather skin. Keep their authored rigid attachment;
            # inventing head weights from torso vertices would be incorrect.
            continue
        ref = max(candidates, key=lambda part: len(meshes[part['mesh']]['v']))
        skin = meshes[ref['mesh']]
        if len(skin['w']) != len(skin['v']) or len(skin['bind']) != len(ref['bones']):
            raise ValueError('Incomplete leather reference skin: ' + p['id'])
        if ref['mesh'] not in trees:
            trees[ref['mesh']] = Nearest(skin['v'])
        tree = trees[ref['mesh']]
        for part in p['parts']:
            if part['skin']:
                continue
            if part['bone'] not in ref['bones']:
                raise ValueError('No leather bind pose for ' + part['bone'])
            # Recover the plate's authored model-space rest geometry before
            # transferring weights. Do NOT use the avatar's current animated pose.
            bind = skin['bind'][ref['bones'].index(part['bone'])]
            model = multiply(inverse(bind), part['local'])
            original = meshes[part['mesh']]
            m = dict(original)
            m['name'] += ' leather-fit'
            m['v'] = [point(model, v) for v in original['v']]
            m['n'] = [direction(model, v) for v in original['n']]
            m['t'] = [direction(model, v[:3]) + (v[3],) for v in original['t']]
            m['w'] = [skin['w'][tree.find(v)] for v in m['v']]
            m['bind'] = skin['bind']
            m['raw'] = encode_mesh(m)
            part['mesh'] = len(meshes)
            meshes.append(m)
            part['skin'] = 1
            part['root'], part['bones'] = ref['root'], ref['bones']
            part['raw'] = (integer(1) + integer(part['mesh']) + array('i', part['mats']) +
                           text(part['root']) + integer(len(part['bones'])) +
                           b''.join(text(bone) for bone in part['bones']))
            converted += 1
    output = prefix + integer(len(meshes)) + b''.join(m['raw'] for m in meshes)
    output += integer(len(pieces))
    for p in pieces:
        output += p['prefix'] + integer(len(p['parts']))
        output += b''.join(part['raw'] for part in p['parts']) + p['suffix']
    return output, converted


if __name__ == '__main__':
    root = Path(__file__).resolve().parents[1]
    paths = [root / 'pchost/armor.pack', root / 'quest/ArmorRestored.armor.pack']
    original = paths[0].read_bytes()
    if paths[1].read_bytes() != original:
        raise SystemExit('PC and Quest packs differ; refusing to overwrite either')
    output, count = convert(original)
    parse(output)  # Validate before replacing either resource.
    for path in paths:
        path.write_bytes(output)
    print('Re-rigged', count, 'worn metal plates; both client resources updated.')
