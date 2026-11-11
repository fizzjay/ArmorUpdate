"""Protocol budget and cross-platform snapshot contracts."""
from pathlib import Path
import unittest
ROOT=Path(__file__).resolve().parents[1]
class SnapshotBudget(unittest.TestCase):
    def test_worst_case_packet(self):
        # Kind, four 32-bit header words, player ID, four full slots.
        size=1+4*4+1+4+4*(1+4+4+32*4)
        self.assertEqual(size,570)
        self.assertLess(size+128,1100)
    def test_receivers_match(self):
        pc=(ROOT/'pchost/Net.cs').read_text(encoding='utf-8-sig')
        quest=(ROOT/'quest/Wire.cs').read_text(encoding='utf-8-sig')
        def helper(s):
            start=s.index('        private static bool incomingFull;')
            end=s.index('\n\t\t// Token:',start)
            return s[start:end].replace('ClientArmor.OnTable','APPLY').replace('Draw.OnTable','APPLY')
        self.assertEqual(helper(pc),helper(quest))
        for text in (pc,quest):
            self.assertIn('incomingParts.Count==incomingCount',text)
            self.assertIn('count>1024',text)
            self.assertIn('incomingParts.Contains((int)chunk)',text)
            self.assertIn('now-incomingAt>10f',text)
    def test_sends_bounded_halves_not_whole_table(self):
        text=(ROOT/'pchost/Net.cs').read_text(encoding='utf-8-sig')
        self.assertIn('int chunks=Math.Max(1,selected.Count*2)',text)
        self.assertIn('byte kind=14',text)
        self.assertIn('int Wire = 6',text)
        for side,net in [('pchost','Net'),('quest','Wire')]:
            text=(ROOT/side/'Gate.cs').read_text(encoding='utf-8-sig')
            self.assertGreaterEqual(text.count(net+'.ResetSnapshot();'),2)
