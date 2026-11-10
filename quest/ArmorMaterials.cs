using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppAlta;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppAlta.Caves;
using Il2CppAlta.Impact;
using Il2CppAlta.Networking;
using UnityEngine;

namespace ArmorRestoredQuest
{
    // Use the game's material channel, rather than making separate armor item IDs.
    internal static class ArmorMaterials
    {
        internal static void Configure(int piece, GameObject armor)
        {
            if (piece / 8 != 1 && piece / 8 != 3 && piece / 8 != 4) return;
            NetworkEntity entity = armor.GetComponent(Il2CppType.Of<NetworkEntity>())?.TryCast<NetworkEntity>();
            if (entity == null) throw new InvalidOperationException("armor has no NetworkEntity");
            Distribution distribution = Resources.Load<Distribution>("distributions/Metal Material Distribution");
            if (distribution == null) throw new InvalidOperationException("Metal Material Distribution is missing");
            PhysicalMaterialPart part = armor.GetComponent(Il2CppType.Of<PhysicalMaterialPart>())?.TryCast<PhysicalMaterialPart>();
            if (part == null) part = armor.AddComponent(Il2CppType.Of<PhysicalMaterialPart>()).TryCast<PhysicalMaterialPart>();
            part.physicalMaterial = HashedGeneralValue<PhysicalMaterial>.Get(1U);
            part.materialChannel = 0;
            part.impactors = new Il2CppReferenceArray<ImpactGenerator>(0L);
            part.listenToPhysicalMaterial = new Il2CppReferenceArray<MonoBehaviour>(0L);
            part.durabilityModule = armor.GetComponent(Il2CppType.Of<DurabilityModule>())?.TryCast<DurabilityModule>();
            List<Renderer> renderers = new List<Renderer>();
            foreach (Component component in armor.GetComponentsInChildren(Il2CppType.Of<Renderer>(), true))
            {
                Renderer renderer = component.TryCast<Renderer>();
                if (renderer != null && piece / 8 < 3) renderers.Add(renderer);
            }
            part.renderers = new Il2CppReferenceArray<Renderer>(renderers.ToArray());
            part.entity = entity;
            part.materialDistribution = piece / 8 == 4 ? null : distribution;
            List<MonoBehaviour> behaviours = new List<MonoBehaviour>();
            bool registered = false;
            if (entity.networkEntityBehaviours != null)
            {
                foreach (MonoBehaviour behaviour in entity.networkEntityBehaviours)
                {
                    behaviours.Add(behaviour);
                    if (behaviour != null && behaviour.Pointer == part.Pointer) registered = true;
                }
            }
            if (!registered) behaviours.Add(part);
            entity.networkEntityBehaviours = new Il2CppReferenceArray<MonoBehaviour>(behaviours.ToArray());
        }

    }
}
