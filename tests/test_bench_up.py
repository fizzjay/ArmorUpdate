"""Construction-only bench orientation; does not simulate Unity tabletop physics."""
import json,math,unittest
from pathlib import Path
from tools.bench_nail_geometry import plans,orientation,dot
from tools.inspect_hebios import audit
ROOT=Path(__file__).resolve().parents[1]

class BenchUpTests(unittest.TestCase):
    def test_all_required_nails_have_upward_full_assembly_clearance(self):
        count=0
        turns={'metal.gauntlet_l','metal.tasset_l','metal.tasset_r','hebios.helmet','hebios.chest'}
        for name,parts in plans(ROOT/'crafting/construction.bin'):
            up,selected=orientation(parts)
            self.assertEqual((0,0,1) if name in turns else (0,1,0),up)
            count+=len(selected)
            for point,normal in selected:self.assertGreaterEqual(dot(normal,up),.25)
            # After rotation, y is the source projection onto the chosen up axis.
            lowest=min(dot(v,up) for _,vertices,_ in parts for v in vertices)
            lift=.06-lowest
            for _,vertices,_ in parts:
                for v in vertices:self.assertGreaterEqual(dot(v,up)+lift,.06-1e-6)
        self.assertEqual(61,count)

    def test_runtime_rotates_all_inputs_and_nails_but_not_worn(self):
        code=(ROOT/'shared/ArmorCrafting.cs').read_text()
        for token in ('JigTransform(plan,out jigRotation,out jigLift)',
                      'vertices[i] = jigRotation*', 'normals[i] = jigRotation*',
                      'Quaternion turn=jigRotation*','Vector3.Dot(candidate.Normal,JigUp(plan))<.25f',
                      '.06f-minY','worn-sections1-benchup2'):
            self.assertIn(token,code)
        split=code[code.index('internal static Mesh SplitWorn'):code.index('private static void ConfigureDarksteelChannel')]
        self.assertNotIn('JigTransform',split)

class HebiosAssetTests(unittest.TestCase):
    def test_supplied_assets_and_embedded_pngs_are_verified(self):
        report=audit()
        self.assertEqual(report,json.loads((ROOT/'crafting/hebios-import-audit.json').read_text()))
        self.assertEqual(7,len(report['files']))
        for entry in report['files']:
            self.assertGreater(entry['mesh_count'],0)
            self.assertGreater(entry['triangles'],0)
            self.assertTrue(entry['attachment_helpers'])
            self.assertGreaterEqual(len(entry['embedded_textures']),2)
        self.assertIn('not registered',report['status'])
