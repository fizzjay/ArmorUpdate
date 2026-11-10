"""Inspect the supplied reference DLL without loading or executing it.

Development only: pip install dnfile. Optional --extract writes the verified
model and jig archive to a scratch directory, never into the runtime pack.
"""
import argparse
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def verify(resources):
    model = resources['PlayerArmor.Model']
    archive = json.loads(resources['PlayerArmor.JigPlans'])
    digest = hashlib.sha256(model).hexdigest().upper()
    if archive.get('schemaVersion') != 1:
        raise ValueError('Unsupported jig schema')
    if archive.get('bundleSha256', '').upper() != digest:
        raise ValueError('Jig archive does not match embedded model')
    entries = archive['entries']
    names = [entry['prefabName'] for entry in entries]
    if len(names) != len(set(names)):
        raise ValueError('Duplicate jig prefab names')
    fields = ('levels', 'parents', 'requiresNails', 'nailPositions',
              'jointPoints', 'supportPoints', 'nailRotations', 'jointGaps',
              'clearanceDegrees')
    for entry in entries:
        count = len(entry['sources'])
        if not 1 <= count <= 32:
            raise ValueError('Invalid ingredient count: ' + entry['prefabName'])
        if any(len(entry[field]) != count for field in fields):
            raise ValueError('Mismatched jig arrays: ' + entry['prefabName'])
    glyphs = []
    for name, data in sorted(resources.items()):
        if not name.startswith('PlayerArmor.Glyphs.ATT_'):
            continue
        local = ROOT / 'RecipeGlyphs' / name.removeprefix('PlayerArmor.Glyphs.')
        if local.exists():
            if local.read_bytes() != data:
                raise ValueError('Local glyph differs from reference: ' + local.name)
            glyphs.append(local.name)
    return {
        'model_sha256': digest,
        'model_bytes': len(model),
        'jig_count': len(entries),
        'matching_local_glyphs': glyphs,
        'recipes': [{'prefab': e['prefabName'], 'sources': e['sources']}
                    for e in entries],
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('dll', nargs='?', type=Path,
                        default=ROOT / 'testothermod/PlayerArmor.CraftingHouse.dll')
    parser.add_argument('--extract', type=Path)
    args = parser.parse_args()
    import dnfile
    pe = dnfile.dnPE(str(args.dll))
    try:
        resources = {str(r.name): r.data for r in pe.net.resources}
        report = verify(resources)
        report['dll_sha256'] = hashlib.sha256(args.dll.read_bytes()).hexdigest().upper()
        if args.extract:
            args.extract.mkdir(parents=True, exist_ok=True)
            for resource, filename in [('PlayerArmor.Model', 'model.bundle'),
                                       ('PlayerArmor.JigPlans', 'jig-plans.json')]:
                (args.extract / filename).write_bytes(resources[resource])
        print(json.dumps(report, indent=2))
    finally:
        pe.close()


if __name__ == '__main__':
    main()
