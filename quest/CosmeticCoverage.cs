using System;
using System.Collections.Generic;
using System.Reflection;
using Il2Cpp;
using Il2CppAlta.Customization;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace ArmorRestoredQuest
{
    // Own only transient native "impeded" arguments, never the player's saved cosmetics.
    internal sealed class CosmeticCoverage
    {
        private PlayerCharacterCustomizationManager manager;
        private readonly Dictionary<CustomizationSlotEntry, Dictionary<ICustomizationHandler, TwoColors>> owned =
            new Dictionary<CustomizationSlotEntry, Dictionary<ICustomizationHandler, TwoColors>>();
        private bool warned;
        private float nextCoverage, fabricWarmupUntil;
        private string lastDiagnostic;
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        internal void Update(Transform root, int[] shown)
        {
            try
            {
                PlayerCharacterCustomizationManager next = root == null ? null : ManagerOf(root);
                if ((manager == null) != (next == null) ||
                    (manager != null && next != null && manager.Pointer != next.Pointer))
                { Clear(); manager = next; fabricWarmupUntil=Time.unscaledTime+8f; }
                if (manager == null)
                {
                    if (!warned && Array.Exists(shown, piece => piece >= 0))
                    {
                        warned = true;
                        Mod.Log.Warning("Cosmetic armor coverage deferred: customization manager not found on character/handler.");
                    }
                    return;
                }
                if(Time.unscaledTime<nextCoverage) return;
                nextCoverage=Time.unscaledTime+.5f;
                uint mask = 0U;
                for (int i = 0; i < 8 && i < shown.Length; i++)
                    if (shown[i] >= 0) mask |= 1U << i;
                HashSet<CustomizationSlotEntry> current = Entries();
                List<ICustomizationHandler> handlers = new List<ICustomizationHandler>();
                foreach (ICustomizationHandler handler in NativeEnumerable.Items(manager.Handlers))
                    if (handler != null && !handler.IsDestroyed) handlers.Add(handler);
                UpdateFabric(current,handlers,mask);
                foreach (CustomizationSlotEntry entry in new List<CustomizationSlotEntry>(owned.Keys))
                {
                    if (!current.Contains(entry) || !Covered(entry, mask))
                    {
                        Restore(entry, owned[entry], current.Contains(entry), handlers);
                        owned.Remove(entry);
                    }
                }
                foreach (CustomizationSlotEntry entry in current)
                {
                    if (!Covered(entry, mask) || entry.DisablerCount > 0) continue;
                    TwoColors original = Native<TwoColors>(entry.Arguments);
                    Dictionary<ICustomizationHandler, TwoColors> overlays;
                    if (!owned.TryGetValue(entry, out overlays))
                    {
                        overlays = new Dictionary<ICustomizationHandler, TwoColors>();
                        owned.Add(entry, overlays);
                    }
                    foreach (ICustomizationHandler stale in new List<ICustomizationHandler>(overlays.Keys))
                        if (!handlers.Contains(stale)) overlays.Remove(stale);
                    foreach (ICustomizationHandler handler in handlers)
                    {
                        CosmeticPacker.Input input = Input(entry, handler);
                        if (input == null) continue;
                        TwoColors previous;
                        overlays.TryGetValue(handler, out previous);
                        if (original.IsImpeded)
                        {
                            if (previous != null && Same(input.Arguments, previous))
                                Change(entry, handler, original);
                            overlays.Remove(handler);
                            continue;
                        }
                        if (previous != null && Same(input.Arguments, previous) &&
                            previous.Primary == original.Primary && previous.Secondary == original.Secondary) continue;
                        TwoColors compact = new TwoColors { Primary = original.Primary,
                            Secondary = original.Secondary, IsImpeded = true };
                        // Record before changing, so a partial failure can still restore.
                        overlays[handler] = compact;
                        Change(entry, handler, compact);
                    }
                }
                Diagnose(current, handlers, mask);
            }
            catch (Exception ex)
            {
                if (!warned)
                {
                    warned = true;
                    Mod.Log.Warning("Cosmetic armor coverage deferred: " + ex.GetBaseException().Message);
                }
            }
        }

        private static PlayerCharacterCustomizationManager ManagerOf(Transform root)
        {
            PlayerCharacterCustomizationManager manager = ManagerOn(root.GetComponent(Il2CppType.Of<PlayerCharacter>())?.TryCast<PlayerCharacter>());
            if (manager != null) return manager;
            foreach (Component component in root.GetComponentsInChildren(Il2CppType.Of<PlayerCustomizationHandler>(), true))
            {
                manager = ManagerOn(component.TryCast<PlayerCustomizationHandler>());
                if (manager != null) return manager;
            }
            return null;
        }

        private static PlayerCharacterCustomizationManager ManagerOn(object target)
        {
            if (target == null) return null;
            for (Type type = target.GetType(); type != null; type = type.BaseType)
            {
                foreach (FieldInfo field in type.GetFields(Members))
                    if (field.FieldType == typeof(PlayerCharacterCustomizationManager))
                    {
                        var manager = Native<PlayerCharacterCustomizationManager>(field.GetValue(target));
                        if (manager != null) return manager;
                    }
                foreach (PropertyInfo property in type.GetProperties(Members))
                    if (property.PropertyType == typeof(PlayerCharacterCustomizationManager) &&
                        property.CanRead && property.GetIndexParameters().Length == 0)
                    {
                        var manager = Native<PlayerCharacterCustomizationManager>(property.GetValue(target, null));
                        if (manager != null) return manager;
                    }
            }
            return null;
        }

        private static T Native<T>(object value) where T : Il2CppObjectBase
        {
            // Use the known interop API directly, not runtime reflective
            // generic dispatch. CLR `as` only recognizes the proxy wrapper,
            // not the underlying native object's derived type.
            T typed = value as T;
            if (typed != null) return typed;
            Il2CppObjectBase native = value as Il2CppObjectBase;
            return native == null ? null : native.TryCast<T>();
        }

        private static bool Same(Il2CppObjectBase left, Il2CppObjectBase right)
        {
            return left == null ? right == null : right != null && left.Pointer == right.Pointer;
        }

        private static void Change(CustomizationSlotEntry entry, ICustomizationHandler handler, CustomizationArguments colors)
        {
            var handlers = new Il2CppSystem.Collections.Generic.List<ICustomizationHandler>();
            handlers.Add(handler);
            entry.Part.ChangeArguments(handlers.Cast<Il2CppSystem.Collections.Generic.IEnumerable<ICustomizationHandler>>(), colors);
        }

        private HashSet<CustomizationSlotEntry> Entries()
        {
            CustomizationVisualManager visuals = Native<CustomizationVisualManager>(Read(manager, "internalManager"));
            if (visuals == null) throw new InvalidOperationException("Native cosmetic visual manager unavailable");
            var items = Native<Il2CppSystem.Collections.Generic.Dictionary<uint, Il2CppSystem.Collections.Generic.List<CustomizationSlotEntry>>>(Read(visuals, "items"));
            if (items == null) throw new InvalidOperationException("Native cosmetic entries unavailable");
            HashSet<CustomizationSlotEntry> entries = new HashSet<CustomizationSlotEntry>();
            var groups = new Il2CppReferenceArray<Il2CppSystem.Collections.Generic.List<CustomizationSlotEntry>>(items.Count);
            items.Values.CopyTo(groups, 0);
            for (int group = 0; group < groups.Length; group++)
            {
                var list = groups[group];
                for (int i = 0; i < list.Count; i++)
                {
                    CustomizationSlotEntry entry = list[i];
                    if (entry != null) entries.Add(entry);
                }
            }
            return entries;
        }

        private static object Read(object target, string name)
        {
            for (Type type = target.GetType(); type != null; type = type.BaseType)
            {
                FieldInfo field = type.GetField(name, Members);
                if (field != null) return field.GetValue(target);
                PropertyInfo property = type.GetProperty(name, Members);
                if (property != null && property.CanRead) return property.GetValue(target, null);
            }
            return null;
        }

        private static CosmeticPacker.Input Input(CustomizationSlotEntry entry, ICustomizationHandler handler)
        {
            ICosmeticMeshPartBase part = Native<ICosmeticMeshPartBase>(entry.Part);
            CosmeticPacker.Input input;
            if (part == null || handler == null || handler.IsDestroyed || handler.CharacterDummy == null ||
                handler.CharacterDummy.CosmeticPacker == null) return null;
            return handler.CharacterDummy.CosmeticPacker.Inputs.TryGetValue(part.PartId, out input) ? input : null;
        }

        private static bool Covered(CustomizationSlotEntry entry, uint mask)
        {
            if (entry == null || Native<TwoColors>(entry.Arguments) == null || Native<SkinMeshPart>(entry.Part) != null ||
                entry.Visual == null || Native<PlayerCosmeticDefinition>(entry.Visual.Definition) == null) return false;
            ICosmeticMeshPartBase part = Native<ICosmeticMeshPartBase>(entry.Part);
            if (part == null || part.IsTextureSharingForced || entry.Part.Slots == null) return false;
            // Never compact an item without a complete set of authored compact LODs.
            var lods = part.Lods;
            var compact = part.ImpededLods;
            if (lods == null || lods.Length == 0 || compact == null || compact.Length < lods.Length) return false;
            bool any = false;
            for (int i = 0; i < lods.Length; i++)
                if (lods[i] != null) { if (compact[i] == null) return false; any = true; }
            if (!any) return false;
            bool covered = false;
            foreach (int slot in entry.Part.Slots)
            {
                // Exclude held accessories/non-clothing, matching the native reference.
                if (slot >= 301 && slot != 331 && slot != 501 && slot != 511) return false;
                if (CoversSlot(mask, slot)) covered = true;
            }
            return covered;
        }

        // Repository order: head, chest, left/right shoulder, left/right arm,
        // left/right tasset. Do not cast the differently ordered native enum.
        internal static bool CoversSlot(uint mask, int slot)
        {
            return ((mask & 1U) != 0 && (Between(slot, 201, 213) || Between(slot, 251, 263) || slot == 331))
                || ((mask & 2U) != 0 && (Between(slot, 11, 55) || Between(slot, 181, 195) || Between(slot, 281, 285) || slot == 511))
                || ((mask & 4U) != 0 && (Between(slot, 81, 85) || Between(slot, 101, 105)))
                || ((mask & 8U) != 0 && (Between(slot, 91, 95) || Between(slot, 111, 115)))
                || ((mask & 16U) != 0 && (Between(slot, 121, 125) || Between(slot, 141, 145) || Between(slot, 161, 165)))
                || ((mask & 32U) != 0 && (Between(slot, 131, 135) || Between(slot, 151, 155) || Between(slot, 171, 175)))
                || ((mask & 192U) != 0 && (Between(slot, 51, 75) || slot == 501));
        }

        private void Diagnose(HashSet<CustomizationSlotEntry> entries, List<ICustomizationHandler> handlers, uint mask)
        {
            if (mask == 0U) { lastDiagnostic = null; return; }
            Dictionary<string, int> reasons = new Dictionary<string, int>();
            int eligible = 0, inputs = 0;
            foreach (CustomizationSlotEntry entry in entries)
            {
                string reason;
                if (Covered(entry, mask))
                {
                    eligible++;
                    foreach (ICustomizationHandler handler in handlers)
                        if (Input(entry, handler) != null) inputs++;
                    reason = "eligible";
                }
                else
                {
                    reason = SkipReason(entry, mask);
                    // Include the actual native class: a base CLR proxy name
                    // cannot explain why a definition/argument cast failed.
                    if (entry != null && reason == "not-player-cosmetic" && entry.Visual != null)
                        reason += ":" + NativeName(entry.Visual.Definition);
                    else if (entry != null && reason == "not-TwoColors")
                        reason += ":" + NativeName(entry.Arguments);
                }
                int count;
                reasons.TryGetValue(reason, out count);
                reasons[reason] = count + 1;
            }
            List<string> details = new List<string>();
            foreach (var pair in reasons) details.Add(pair.Key + "=" + pair.Value);
            details.Sort(StringComparer.Ordinal);
            string diagnostic = "mask=" + mask + " entries=" + entries.Count +
                " handlers=" + handlers.Count + " eligible=" + eligible + " packedInputs=" + inputs +
                " [" + string.Join(", ", details.ToArray()) + "]";
            if (diagnostic == lastDiagnostic) return;
            lastDiagnostic = diagnostic;
            Mod.Log.Msg("cosmetic coverage: " + diagnostic);
        }

        private static string NativeName(object value)
        {
            Il2CppObjectBase native = value as Il2CppObjectBase;
            if (native == null) return value == null ? "null" : value.GetType().FullName;
            IntPtr klass = IL2CPP.il2cpp_object_get_class(native.Pointer);
            return System.Runtime.InteropServices.Marshal.PtrToStringAnsi(IL2CPP.il2cpp_class_get_name(klass));
        }

        private static string SkipReason(CustomizationSlotEntry entry, uint mask)
        {
            if (entry == null) return "missing-entry";
            if (Native<TwoColors>(entry.Arguments) == null) return "not-TwoColors";
            if (Native<SkinMeshPart>(entry.Part) != null) return "skin";
            if (entry.Visual == null || Native<PlayerCosmeticDefinition>(entry.Visual.Definition) == null) return "not-player-cosmetic";
            ICosmeticMeshPartBase part = Native<ICosmeticMeshPartBase>(entry.Part);
            if (part == null) return "not-mesh-part";
            if (part.IsTextureSharingForced) return "texture-sharing";
            if (entry.Part.Slots == null) return "no-slots";
            bool matched = false;
            foreach (int slot in entry.Part.Slots)
            {
                if (slot >= 301 && slot != 331 && slot != 501 && slot != 511) return "accessory-slot";
                if (CoversSlot(mask, slot)) matched = true;
            }
            if (!matched) return "outside-armor-slots";
            return "missing-compact-LODs";
        }

        private static bool Between(int slot, int first, int last) { return slot >= first && slot <= last; }

        private static void Restore(CustomizationSlotEntry entry, Dictionary<ICustomizationHandler, TwoColors> overlays,
            bool registered, List<ICustomizationHandler> handlers)
        {
            if (!registered || entry.Part == null || entry.Arguments == null || entry.DisablerCount > 0) return;
            foreach (KeyValuePair<ICustomizationHandler, TwoColors> overlay in overlays)
            {
                if (!handlers.Contains(overlay.Key)) continue;
                CosmeticPacker.Input input = Input(entry, overlay.Key);
                // Restore only our own overlay, never overwrite another mod/native change.
                if (input != null && Same(input.Arguments, overlay.Value))
                    Change(entry, overlay.Key, entry.Arguments);
            }
        }

        internal void Clear()
        {
            ClearFabric();
            if (manager != null)
            {
                try
                {
                    HashSet<CustomizationSlotEntry> current = Entries();
                    List<ICustomizationHandler> handlers = new List<ICustomizationHandler>();
                    foreach (ICustomizationHandler handler in NativeEnumerable.Items(manager.Handlers))
                        if (handler != null && !handler.IsDestroyed) handlers.Add(handler);
                    foreach (var pair in owned)
                        try { Restore(pair.Key, pair.Value, current.Contains(pair.Key), handlers); }
                        catch (Exception) { }
                }
                catch (Exception) { }
            }
            owned.Clear();
            manager = null;
            warned = false;
            lastDiagnostic = null;
            nextCoverage=0f;
        }
        // Fabric fallback owns pack-local cloned assets only. Saved parts and their LODs are never edited.
        private sealed class FabricBinding { internal CosmeticPacker.Input Input; internal ICustomizationHandler Handler; }
        private readonly Dictionary<long,FabricBinding> fabricOwned = new Dictionary<long,FabricBinding>();
        private static readonly object fabricGate = new object();
        private static readonly Dictionary<long,int> fabricUsers = new Dictionary<long,int>();
        private static readonly Dictionary<int,CosmeticMeshPart> fabricCache = new Dictionary<int,CosmeticMeshPart>();
        private static bool fabricInstalled, fabricAttempted, fabricWarning;
        private static long FabricKey(CosmeticPacker.Input input) { return input.Pointer.ToInt64(); }
        private static void InstallFabric()
        {
            if(fabricAttempted) return;
            fabricAttempted=true;
            try
            {
                var method=HarmonyLib.AccessTools.Method(typeof(MeshPacker),"Pack");
                if(method==null || method.GetParameters().Length!=8)
                    throw new MissingMethodException("Native MeshPacker.Pack layout changed");
                new HarmonyLib.Harmony("armorrestored.fabric-fit").Patch(method,
                    prefix:new HarmonyLib.HarmonyMethod(typeof(CosmeticCoverage),nameof(BeforeFabricPack)));
                fabricInstalled=true;
                Mod.Log.Msg("Cosmetic torso fabric fitting ready; authored compact LODs remain preferred.");
            }
            catch(Exception error) {Mod.Log.Warning("Cosmetic fabric fitting unavailable: "+error.GetBaseException().Message);}
        }
        private static bool FabricEligible(CustomizationSlotEntry entry,uint mask)
        {
            if((mask&2U)==0U || entry==null || entry.DisablerCount>0 || Covered(entry,mask) ||
                Native<TwoColors>(entry.Arguments)==null || Native<SkinMeshPart>(entry.Part)!=null || entry.Part.Slots==null ||
                entry.Visual==null || Native<PlayerCosmeticDefinition>(entry.Visual.Definition)==null) return false;
            var part=Native<ICosmeticMeshPartBase>(entry.Part);
            if(part==null || part.IsTextureSharingForced) return false;
            bool torso=false;
            foreach(int slot in entry.Part.Slots)
            {
                if(slot>=301 && slot!=331 && slot!=501 && slot!=511) return false;
                if(Between(slot,21,45) || Between(slot,191,195)) torso=true;
            }
            return torso;
        }
        private void UpdateFabric(HashSet<CustomizationSlotEntry> current,List<ICustomizationHandler> handlers,uint mask)
        {
            if(Time.unscaledTime<fabricWarmupUntil && fabricOwned.Count==0) return;
            var next=new Dictionary<long,FabricBinding>();
            foreach(var entry in current)
                if(FabricEligible(entry,mask)) foreach(var handler in handlers)
                {
                    var input=Input(entry,handler);
                    if(input!=null && input.Arguments!=null && !input.Arguments.IsImpeded) next[FabricKey(input)]=new FabricBinding {Input=input,Handler=handler};
                }
            if(next.Count==0 && fabricOwned.Count==0) return;
            InstallFabric();if(!fabricInstalled) return;
            var repack=new HashSet<ICustomizationHandler>();
            lock(fabricGate)
            {
                foreach(var pair in fabricOwned)
                    if(!next.ContainsKey(pair.Key))
                    {
                        long key=pair.Key;int count;
                        if(fabricUsers.TryGetValue(key,out count)) {if(count<=1) fabricUsers.Remove(key);else fabricUsers[key]=count-1;}
                        if(handlers.Contains(pair.Value.Handler)) repack.Add(pair.Value.Handler);
                    }
                foreach(var pair in next)
                    if(!fabricOwned.ContainsKey(pair.Key))
                    {long key=pair.Key;int count;fabricUsers.TryGetValue(key,out count);fabricUsers[key]=count+1;repack.Add(pair.Value.Handler);}
                fabricOwned.Clear();foreach(var pair in next) fabricOwned.Add(pair.Key,pair.Value);
            }
            foreach(var handler in repack) RepackFabric(handler);
        }
        private static void RepackFabric(ICustomizationHandler handler)
        {
            try {if(handler!=null && !handler.IsDestroyed && handler.CharacterDummy!=null && handler.CharacterDummy.CosmeticPacker!=null) handler.CharacterDummy.CosmeticPacker.PackAndApply();}
            catch(Exception error) {if(!fabricWarning) {fabricWarning=true;Mod.Log.Warning("Cosmetic fabric repack deferred: "+error.GetBaseException().Message);}}
        }
        private void ClearFabric()
        {
            var repack=new HashSet<ICustomizationHandler>();
            lock(fabricGate)
            {
                foreach(var pair in fabricOwned)
                {
                    long key=pair.Key;int count;
                    if(fabricUsers.TryGetValue(key,out count)) {if(count<=1) fabricUsers.Remove(key);else fabricUsers[key]=count-1;}
                    repack.Add(pair.Value.Handler);
                }
                fabricOwned.Clear();
            }
            foreach(var handler in repack) RepackFabric(handler);
        }
        private static void BeforeFabricPack(ref Il2CppSystem.Collections.Generic.Dictionary<CosmeticPacker.PartId,ICosmeticMeshPartBase> __0, Il2CppSystem.Collections.Generic.IEnumerable<CosmeticPacker.Input> __2)
        {
            var map=__0;var parts=__2;
            if(map==null || parts==null) return;
            // All modifications are staged. An unreadable/unsupported native part leaves this pack untouched.
            try
            {
                Il2CppSystem.Collections.Generic.Dictionary<CosmeticPacker.PartId,ICosmeticMeshPartBase> copy=null;
                lock(fabricGate)
                {
                    foreach(var input in NativeEnumerable.Items(parts))
                    {
                        if(input==null || input.Arguments==null || input.Arguments.IsImpeded || !fabricUsers.ContainsKey(FabricKey(input))) continue;
                        ICosmeticMeshPartBase original;
                        if(!map.TryGetValue(input.PartId,out original) || original==null) continue;
                        var fitted=FittedFabric(original);
                        if(fitted==null) continue;
                        if(copy==null) copy=new Il2CppSystem.Collections.Generic.Dictionary<CosmeticPacker.PartId,ICosmeticMeshPartBase>(map.Cast<Il2CppSystem.Collections.Generic.IDictionary<CosmeticPacker.PartId,ICosmeticMeshPartBase>>());
                        copy[input.PartId]=fitted.Cast<ICosmeticMeshPartBase>();
                    }
                }
                if(copy!=null) __0=copy;
            }
            catch(Exception error)
            {if(!fabricWarning) {fabricWarning=true;Mod.Log.Warning("Cosmetic fabric mesh deferred: "+error.GetBaseException().Message);}}
        }
        private static CosmeticMeshPart FittedFabric(ICosmeticMeshPartBase source)
        {
            var asset=source.TryCast<CosmeticMeshPart>();
            if(asset==null) return null;
            int key=asset.GetInstanceID();CosmeticMeshPart cached;
            if(fabricCache.TryGetValue(key,out cached)) return cached;
            // A bounded process-lifetime cache avoids destroying meshes still read by asynchronous native pack jobs.
            if(fabricCache.Count>=96) return null;
            var sourceLods=source.Lods;
            if(sourceLods==null || sourceLods.Length==0) {fabricCache[key]=null;return null;}
            var meshes=new Mesh[sourceLods.Length];var ownedMeshes=new List<Mesh>();CosmeticMeshPart clone=null;
            try
            {
                for(int i=0;i<meshes.Length;i++)
                {
                    Mesh original=sourceLods[i];meshes[i]=original;
                    if(original==null) continue;
                    if(!original.isReadable) throw new InvalidOperationException("Unreadable cosmetic fabric LOD: "+original.name);
                    var vertices=original.vertices;bool changed=false;
                    for(int j=0;j<vertices.Length;j++) {Vector3 fitted;if(FitFabricVertex(vertices[j],out fitted)) {vertices[j]=fitted;changed=true;}}
                    if(!changed) continue;
                    Mesh mesh=UnityEngine.Object.Instantiate(original);
                    ownedMeshes.Add(mesh);mesh.name=original.name+" (armor fabric fit)";mesh.hideFlags=HideFlags.HideAndDontSave;
                    mesh.vertices=vertices;mesh.RecalculateBounds();meshes[i]=mesh;
                }
                if(ownedMeshes.Count==0) {fabricCache[key]=null;return null;}
                clone=UnityEngine.Object.Instantiate(asset).TryCast<CosmeticMeshPart>();
                clone.name=asset.name+" (armor fabric fit)";clone.hideFlags=HideFlags.HideAndDontSave;
                // Both arrays point at the same active-platform fitted LODs; source compact arrays remain untouched.
                ArmorCrafting.Set(clone,"lods",new Il2CppReferenceArray<Mesh>(meshes));
                ArmorCrafting.Set(clone,"androidLods",new Il2CppReferenceArray<Mesh>(meshes));
                fabricCache.Add(key,clone);
                return clone;
            }
            catch
            {
                if(clone!=null) UnityEngine.Object.Destroy(clone);
                foreach(var mesh in ownedMeshes) UnityEngine.Object.Destroy(mesh);
                throw;
            }
        }
        internal static bool FitFabricVertex(Vector3 source,out Vector3 fitted)
        {
            fitted=source;
            float x=Mathf.Abs(source.x),z=source.z;
            if(float.IsNaN(x) || float.IsInfinity(x) || float.IsNaN(z) || float.IsInfinity(z) ||
                float.IsNaN(source.y) || float.IsInfinity(source.y) || z<=.995f || z>=1.355f || x>=.205f) return false;
            float radiusSquared=source.x*source.x+source.y*source.y;
            if(radiusSquared<=.0064f || radiusSquared>=.25f) return false;
            float radius=Mathf.Sqrt(radiusSquared);
            float target=FabricSkinRadius(z,Mathf.Atan2(source.y,x))+.008f;
            if(radius<=target) return false;
            float feather=FabricFeather((z-.995f)/.085f)*FabricFeather((1.355f-z)/.06f)*FabricFeather((.205f-x)/.052f);
            float compression=Mathf.Min(radius-target,.065f)*feather;
            if(compression<.0001f) return false;
            float scale=(radius-compression)/radius;
            fitted.x*=scale;fitted.y*=scale;return true;
        }
        private static float FabricFeather(float value) {value=Mathf.Clamp01(value);return value*value*(3f-2f*value);}
        private static float FabricSkinRadius(float height,float angle)
        {
            float row=Mathf.Clamp((height-1.005f)/.035f,0f,10f);
            float column=Mathf.Clamp((angle/Mathf.PI+.5f)*16f,0f,16f);
            int r=Mathf.Min((int)row,9),c=Mathf.Min((int)column,15);
            float a=Mathf.Lerp(FabricRadii[r,c],FabricRadii[r,c+1],column-c);
            float b=Mathf.Lerp(FabricRadii[r+1,c],FabricRadii[r+1,c+1],column-c);
            return Mathf.Lerp(a,b,row-r)*.001f;
        }
        private static readonly byte[,] FabricRadii = {
            {137,138,143,148,155,159,161,163,158,155,146,144,138,135,131,128,128},
            {121,122,127,132,137,144,149,152,150,150,142,140,134,132,128,125,126},
            {107,108,112,116,122,129,135,138,139,142,136,135,131,128,126,124,124},
            {99,101,106,111,117,123,130,132,133,135,132,132,129,129,126,124,124},
            {98,100,106,111,118,125,132,134,135,137,134,135,133,133,130,129,129},
            {101,104,111,116,124,132,140,142,142,143,139,141,137,137,135,133,134},
            {110,114,122,127,135,144,151,151,149,149,145,145,140,139,136,134,135},
            {118,124,132,138,147,156,162,162,158,157,149,147,141,138,134,131,131},
            {125,132,142,149,159,169,174,172,167,166,158,157,149,141,133,127,125},
            {130,139,150,160,171,183,185,182,178,178,175,171,156,144,132,125,123},
            {136,145,156,167,180,198,209,205,196,192,188,171,150,135,124,117,114}
        };
    }
}
