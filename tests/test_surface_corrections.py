"""Appearance/withdrawal source contracts and real triangle ownership; no Unity simulation."""
import unittest
from pathlib import Path
from tools.build_worn_groups import build
from tools.skin_metal import parse
ROOT=Path(__file__).resolve().parents[1]
def code(path):return (ROOT/path).read_text('utf-8-sig')

class SurfaceCorrections(unittest.TestCase):
 def test_uncrafted_hebios_palette_is_zero_and_crafted_overrides_it(self):
  text=code('pchost/ServerArmor.cs')
  start=text.index('uint appearance = (row[slot] / 8 == 3 || row[slot] / 8 == 4 && materials[slot] == 1U) ? 0U : materials[slot];')
  end=text.index('origin.MaterialAt(group)',start)
  self.assertIn('row[8+slot]=unchecked((int)appearance)',text[start:end])
  self.assertIn('origin.Result!=(uint)Catalog.Number(row[slot])',text[start:end])
  self.assertIn('string.IsNullOrEmpty(origins[slot])',text[start:end])
 def test_native_material_cannot_replace_default_hebios_atlas(self):
  self.assertIn('piece / 8 != 1 ? new Renderer[0]',code('pchost/ArmorMaterials.cs'))
  self.assertIn('renderer != null && piece / 8 == 1',code('quest/ArmorMaterials.cs'))
 def test_grace_starts_before_pop_and_blocks_both_insert_paths(self):
  text=code('pchost/ArmorCraftingServer.cs')
  self.assertIn('if (__instance.IsFilled) ArmorCrafting.BeginRemoval(__state.Station)',text)
  self.assertIn('if (ArmorCrafting.RemovalGrace(__0)) return;',text)
  shared=code('shared/ArmorCrafting.cs')
  self.assertIn('Time.unscaledTime+1f',shared)
  self.assertIn('if(RemovalGrace(__instance)) return false;',shared)
  self.assertIn('if(RemovalGrace(project.Station)) { __result=false; return; }',shared)
  self.assertIn('removalGrace.Clear()',shared)
 def test_initial_dismantled_refund_also_has_grace(self):
  text=code('pchost/ArmorCraftingServer.cs');start=text.index('project.SetMaxLevel();')
  fragment=text[start:text.index('detached.Empty();',start)]
  self.assertLess(fragment.index('ArmorCrafting.BeginRemoval(station)'),fragment.index('SpawnHelper.Spawn'))
 def test_leather_worn_maps_cover_every_actual_triangle(self):
  maps=build();_,meshes,pieces=parse((ROOT/'pchost/armor.pack').read_bytes())
  for piece in pieces[:8]:
   owners=set()
   for part in piece['parts']:
    m=meshes[part['mesh']];groups=maps[m['name']]
    self.assertEqual(sum(len(s)//3 for s in m['subs']),len(groups))
    self.assertTrue(all(0<=g<3 for g in groups));owners.update(groups)
   self.assertEqual({0,1,2},owners)
 def test_dye_uses_native_ramp_but_preserves_authored_texture_alpha_and_maps(self):
  for path in ('pchost/WornMaterials.cs','quest/WornMaterials.cs'):
   text=code(path)
   for token in ('ApplyLeather(uint hash','Township Tale/RampLerp3','_Sampler','NativeLeather(33384U)',
                 'color.a=pixels[i].a','new Material(original)','entry.Material.mainTexture=texture',
                 'RenderTexture.active=previous','RenderTexture.ReleaseTemporary(temporary)',
                 'UnityEngine.Object.Destroy(variant.Texture)','UpdateLeather(palette.Key,variant)',
                 'if(hash==0U || original==null) return original;','AuthoredColors.Clear()'):
    self.assertIn(token,text)
   leather=text[text.index('private sealed class LeatherVariant'):text.index('private static void Paint')]
   self.assertNotIn('.shader=',leather)
   self.assertNotIn('mesh.vertices',leather)
 def test_surface_type_follows_input_source(self):
  text=code('shared/ArmorCrafting.cs')
  self.assertIn('source==47760U || source==63204U',text)
  self.assertIn('return WornMaterials.ApplyLeather(hash,original,name)',text)
  self.assertIn('piece/8!=2 && piece/8!=5 ? WornIngredientGroups.Get(name) : null',text)
 def test_reference_tone_mapping_is_monotonic(self):
  def inverse(a,b,v):return max(0,min(1,(v-a)/(b-a)))
  low,mid,high,knee=0,.5,1,.5
  values=[knee*inverse(low,mid,x/255) if x/255<mid else knee+(1-knee)*inverse(mid,high,x/255) for x in range(256)]
  self.assertEqual(values,sorted(values));self.assertEqual(0,values[0]);self.assertEqual(1,values[-1])
