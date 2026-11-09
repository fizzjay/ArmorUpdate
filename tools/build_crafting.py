"""Export verified construction data without touching either armor.pack.

Requires development-only dnfile and UnityPy. No DLL code is executed. This
exports the 14 original Leather/Metal plans; combined tassets are reference-only
until their topology and nail poses have been adapted for eight-slot recipes.
Run with .venv/bin/python tools/build_crafting.py.
"""
import argparse
import hashlib
import io
import json
import math
from pathlib import Path
import struct

try:
    from tools.inspect_crafting_reference import verify
except ModuleNotFoundError:
    from inspect_crafting_reference import verify

ROOT = Path(__file__).resolve().parents[1]
SLOTS = ('helmet', 'chest', 'shoulder_l', 'shoulder_r', 'gauntlet_l', 'gauntlet_r', 'tassets')
GLYPHS = ('Helmet', 'Cuirass', 'Pauldron_Left', 'Pauldron_Right', 'Bracer_Left', 'Bracer_Right', 'Tassets')


def finite(values):
    if not all(math.isfinite(v) for v in values):
        raise ValueError('Non-finite geometry')


def split_mesh(vertices, normals, uv, semantics, submeshes, sources):
    """Partition by authored role/group, retaining original submesh indices.

    Reject crossing tags rather than assigning a whole triangle to its first
    vertex. Retain only used vertices in each ingredient mesh.
    """
    count = len(vertices)
    if not count or any(len(a) != count for a in (normals, uv, semantics)):
        raise ValueError('Missing vertex attributes or semantic UV4')
    keys = []
    for v, n, u, tag in zip(vertices, normals, uv, semantics):
        finite((*v, *n, *u, *tag))
        if len(tag) != 4 or tag[2:] != (2.0, 47.0) and list(tag[2:]) != [2.0, 47.0]:
            raise ValueError('Invalid semantic layout marker')
        role, group = tag[:2]
        if role not in (1, 2) or group != int(group) or not 0 <= group < 32:
            raise ValueError('Invalid semantic role/group')
        keys.append((int(role), int(group)))
    primary = sum(s in (50988, 47760) for s in sources)
    expected = [(1, i) for i in range(primary)] + [(2, i) for i in range(len(sources) - primary)]
    if set(keys) != set(expected):
        raise ValueError('Semantic groups do not match ingredient sources')
    triangles = {key: [[] for _ in submeshes] for key in expected}
    for sub_index, sub in enumerate(submeshes):
        for tri in sub:
            if len(tri) != 3 or any(i < 0 or i >= count for i in tri):
                raise ValueError('Invalid triangle')
            tags = {keys[i] for i in tri}
            if len(tags) != 1:
                raise ValueError('Triangle crosses ingredient groups')
            triangles[keys[tri[0]]][sub_index].append(tri)
    parts = []
    for key in expected:
        subs = triangles[key]
        used = sorted({i for sub in subs for tri in sub for i in tri})
        if not used:
            raise ValueError('Empty ingredient mesh')
        remap = {old: new for new, old in enumerate(used)}
        parts.append(dict(vertices=[vertices[i] for i in used],
                          normals=[normals[i] for i in used], uv=[uv[i] for i in used],
                          submeshes=[[[remap[i] for i in tri] for tri in sub] for sub in subs]))
    return parts


def validate_plan(plan):
    n = len(plan['sources'])
    for i, (parent, level) in enumerate(zip(plan['parents'], plan['levels'])):
        if parent < -1 or parent >= n or parent == i or level < 0:
            raise ValueError('Invalid plan hierarchy')
        seen = {i}
        while parent != -1:
            if parent in seen:
                raise ValueError('Cyclic plan hierarchy')
            seen.add(parent)
            parent = plan['parents'][parent]
    for field in ('nailPositions', 'jointPoints', 'supportPoints', 'nailRotations'):
        for value in plan[field]:
            finite(value.values())
    for rotation in plan['nailRotations']:
        length = sum(v * v for v in rotation.values())
        if abs(length - 1) > .01:
            raise ValueError('Non-unit nail rotation')
    finite(plan['jointGaps'])


def encode(records, model_digest):
    out = io.BytesIO()
    out.write(b'ACRF')
    def number(value):
        out.write(struct.pack('<i', value))
    def text(value):
        data = value.encode('utf8'); number(len(data)); out.write(data)
    number(1)
    out.write(bytes.fromhex(model_digest))
    number(len(records))
    for record in records:
        text(record['id'])
        text(record['glyph'])
        plan = record['plan']
        number(len(record['parts']))
        for i, part in enumerate(record['parts']):
            out.write(struct.pack('<Iii?', plan['sources'][i], plan['parents'][i],
                                  plan['levels'][i], plan['requiresNails'][i]))
            pos, rot = plan['nailPositions'][i], plan['nailRotations'][i]
            out.write(struct.pack('<7fi', *(pos[k] for k in 'xyz'),
                                  *(rot[k] for k in 'xyzw'), plan['clearanceDegrees'][i]))
            number(len(part['vertices']))
            for v, n, uv in zip(part['vertices'], part['normals'], part['uv']):
                out.write(struct.pack('<8f', *v, *n, *uv))
            number(len(part['submeshes']))
            for sub in part['submeshes']:
                number(len(sub) * 3)
                for tri in sub:
                    out.write(struct.pack('<3i', *tri))
    return out.getvalue()


def build(dll):
    import dnfile
    import UnityPy
    from UnityPy.helpers.MeshHelper import MeshHandler
    pe = dnfile.dnPE(str(dll))
    try:
        resources = {str(r.name): r.data for r in pe.net.resources}
    finally:
        pe.close()
    report = verify(resources)
    plans = {p['prefabName']: p for p in json.loads(resources['PlayerArmor.JigPlans'])['entries']}
    env = UnityPy.load(resources['PlayerArmor.Model'])
    meshes = {}
    for obj in env.objects:
        if obj.type.name == 'Mesh':
            mesh = obj.read()
            if mesh.m_Name.endswith('_pickup'):
                if mesh.m_Name in meshes:
                    raise ValueError('Duplicate mesh name')
                meshes[mesh.m_Name] = mesh
    records = []
    for style, glyph_style in (('Leather', 'Leather'), ('Metal', 'Iron')):
        for slot, (slot_name, suffix) in enumerate(zip(SLOTS, GLYPHS)):
            plan = plans['ATT_' + glyph_style + 'Armor_' + suffix]
            validate_plan(plan)
            handler = MeshHandler(meshes[style + '_' + str(slot) + '_pickup'])
            handler.process()
            parts = split_mesh(handler.m_Vertices, handler.m_Normals, handler.m_UV0,
                               handler.m_UV4, handler.get_triangles(), plan['sources'])
            records.append(dict(id=style.lower() + '.' + slot_name,
                                glyph='ATT_' + glyph_style + 'Armor_' + suffix + '.png',
                                parts=parts, plan=plan))
    data = encode(records, report['model_sha256'])
    summary = dict(schema=1, model_sha256=report['model_sha256'],
                   pack_sha256=hashlib.sha256(data).hexdigest().upper(),
                   recipes=[dict(id=r['id'], reference_only=r['id'].endswith('.tassets'),
                                 sources=r['plan']['sources'],
                                 vertex_counts=[len(p['vertices']) for p in r['parts']])
                            for r in records])
    return data, summary


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--dll', type=Path, default=ROOT / 'testothermod/PlayerArmor.CraftingHouse.dll')
    parser.add_argument('--output', type=Path, default=ROOT / 'crafting')
    args = parser.parse_args()
    data, summary = build(args.dll)
    args.output.mkdir(parents=True, exist_ok=True)
    (args.output / 'construction.bin').write_bytes(data)
    (args.output / 'construction-manifest.json').write_text(json.dumps(summary, indent=2) + '\n')
    print('Exported', len(summary['recipes']), 'plans;', len(data), 'bytes; no worn armor changed')


if __name__ == '__main__':
    main()
