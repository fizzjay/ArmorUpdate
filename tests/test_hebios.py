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
  self.assertEqual(48,len(self.pieces));manifest=[r for r in manifest if r['id'].startswith('hebios.')];self.assertEqual(8,len(manifest))
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
  for piece in self.pieces[24:32]:
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
  for piece in self.pieces[24:32]:
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
  ids=[52830+i+(i>=24) for i in range(48)]
  self.assertEqual(48,len(set(ids)));self.assertNotIn(52854,ids)
  for platform in ('pchost','quest'):
   code=(ROOT/platform/'Catalog.cs').read_text('utf-8-sig')
   self.assertIn('piece >= 24 ? 1 : 0',code)
   self.assertIn('number == 52854U',code)
  code=(ROOT/'shared/ArmorCrafting.cs').read_text()
  self.assertEqual(2,code.count('if (!Craftable(i)) continue;'))
  self.assertIn('Catalog.Number((int)(hash-RecipeStart))',code)
  self.assertIn('if (recipe != null) bases.Add(recipe)',code)
  self.assertIn('style < Catalog.Count / 8',(ROOT/'shared/ArmorStand.cs').read_text())
 def test_native_sources_and_provenance_include_leather_closure(self):
  code=(ROOT/'shared/ArmorCraftingData.cs').read_text()
  self.assertIn('hash >= 52855U && hash <= 52870U',code)
  self.assertIn('result < 52838U || result >= 52855U ? 63204U : 62594U',code)
  self.assertIn('parts[0] != "leather" ? 50988U : 47760U',code)
  manifest=json.loads((ROOT/'crafting/construction-manifest.json').read_text())
  self.assertEqual(32,len(manifest['recipes']))
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

class WoodMushroomIntegrationTests(unittest.TestCase):
 @classmethod
 def setUpClass(cls):
  cls.data=(ROOT/'pchost/armor.pack').read_bytes()
  cls.prefix,cls.meshes,cls.pieces=parse(cls.data)
 def test_two_sets_conserve_all_source_geometry(self):
  from tools.import_hebios import fbx,arr
  for folder,style,offset in [('WoodArmor','wood',32),('mushroomarmor','mushroom',40)]:
   triangles=0
   for file in (ROOT/folder).glob('*.fbx'):
    objects,edges,_,_=fbx(file)
    for _,(kind,name,sub,body) in objects.items():
     if kind=='Geometry':
      ids=arr(body,'PolygonVertexIndex',int);count=0
      for i in ids:
       count+=1
       if i<0:triangles+=count-2;count=0
     if kind=='Model' and sub=='Mesh':
      key=next(k for k,v in objects.items() if v==(kind,name,sub,body))
      self.assertEqual(1,len([a for a,b in edges if b==key and objects[a][0]=='Material']))
   worn=sum(sum(len(sub)//3 for sub in self.meshes[p['mesh']]['subs']) for piece in self.pieces[offset:offset+8] for p in piece['parts'])
   self.assertEqual(triangles,worn)
   self.assertEqual(9714 if style=='wood' else 28298,worn)
   self.assertTrue(all(piece['id'].startswith(style+'.') for piece in self.pieces[offset:offset+8]))
 def test_new_meshes_have_complete_finite_attributes(self):
  for piece in self.pieces[32:]:
   for part in piece['parts']:
    mesh=self.meshes[part['mesh']]
    self.assertTrue(part['bone'].endswith('_jnt'))
    self.assertEqual(len(mesh['v']),len(mesh['n']))
    self.assertEqual(len(mesh['v']),len(mesh['uv']))
    self.assertTrue(all(math.isfinite(v) for attr in ('v','n','uv') for p in mesh[attr] for v in p))
    self.assertTrue(all(0<=index[0]<len(mesh['v']) for sub in mesh['subs'] for index in sub))
 def test_book_policy_keeps_leather_metal_and_adds_wood_only(self):
  craft=(ROOT/'shared/ArmorCrafting.cs').read_text()
  self.assertIn('piece>=0 && (piece<16 || piece>=32 && piece<40)',craft)
  self.assertEqual(2,craft.count('if (!Craftable(i)) continue;'))
  self.assertIn('foreach (var recipe in Recipes) if (recipe != null) bases.Add(recipe)',craft)
  self.assertIn('Set(collection, "pageBases", bases.ToArray())',craft)
  self.assertIn('new Il2CppReferenceArray<Object>(bases.ToArray())',craft)
  active=[i for i in range(48) if i<16 or 32<=i<40]
  self.assertEqual(24,len(active))
  self.assertEqual(list(range(60064,60072)),[60032+i for i in active if i>=32])
  self.assertIn('CreatePage(2)',craft)
 def test_wood_recipe_graphs_match_native_metadata(self):
  metadata={p['prefabName']:p for p in json.loads((ROOT/'crafting/hebios-jig-plans.json').read_text())['entries']}
  raw=(ROOT/'crafting/construction.bin').read_bytes();r=Reader(raw);r.take(40)
  suffixes=['Helmet','Cuirass','Pauldron_Left','Pauldron_Right','Bracer_Left','Bracer_Right','Tassets','Tassets'];wood=0
  for _ in range(r.number()):
   name=r.text();r.text();inputs=[]
   for _ in range(r.number()):
    inputs.append(struct.unpack('<Iii?',r.take(13)));r.take(32);r.take(r.number()*32)
    for _ in range(r.number()):r.take(r.number()*4)
   if not name.startswith('wood.'):continue
   expected=metadata['ATT_WoodlandArmor_'+suffixes[wood]]
   if wood<6:
    self.assertEqual(expected['sources'],[v[0] for v in inputs]);self.assertEqual(expected['parents'],[v[1] for v in inputs])
    self.assertEqual(expected['levels'],[v[2] for v in inputs]);self.assertEqual(expected['requiresNails'],[v[3] for v in inputs])
   else:self.assertEqual([43836,47118,47118],[v[0] for v in inputs])
   wood+=1
  self.assertEqual(8,wood)
 def test_wood_surfaces_keep_input_ownership_and_neutral_hide(self):
  for piece in self.pieces[32:40]:
   owners=set()
   for part in piece['parts']:
    mesh=self.meshes[part['mesh']];fields=mesh['name'].split('/');owner=int(fields[2]);owners.add(owner)
    self.assertEqual('wood',fields[0]);self.assertEqual(sum(len(s)//3 for s in mesh['subs']),int(fields[3]))
    self.assertIn(owner,(0,1,2,3,255))
   self.assertTrue({0,1,2,255}.issubset(owners))
  for platform in ('pchost','quest'):
   text=(ROOT/platform/'WornMaterials.cs').read_text('utf-8-sig')
   for token in ('ApplyWood(uint hash','GetNonAtlasedNonDeformingMaterial(0)','surface.mainTexture=wood.mainTexture',
                 'surface.mainTextureScale=wood.mainTextureScale','UpdateWood(palette.Key,variant)','Woods.Clear()'):
    self.assertIn(token,text)
 def test_loot_policy_and_icons_exclude_wood_from_random_loot(self):
  catalog=(ROOT/'pchost/Catalog.cs').read_text('utf-8-sig')
  self.assertIn('Id="wood", Title="Wood", LootRarity=null',catalog)
  self.assertIn('Id="hebios", Title="Hebios", LootRarity="4. Epic"',catalog)
  self.assertIn('Id="mushroom", Title="Mushroom", LootRarity="3. Rare"',catalog)
  craft=(ROOT/'shared/ArmorCrafting.cs').read_text()
  self.assertIn('if(i>=32 && i<40) continue;',craft)
  self.assertIn('i<32 ? "Hebios" : "Mushroom"',craft)
 def test_decoded_yaml_texture_and_no_duplicate_generated_pixels(self):
  r=Reader(self.prefix);r.take(8);seen=set();white=False
  for _ in range(r.number()):
   name=r.text();header=r.take(16);fmt,w,h,mips=struct.unpack('<4i',header);data=r.take(r.number())
   if '/' not in name:continue
   self.assertEqual(4,fmt);self.assertEqual(w*h*4,len(data))
   digest=hashlib.sha256(header+data).digest();self.assertNotIn(digest,seen);seen.add(digest)
   if w==h==4 and data==b'\xff'*64:white=True
  self.assertTrue(white)
  self.assertLess(len(self.data),60*1024*1024)
 def test_wood_provenance_and_loot_dismantle_guard(self):
  data=(ROOT/'shared/ArmorCraftingData.cs').read_text()
  self.assertIn('result >= 52863U ? 47118U',data)
  self.assertIn('result >= 52863U ? 43836U',data)
  server=(ROOT/'pchost/ArmorCraftingServer.cs').read_text()
  self.assertIn('source == 47118U',server)
  self.assertIn('if (!ArmorCrafting.Craftable(index)) return;',server)
 def test_clients_share_resources_without_new_project_entries(self):
  self.assertEqual(self.data,(ROOT/'quest/ArmorRestored.armor.pack').read_bytes())
  for platform in ('pchost','quest'):
   project=(ROOT/platform/'ArmorRestored.csproj').read_text('utf-8-sig')
   self.assertNotIn('WoodSurface.cs',project)
   self.assertNotIn('Mushroom.cs',project)
