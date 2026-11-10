"""Hebios binary integration contracts; not a Unity runtime/fit test."""
import hashlib,json,math,struct,unittest
from pathlib import Path
from tools.skin_metal import Reader,parse,point
from tools.bench_nail_geometry import plans
ROOT=Path(__file__).resolve().parents[1]

class HebiosIntegrationTests(unittest.TestCase):
 @classmethod
 def setUpClass(cls):
  cls.data=(ROOT/'pchost/armor.pack').read_bytes()
  cls.prefix,cls.meshes,cls.pieces=parse(cls.data)
 def test_slots_costs_and_triangle_conservation(self):
  manifest=json.loads((ROOT/'crafting/hebios-runtime-manifest.json').read_text())
  self.assertEqual(32,len(self.pieces));self.assertEqual(8,len(manifest))
  self.assertEqual(24154,sum(r['triangles'] for r in manifest))
  native={r['prefabName']:r for r in json.loads((ROOT/'crafting/hebios-jig-plans.json').read_text())['entries']}
  for slot,suffix in [('helmet','Helmet'),('chest','Cuirass'),('shoulder_l','Pauldron_Left'),('shoulder_r','Pauldron_Right'),('gauntlet_l','Bracer_Left'),('gauntlet_r','Bracer_Right')]:
   r=next(r for r in manifest if r['id']=='hebios.'+slot)
   self.assertEqual(native['ATT_HebiosArmor_'+suffix]['sources'],r['sources'])
  hips=[r for r in manifest if 'tasset' in r['id']]
  self.assertEqual(3408,sum(r['triangles'] for r in hips))
 def test_textures_decoded_and_material_indices_bounded(self):
  r=Reader(self.prefix);r.take(8);textures=[]
  for _ in range(r.number()):
   name=r.text();fmt,w,h,mips=struct.unpack('<4i',r.take(16));raw=r.take(r.number());textures.append(name)
   if name.startswith('Hebios/'):
    self.assertEqual((4,1),(fmt,mips));self.assertEqual(w*h*4,len(raw))
  count=0;references=set()
  for _ in range(r.number()):
   name=r.text();tex,normal,metal,occlusion,*color=struct.unpack('<4i6f',r.take(40))
   if name.startswith('Hebios/'):
    count+=1;self.assertTrue(all(math.isfinite(v) for v in color));self.assertTrue(-1<=tex<len(textures))
    if tex>=0:references.add(tex)
  self.assertGreater(count,20)
  self.assertEqual({i for i,n in enumerate(textures) if n.startswith('Hebios/')},references)
 def test_bone_helpers_weights_and_neutral_glove(self):
  skinned=[]
  for piece in self.pieces[24:]:
   for part in piece['parts']:
    mesh=self.meshes[part['mesh']]
    if part['skin']:
     skinned.append(part);self.assertEqual(len(mesh['v']),len(mesh['w']))
     self.assertEqual(len(part['bones']),len(mesh['bind']))
     for weights in mesh['w']:
      self.assertAlmostEqual(1,sum(weights[4:]),places=5)
      self.assertTrue(all(0<=b<len(part['bones']) for b in weights[:4]))
     self.assertEqual('255',mesh['name'].split('/')[2])
    else:self.assertTrue(part['bone'].endswith('_jnt'))
  self.assertEqual(2,len(skinned))
 def test_every_worn_triangle_has_valid_input_owner(self):
  for piece in self.pieces[24:]:
   ownercounts={}
   for part in piece['parts']:
    m=self.meshes[part['mesh']];prefix,slot,group,count,name=m['name'].split('/')
    self.assertEqual(piece['id'].split('.')[1],slot)
    self.assertEqual(sum(len(s)//3 for s in m['subs']),int(count))
    group=int(group);self.assertIn(group,range(4) if slot=='helmet' else (*range(3),255))
    ownercounts[group]=ownercounts.get(group,0)+int(count)
   self.assertTrue({0,1,2}.issubset(ownercounts))
 def test_compact_gloves_have_no_original_skeleton_space_vertices(self):
  for name,parts in plans(ROOT/'crafting/construction.bin'):
   if name.startswith('hebios.gauntlet'):
    for _,vertices,_ in parts:
     self.assertLess(max(abs(v) for p in vertices for v in p),.6)
 def test_shared_id_map_skips_stand_and_wyrm_recipes(self):
  ids=[52830+i+(i>=24) for i in range(32)]
  self.assertEqual(32,len(set(ids)));self.assertNotIn(52854,ids)
  for platform in ('pchost','quest'):
   code=(ROOT/platform/'Catalog.cs').read_text('utf-8-sig')
   self.assertIn('piece >= 24 ? 1 : 0',code)
   self.assertIn('number == 52854U',code)
  code=(ROOT/'shared/ArmorCrafting.cs').read_text()
  self.assertEqual(2,code.count('if (i >= 16 && i < 24) continue;'))
  self.assertIn('Catalog.Number((int)(hash-RecipeStart))',code)
  self.assertIn('if (recipe != null) bases.Add(recipe)',code)
  self.assertIn('style < Catalog.Count / 8',(ROOT/'shared/ArmorStand.cs').read_text())
 def test_native_sources_and_provenance_include_leather_closure(self):
  code=(ROOT/'shared/ArmorCraftingData.cs').read_text()
  self.assertIn('hash >= 52855U && hash <= 52862U',code)
  self.assertIn('result < 52838U || result >= 52855U ? 63204U : 62594U',code)
  self.assertIn('parts[0] != "leather" ? 50988U : 47760U',code)
  manifest=json.loads((ROOT/'crafting/construction-manifest.json').read_text())
  self.assertEqual(24,len(manifest['recipes']))
  self.assertEqual(hashlib.sha256((ROOT/'crafting/construction.bin').read_bytes()).hexdigest().upper(),manifest['pack_sha256'])

 def test_existing_saved_recipe_identity_is_preserved(self):
  r=Reader((ROOT/'crafting/construction.bin').read_bytes());header=r.take(40);r.take(4)
  for _ in range(16):
   r.text();r.text()
   for _ in range(r.number()):
    r.take(45);r.take(r.number()*32)
    for _ in range(r.number()):r.take(r.number()*4)
  original=header+struct.pack('<i',16)+(ROOT/'crafting/construction.bin').read_bytes()[44:r.pos]
  self.assertEqual('d003c7633163f4784922b63ae29e25aa3555855853c5996a017c654bb66b5ca7',hashlib.sha256(original).hexdigest())
  self.assertIn('index < 16 ? LegacyRecipeFingerprint : ArmorCrafting.Fingerprint',(ROOT/'pchost/ArmorCraftingServer.cs').read_text())
