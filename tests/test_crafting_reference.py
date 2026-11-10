"""Offline validation of the reference inspector; no game or DLL dependency."""
import json
import unittest
from tools.inspect_crafting_reference import verify
import hashlib


class CraftingReferenceTests(unittest.TestCase):
    def resources(self):
        model = b'test model'
        entry = dict(prefabName='test', sources=[50988])
        for field in ('levels', 'parents', 'requiresNails', 'nailPositions',
                      'jointPoints', 'supportPoints', 'nailRotations',
                      'jointGaps', 'clearanceDegrees'):
            entry[field] = [0]
        archive = dict(schemaVersion=1, bundleSha256=hashlib.sha256(model).hexdigest(),
                       entries=[entry])
        return {'PlayerArmor.Model': model,
                'PlayerArmor.JigPlans': json.dumps(archive).encode()}

    def test_matching_archive(self):
        self.assertEqual(verify(self.resources())['jig_count'], 1)

    def test_replaced_model_rejected(self):
        data = self.resources()
        data['PlayerArmor.Model'] = b'different'
        with self.assertRaisesRegex(ValueError, 'does not match'):
            verify(data)

    def test_bad_array_rejected(self):
        data = self.resources()
        archive = json.loads(data['PlayerArmor.JigPlans'])
        archive['entries'][0]['parents'] = []
        data['PlayerArmor.JigPlans'] = json.dumps(archive).encode()
        with self.assertRaisesRegex(ValueError, 'Mismatched'):
            verify(data)

    def test_duplicate_plan_rejected(self):
        data = self.resources()
        archive = json.loads(data['PlayerArmor.JigPlans'])
        archive['entries'] *= 2
        data['PlayerArmor.JigPlans'] = json.dumps(archive).encode()
        with self.assertRaisesRegex(ValueError, 'Duplicate'):
            verify(data)
