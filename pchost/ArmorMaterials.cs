using System;
using System.Collections.Generic;
using Alta;
using Alta.Caves;
using Alta.Impact;
using Alta.Networking;
using UnityEngine;

namespace ArmorRestored
{
    // Use the game's material channel, rather than making separate armor item IDs.
    internal static class ArmorMaterials
    {
        internal static void Configure(int piece, GameObject armor)
        {
            if (Catalog.SetOf(piece).Id != "metal") return;
            NetworkEntity entity = armor.GetComponent<NetworkEntity>();
            if (entity == null) throw new InvalidOperationException("armor has no NetworkEntity");
            Distribution distribution = Resources.Load<Distribution>("distributions/Metal Material Distribution");
            if (distribution == null) throw new InvalidOperationException("Metal Material Distribution is missing");
            PhysicalMaterialPart part = armor.GetComponent<PhysicalMaterialPart>();
            if (part == null) part = armor.AddComponent<PhysicalMaterialPart>();
            part.physicalMaterial = HashedGeneralValue<PhysicalMaterial>.Get(1U);
            part.materialChannel = 0;
            part.impactors = new ImpactGenerator[0];
            part.listenToPhysicalMaterial = new MonoBehaviour[0];
            part.durabilityModule = armor.GetComponent<DurabilityModule>();
            part.renderers = armor.GetComponentsInChildren<Renderer>(true);
            part.entity = entity;
            part.materialDistribution = distribution;
            List<MonoBehaviour> behaviours = entity.networkEntityBehaviours == null
                ? new List<MonoBehaviour>() : new List<MonoBehaviour>(entity.networkEntityBehaviours);
            if (!behaviours.Contains(part)) behaviours.Add(part);
            entity.networkEntityBehaviours = behaviours.ToArray();
        }

        internal static uint Read(NetworkEntity entity)
        {
            PhysicalMaterialPart part = entity.GetComponent<PhysicalMaterialPart>();
            return part == null || part.physicalMaterial == null ? 0U : part.physicalMaterial.Hash;
        }

        internal static void Restore(NetworkEntity entity, uint hash)
        {
            if (entity == null || hash == 0U) return;
            PhysicalMaterialPart part = entity.GetComponent<PhysicalMaterialPart>();
            PhysicalMaterial material = HashedGeneralValue<PhysicalMaterial>.Get(hash);
            if (part != null && material != null) part.physicalMaterial = material;
        }
    }
}
