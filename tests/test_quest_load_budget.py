"""Load-budget source contracts; not headset runtime certification."""
from pathlib import Path
import unittest
ROOT = Path(__file__).resolve().parents[1]
def source(name):
    return (ROOT / name).read_text(encoding='utf-8-sig')
class QuestLoadBudget(unittest.TestCase):
    def test_texture_cap_covers_raw_and_compressed(self):
        text=source('quest/Pack.cs')
        self.assertIn('texDef.Format!=4 && num+1<texDef.Mips',text)
        self.assertIn('while(num3>512 || num4>512)',text)
        self.assertIn('array=Dxt.HalfRgba(array,num3,num4)',text)
        self.assertLess(text.index('array=Dxt.HalfRgba'),text.index('Dxt.MipChain(array'))
    def test_slot_budget_preserves_empty_slot_removal(self):
        text=source('quest/Draw.cs')
        self.assertIn('int buildsRemaining=1',text)
        self.assertIn('if(num>=0 && buildsRemaining==0) {Draw._dirty=true;continue;}',text)
        self.assertLess(text.index('if(num>=0 && buildsRemaining==0)'),text.index('Draw.Remove(dressed, j)'))
    def test_idle_fabric_does_not_install_hook(self):
        text=source('quest/CosmeticCoverage.cs')
        text=text[text.index('private void UpdateFabric('):]
        self.assertLess(text.index('if(next.Count==0 && fabricOwned.Count==0) return;'),text.index('InstallFabric();'))
    def test_quest_discovery_interval(self):
        self.assertIn('nextTick = Time.unscaledTime + 2f;',source('shared/ArmorStats.cs'))
    def test_visuals_wait_for_local_avatar_and_settling_delay(self):
        text=source('quest/Draw.cs')
        self.assertIn('visualReadyAt=Time.unscaledTime+10f;',text)
        self.assertIn('!character.gameObject.activeInHierarchy',text)
        self.assertIn('loadingRig!=rig.Pointer',text)
        self.assertLess(text.index('if(!VisualsReady()) return;'),text.index('WornMaterials.Tick();'))
        self.assertIn('visualsReady=waitingLogged=false;',text)
        self.assertIn('!Draw.VisualsReady()',source('shared/ArmorStats.cs'))
    def test_experimental_quest_fabric_is_held_off(self):
        text=source('quest/CosmeticCoverage.cs')
        self.assertIn('readonly bool QuestFabricEnabled = false;',text)
        text=text[text.index('private void UpdateFabric('):]
        self.assertLess(text.index('if(!QuestFabricEnabled) return;'),text.index('InstallFabric();'))
    def test_unbaked_preview_cannot_build_armor(self):
        text=source('quest/Draw.cs')
        self.assertIn('d.BakedBindings=bound;',text)
        self.assertIn('if(dressed.BakedBindings==0) continue;',text)
        self.assertIn('dressed.NextBindingRetry=Time.unscaledTime+1f;',text)
