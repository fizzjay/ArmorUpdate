"""Source-contract checks only: native cosmetic packing needs game validation."""
from pathlib import Path
import re
import unittest
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]


def source(path):
    return (ROOT / path).read_text(encoding='utf-8-sig')


class CosmeticCoverageTests(unittest.TestCase):
    def test_native_slot_ranges_in_eight_slot_order(self):
        expected = [201, 11, 81, 91, 121, 131, 51, 51]
        for platform in ('pchost', 'quest'):
            code = source(platform + '/CosmeticCoverage.cs')
            body = code.split('internal static bool CoversSlot')[1].split('private static bool Between')[0]
            terms = re.split(r'\(mask & (\d+)U\)', body)[1:]
            def covered(mask, slot):
                for bits, clause in zip(terms[::2], terms[1::2]):
                    ranges = [(int(a), int(b)) for a, b in re.findall(r'Between\(slot, (\d+), (\d+)\)', clause)]
                    singles = [int(a) for a in re.findall(r'slot == (\d+)', clause)]
                    if mask & int(bits) and (any(a <= slot <= b for a, b in ranges) or slot in singles):
                        return True
                return False
            for index, slot in enumerate(expected):
                self.assertTrue(covered(1 << index, slot), (platform, index, slot))
                self.assertFalse(covered(0, slot))
            self.assertFalse(covered(1 << 2, 91))  # left/right shoulders independent
            self.assertFalse(covered(1 << 4, 131))  # left/right arms independent
            self.assertFalse(covered(255, 900))
            self.assertFalse(covered(255, 200))
            self.assertTrue(covered(128, 501))  # right tasset is not dropped as in 7-slot mask

    def test_only_safe_native_compact_overlays_are_used(self):
        for platform in ('pchost', 'quest'):
            code = source(platform + '/CosmeticCoverage.cs')
            for fragment in ('part.IsTextureSharingForced', 'SkinMeshPart', 'PlayerCosmeticDefinition',
                             'part.ImpededLods', 'compact.Length < lods.Length',
                             'if (compact[i] == null) return false;', 'entry.DisablerCount > 0',
                             'IsImpeded = true',
                             'previous.Primary == original.Primary'):
                self.assertIn(fragment, code)
            self.assertNotIn('renderer.enabled', code)
            self.assertNotRegex(code, r'entry\.Arguments\s*=(?!=)')
            self.assertNotRegex(code, r'DisablerCount\s*=(?!=)')

    def test_quest_uses_direct_native_casts_and_pointer_ownership(self):
        code = source('quest/CosmeticCoverage.cs')
        helper = code.split('private static T Native<T>')[1].split('private static void Change')[0]
        self.assertIn('where T : Il2CppObjectBase', helper)
        self.assertIn('native.TryCast<T>()', helper)
        self.assertNotIn('MakeGenericMethod', helper)
        self.assertIn('left.Pointer == right.Pointer', helper)
        self.assertIn('Same(input.Arguments, previous)', code)
        self.assertIn('Same(input.Arguments, overlay.Value)', code)
        self.assertIn('il2cpp_class_get_name', code)
        self.assertIn('input.Arguments == overlay.Value', source('pchost/CosmeticCoverage.cs'))

    def test_platform_specific_collection_bridge(self):
        pc = source('pchost/CosmeticCoverage.cs')
        self.assertIn('using Alta.Character;', pc)
        quest = source('quest/CosmeticCoverage.cs')
        self.assertNotIn('in manager.Handlers)', quest)
        self.assertNotIn('new Il2CppReferenceArray<ICustomizationHandler>', quest)
        self.assertIn('new Il2CppSystem.Collections.Generic.List<ICustomizationHandler>()', quest)
        bridge = source('quest/NativeEnumerable.cs')
        self.assertIn('TryCast<Il2CppSystem.Collections.Generic.ICollection<T>>()', bridge)
        self.assertIn('collection.CopyTo(copy, 0)', bridge)
        self.assertNotIn('MoveNext(', bridge)
        self.assertNotIn('GetEnumerator(', bridge)
        self.assertIn('items.Values.CopyTo(groups, 0)', quest)
        self.assertNotIn('in items.Values)', quest)
        self.assertIn('manager.Pointer != next.Pointer', quest)
        self.assertNotIn('if (manager != next)', quest)
        self.assertNotIn('HashedGeneralValue<PhysicalMaterial>.All',
                         source('quest/WornMaterials.cs'))
        tree = ET.parse(ROOT / 'quest/ArmorRestored.csproj')
        ns = {'m': 'http://schemas.microsoft.com/developer/msbuild/2003'}
        self.assertEqual(sum(x.attrib['Include'] == 'NativeEnumerable.cs'
                             for x in tree.findall('.//m:Compile', ns)), 1)

    def test_clients_register_and_release_coverage(self):
        for platform in ('pchost', 'quest'):
            tree = ET.parse(ROOT / platform / 'ArmorRestored.csproj')
            ns = {'m': 'http://schemas.microsoft.com/developer/msbuild/2003'}
            files = [x.attrib['Include'] for x in tree.findall('.//m:Compile', ns)]
            self.assertEqual(files.count('CosmeticCoverage.cs'), 1)
        for path in ('pchost/ClientArmor.cs', 'quest/Draw.cs'):
            code = source(path)
            self.assertIn('dressed.Coverage.Update(dressed.Root, dressed.Shown);', code)
            self.assertGreaterEqual(code.count('Coverage.Clear();'), 3)
        self.assertIn('new CosmeticCoverage()', source('quest/Dressed.cs'))
        quest = source('quest/CosmeticCoverage.cs')
        self.assertIn('handlers.Cast<Il2CppSystem.Collections.Generic.IEnumerable<ICustomizationHandler>>()', quest)
        self.assertIn('NativeEnumerable.Items(manager.Handlers)', quest)
        self.assertIn('Native<TwoColors>(entry.Arguments)', quest)
        self.assertIn('PropertyInfo property', quest)  # generated IL2CPP properties

class FabricFitContracts(unittest.TestCase):
    def test_both_platforms_use_same_reference_envelope_and_arithmetic(self):
        pc=source('pchost/CosmeticCoverage.cs');quest=source('quest/CosmeticCoverage.cs')
        start='        internal static bool FitFabricVertex'
        self.assertEqual(pc[pc.index(start):],quest[quest.index(start):])
        rows=re.findall(r'\{([0-9,]+)\}',pc[pc.index('byte[,] FabricRadii'):])
        table=[[int(v) for v in row.split(',')] for row in rows]
        self.assertEqual(11,len(table));self.assertTrue(all(len(row)==17 for row in table))
        self.assertEqual([137,138,143,148,155,159,161,163,158,155,146,144,138,135,131,128,128],table[0])
        self.assertEqual([136,145,156,167,180,198,209,205,196,192,188,171,150,135,124,117,114],table[-1])

    def test_reference_compression_is_bounded_symmetric_and_torso_only(self):
        import math,random
        text=source('pchost/CosmeticCoverage.cs')
        rows=re.findall(r'\{([0-9,]+)\}',text[text.index('byte[,] FabricRadii'):])
        table=[[int(v) for v in row.split(',')] for row in rows]
        def clamp(x,a,b):return max(a,min(x,b))
        def feather(x):x=clamp(x,0,1);return x*x*(3-2*x)
        def fit(p):
            x,y,z=p;ax=abs(x)
            if not all(math.isfinite(v) for v in p) or z<=.995 or z>=1.355 or ax>=.205:return p
            rr=x*x+y*y
            if rr<=.0064 or rr>=.25:return p
            r=clamp((z-1.005)/.035,0,10);c=clamp((math.atan2(y,ax)/math.pi+.5)*16,0,16)
            ri=min(int(r),9);ci=min(int(c),15)
            lerp=lambda a,b,t:a+(b-a)*t
            a=lerp(table[ri][ci],table[ri][ci+1],c-ci);b=lerp(table[ri+1][ci],table[ri+1][ci+1],c-ci)
            target=lerp(a,b,r-ri)*.001+.008;radius=math.sqrt(rr)
            if radius<=target:return p
            d=min(radius-target,.065)*feather((z-.995)/.085)*feather((1.355-z)/.06)*feather((.205-ax)/.052)
            if d<.0001:return p
            s=(radius-d)/radius;return (x*s,y*s,z)
        for p in [(0,.3,.995),(0,.3,1.355),(.205,.2,1.1),(0,.05,1.1),(0,.2,2),(.3,0,1.2)]:
            self.assertEqual(p,fit(p))
        self.assertNotEqual((0,.25,1.15),fit((0,.25,1.15)))
        rng=random.Random(2026)
        for _ in range(2000):
            p=(rng.uniform(-.3,.3),rng.uniform(-.4,.4),rng.uniform(.8,1.5));q=fit(p)
            before=math.hypot(p[0],p[1]);after=math.hypot(q[0],q[1])
            self.assertEqual(p[2],q[2]);self.assertLessEqual(after,before+1e-10)
            self.assertLessEqual(before-after,.065+1e-10)
            mirrored=fit((-p[0],p[1],p[2]))
            self.assertAlmostEqual(-q[0],mirrored[0]);self.assertAlmostEqual(q[1],mirrored[1])
        for token in ('z<=.995f || z>=1.355f || x>=.205f','+.008f','Mathf.Min(radius-target,.065f)',
                      'float.IsNaN','float.IsInfinity','FabricFeather'):
            self.assertIn(token,text)

    def test_fitting_is_pack_local_reversible_and_never_edits_saved_assets(self):
        for platform in ('pchost','quest'):
            text=source(platform+'/CosmeticCoverage.cs')
            for token in ('UpdateFabric(current,handlers,mask)', 'ClearFabric();',
                          'Covered(entry,mask)', '(mask&2U)==0U', 'entry.DisablerCount>0',
                          'slot>=301 && slot!=331 && slot!=501 && slot!=511',
                          'Between(slot,21,45) || Between(slot,191,195)',
                          'input.Arguments.IsImpeded','fabricUsers[key]=count+1','fabricUsers[key]=count-1',
                          'RepackFabric(handler)', 'if(copy!=null) __0=copy;',
                          'Mesh mesh=UnityEngine.Object.Instantiate(original)',
                          'mesh.vertices=vertices;mesh.RecalculateBounds()',
                          'ArmorCrafting.Set(clone,"lods"','ArmorCrafting.Set(clone,"androidLods"',
                          'if(fabricCache.Count>=96) return null;', 'if(!original.isReadable) throw'):
                self.assertIn(token,text)
            self.assertNotIn('original.vertices=',text)
            self.assertNotIn('source.Lods=',text)
            self.assertNotIn('ArmorCrafting.Set(asset,',text)
            self.assertNotIn('entry.Arguments=',text)
            self.assertIn('if(clone!=null) UnityEngine.Object.Destroy(clone)',text)
            self.assertIn('foreach(var mesh in ownedMeshes) UnityEngine.Object.Destroy(mesh)',text)

    def test_quest_uses_native_clones_collections_and_pointer_keys(self):
        text=source('quest/CosmeticCoverage.cs')
        for token in ('input.Pointer.ToInt64()', 'NativeEnumerable.Items(parts)',
                      'source.TryCast<CosmeticMeshPart>()','fitted.Cast<ICosmeticMeshPartBase>()',
                      'map.Cast<Il2CppSystem.Collections.Generic.IDictionary',
                      'new Il2CppReferenceArray<Mesh>(meshes)',
                      'ref Il2CppSystem.Collections.Generic.Dictionary<CosmeticPacker.PartId,ICosmeticMeshPartBase> __0'):
            self.assertIn(token,text)
