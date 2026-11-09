using System;
using System.Collections.Generic;
using Alta.Chunks;
using Alta.Networking;
using Alta.Saving;
using HarmonyLib;
using UnityEngine;
using SavingSetup = Alta.Saving.SavingSystem.SavingSetup;

namespace ArmorRestored
{
    // Server-only savable component. Clients receive the ordinary native pickup
    // and PhysicalMaterialPart; no custom IL2CPP interface injection is needed.
    [SavingInfo("ArmorRestored.CraftOrigin.v1", false)]
    public sealed class ArmorCraftRecord : MonoBehaviour, ISavable<ArmorCraftRecordData>, ISavableSave<ArmorCraftRecordData>
    {
        private string payload;
        internal bool HasRecord { get { return payload != null; } }
        internal ArmorCraftOrigin Read()
        {
            if (payload == null) return null;
            if (payload.Length == 0 || payload.Length > 440) throw new InvalidOperationException("Invalid saved armor origin");
            return ArmorCraftOrigin.Decode(Convert.FromBase64String(payload));
        }
        internal void Store(ArmorCraftOrigin origin) { payload = Convert.ToBase64String(origin.Encode()); }
        ArmorCraftRecordData ISavableSave<ArmorCraftRecordData>.Save(ISerializeChildren children)
        { return new ArmorCraftRecordData { payload = payload ?? "N" }; }
        void ISavable<ArmorCraftRecordData>.Load(ArmorCraftRecordData data, ISerializeChildren children)
        {
            payload = data == null ? "" : data.payload == "N" ? null : data.payload ?? "";
            if (payload == null) return; // naturally spawned, never crafted
            try { Read(); } catch { payload = ""; } // retain invalid marker; NEVER fall back to a loot refund
        }
        string ISavableSaveBase.name { get { return name; } }
        internal static void Register()
        {
            var setups = (Dictionary<Type,SavingSetup>)AccessTools.Field(typeof(SavingSystem),"savingSetups").GetValue(null);
            var hashes = (Dictionary<uint,SavingSetup>)AccessTools.Field(typeof(SavingSystem),"savingTypes").GetValue(null);
            Type topic = typeof(ArmorCraftRecord);
            uint hash = SavingInfoAttribute.GetHash(topic);
            var versions = SavingSystem.LatestChunkVersioning;
            if (setups == null || hashes == null || versions == null) throw new InvalidOperationException("Native chunk saving is not ready");
            uint version;
            bool hasVersion = versions.HashVersions.TryGetValue(hash,out version);
            if (hasVersion && version != 1) throw new InvalidOperationException("Armor origin chunk schema mismatch");
            SavingSetup setup, collision;
            if (setups.TryGetValue(topic,out setup))
            {
                if (setup.LatestVersion != 1 || setup.LatestSetup.Type != typeof(ArmorCraftRecordData) ||
                    !hashes.TryGetValue(hash,out collision) || !object.ReferenceEquals(collision,setup))
                    throw new InvalidOperationException("Armor origin save registry mismatch");
            }
            else
            {
                if (hashes.ContainsKey(hash)) throw new InvalidOperationException("Armor origin save hash collision");
                setup = new SavingSetup(topic);
                setup.Add(1,typeof(ArmorCraftRecordData));
                setup.FinishInitialization(setups);
                if (setup.Save == null) throw new InvalidOperationException("Armor origin native save delegate unavailable");
                setups.Add(topic,setup); hashes.Add(setup.Hash,setup);
            }
            if (!hasVersion) versions.Add(hash,1);
        }
    }
    [SaveStructure(typeof(ArmorCraftRecord), 1)]
    public sealed class ArmorCraftRecordData : NewSaveData<ArmorCraftRecord,ArmorCraftRecordData>
    {
        // Native serializer supports primitive strings; no custom array serializer.
        // Base64 is bounded to 440 chars and the decoded format is checksummed.
        public string payload;
    }
}
