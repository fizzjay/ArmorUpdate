using System;
using System.Collections.Generic;
using System.Reflection;
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
            if (Catalog.SetOf(piece).Id != "metal" && Catalog.SetOf(piece).Id != "hebios" && piece / 8 != 4) return;
            NetworkEntity entity = armor.GetComponent<NetworkEntity>();
            if (entity == null) throw new InvalidOperationException("armor has no NetworkEntity");
            Distribution distribution = Resources.Load<Distribution>("distributions/Metal Material Distribution");
            if (distribution == null) throw new InvalidOperationException("Metal Material Distribution is missing");
            PhysicalMaterialPart part = armor.GetComponent<PhysicalMaterialPart>();
            if (part == null) part = armor.AddComponent<PhysicalMaterialPart>();
            Set(part, "physicalMaterial", HashedGeneralValue<PhysicalMaterial>.Get(1U));
            Set(part, "materialChannel", 0);
            Set(part, "impactors", new ImpactGenerator[0]);
            Set(part, "listenToPhysicalMaterial", new MonoBehaviour[0]);
            Set(part, "durabilityModule", armor.GetComponent<DurabilityModule>());
            Set(part, "renderers", piece / 8 >= 3 ? new Renderer[0] : armor.GetComponentsInChildren<Renderer>(true));
            Set(part, "entity", entity);
            Set(part, "materialDistribution", piece / 8 == 4 ? null : distribution);
            object registered = Get(entity, "networkEntityBehaviours");
            MonoBehaviour[] existing = registered as MonoBehaviour[];
            if (registered != null && existing == null)
                throw new InvalidOperationException("NetworkEntity.networkEntityBehaviours is not a MonoBehaviour array in this game build");
            List<MonoBehaviour> behaviours = existing == null
                ? new List<MonoBehaviour>() : new List<MonoBehaviour>(existing);
            if (!behaviours.Contains(part)) behaviours.Add(part);
            Set(entity, "networkEntityBehaviours", behaviours.ToArray());
        }

        internal static uint Read(NetworkEntity entity)
        {
            PhysicalMaterialPart part = entity.GetComponent<PhysicalMaterialPart>();
            PhysicalMaterial material = part == null ? null : Get(part, "physicalMaterial") as PhysicalMaterial;
            return material == null ? 0U : material.Hash;
        }

        internal static void Restore(NetworkEntity entity, uint hash)
        {
            if (entity == null || hash == 0U) return;
            PhysicalMaterialPart part = entity.GetComponent<PhysicalMaterialPart>();
            PhysicalMaterial material = HashedGeneralValue<PhysicalMaterial>.Get(hash);
            if (part == null || material == null)
                throw new InvalidOperationException("Cannot restore armor physical material " + hash);
            Set(part, "physicalMaterial", material);
        }
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        private static MemberInfo Find(object target, string name)
        {
            for (Type type = target.GetType(); type != null; type = type.BaseType)
            {
                FieldInfo field = type.GetField(name, Members);
                if (field != null) return field;
                PropertyInfo property = type.GetProperty(name, Members);
                if (property != null && property.GetIndexParameters().Length == 0) return property;
            }
            throw new MissingMemberException(target.GetType().FullName, name);
        }

        private static object Get(object target, string name)
        {
            MemberInfo member = Find(target, name);
            FieldInfo field = member as FieldInfo;
            return field != null ? field.GetValue(target) : ((PropertyInfo)member).GetValue(target, null);
        }

        private static void Set(object target, string name, object value)
        {
            MemberInfo member = Find(target, name);
            FieldInfo field = member as FieldInfo;
            if (field != null) field.SetValue(target, value);
            else ((PropertyInfo)member).SetValue(target, value, null);
        }
    }
}
