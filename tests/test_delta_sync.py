"""Reference wire round trips and delta contracts (not native runtime tests)."""
from pathlib import Path
import random
import struct
import unittest
ROOT=Path(__file__).resolve().parents[1]
def src(file): return (ROOT/file).read_text(encoding='utf-8-sig')
def encode_slot(piece,material,inputs):
    mask=sum(1<<i for i,value in enumerate(inputs) if value!=material)
    return struct.pack('<BII',piece+1,material,mask)+b''.join(struct.pack('<I',value) for i,value in enumerate(inputs) if mask>>i&1)
def decode_slot(data):
    piece,material,mask=struct.unpack_from('<BII',data);at=9;inputs=[]
    for i in range(32):
        value=material
        if mask>>i&1: value=struct.unpack_from('<I',data,at)[0];at+=4
        inputs.append(value)
    return piece-1,material,inputs,at
class DeltaSync(unittest.TestCase):
    def test_sparse_palettes_round_trip(self):
        rng=random.Random(443)
        for changes in range(33):
            material=0xffffffff;inputs=[material]*32
            for i in range(changes): inputs[i]=rng.randrange(0xffffffff)
            data=encode_slot(47,material,inputs)
            self.assertEqual((47,material,inputs,len(data)),decode_slot(data))
            self.assertEqual(9+4*changes,len(data))
    def test_sparse_uniform_payload(self):
        self.assertEqual(58,22+4*len(encode_slot(-1,0,[0]*32)))
    def test_sender_initial_delta_resync_and_failure_guards(self):
        text=src('pchost/Net.cs')
        for check in ('state.FullAt=UnityEngine.Time.unscaledTime+30f','!EqualRow(previous,row.Value)',
                      'if(!full && selected.Count==0) return;', 'state.Rows.Clear();state.FullAt=0f;',
                      'if(!live.Contains(key)) recipients.Remove(key)', 'bool full=fresh'):
            self.assertIn(check,text)
        self.assertIn('ServerArmor._dirty || needsSync',src('pchost/ServerArmor.cs'))
    def test_delta_does_not_clear_other_players(self):
        for file in ('pchost/Net.cs','quest/Wire.cs'):
            text=src(file)
            self.assertIn('if(incomingFull) completeRows.Clear();',text)
            self.assertIn('foreach(var pair in incomingRows) completeRows[pair.Key]=pair.Value;',text)
            self.assertIn('completeRows.Clear();}',text)
    def test_quest_culling_and_scan_budget(self):
        text=src('quest/Draw.cs')
        for check in ('nextPlayerScan=Time.unscaledTime+1f','35f*35f : 30f*30f',
                      'if(num>=0 && !visible) continue;', 'updateWhenOffscreen = false;',
                      'ShadowCastingMode.Off', 'LightProbeUsage.Off','ReflectionProbeUsage.Off'):
            self.assertIn(check,text)
        self.assertIn('cachedPlayers.Clear();nextPlayerScan=0f;',text)
