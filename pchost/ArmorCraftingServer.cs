using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Alta;
using Alta.Crafting;
using Alta.Networking;
using Alta.Networking.Scripts.Player;
using Alta.Saving;
using HarmonyLib;
using UnityEngine;

namespace ArmorRestored
{
    internal static class ArmorCraftingServer
    {
        internal static bool Installed;
        private sealed class Claim { }
        private static readonly ConditionalWeakTable<IProject,Claim> claimed = new ConditionalWeakTable<IProject,Claim>();
        private sealed class Released
        {
            internal GenericCraftingStation Station;
            internal Pickup Pickup;
            internal float At, Until;
        }
        private static readonly List<Released> pending = new List<Released>();
        internal static void Install()
        {
            if (Installed || !Mod.IsServer) return;
            ArmorCraftRecord.Register();
            var harmony = new HarmonyLib.Harmony("armorrestored.crafting-server");
            harmony.Patch(AccessTools.Method(typeof(SavingSystem),"Reinitialize"),
                postfix: new HarmonyMethod(typeof(ArmorCraftRecord),"Register"));
            harmony.Patch(AccessTools.Method(typeof(GenericCraftingStation),"SpawnResult"),
                prefix: new HarmonyMethod(typeof(ArmorCraftingServer),"SpawnResult"));
            harmony.Patch(AccessTools.Method(typeof(GenericCraftingStation),"ComponentReleased"),
                postfix: new HarmonyMethod(typeof(ArmorCraftingServer),"OnReleased"));
            harmony.Patch(AccessTools.Method(typeof(GenericCraftingComponent),"Alta.Crafting.IGenericCraftingComponent.Track"),
                postfix: new HarmonyMethod(typeof(ArmorCraftingServer),"TakeValidHeldIngredient"));
            harmony.Patch(AccessTools.Method(typeof(GenericCraftingPiece),"Pop"),
                prefix: new HarmonyMethod(typeof(ArmorCraftingServer),"BeforePop"),
                postfix: new HarmonyMethod(typeof(ArmorCraftingServer),"AfterPop"));
            Installed = true;
            ArmorCrafting.CraftLog("server-hooks","server auto-place Track hook and release hook installed",0f);
        }
        internal static void AddRecord(GameObject prefab)
        {
            if (Mod.IsServer && Installed && prefab.GetComponent<ArmorCraftRecord>() == null) prefab.AddComponent<ArmorCraftRecord>();
        }
        private static IProject Project(GenericCraftingStation station) { return ArmorCrafting.Get(station,"currentProject") as IProject; }
        private static bool NativeBench(GenericCraftingStation station)
        {
            if (station == null || station.Entity == null) return false;
            var entity = station.Entity;
            return (entity.Prefab != null && entity.Prefab.Hash == 28396U) ||
                (entity.PrefabRoot != null && entity.PrefabRoot.Prefab != null && entity.PrefabRoot.Prefab.Hash == 28396U);
        }
        private static byte[] Digest(int index)
        {
            var bytes = new byte[36];
            Array.Copy(ArmorCrafting.Fingerprint,bytes,32);
            Array.Copy(BitConverter.GetBytes((uint)(52830+index)),0,bytes,32,4);
            using (var sha = SHA256.Create()) return sha.ComputeHash(bytes);
        }
        private static bool Equal(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            int difference = 0; for (int i=0;i<a.Length;i++) difference |= a[i]^b[i]; return difference==0;
        }
        private static uint MaterialHash(PhysicalMaterial material) { return material == null ? 0U : material.Hash; }
        private static bool Primary(uint source) { return source == 50988U || source == 47760U; }
        private static PhysicalMaterial Resolve(uint hash)
        {
            if (hash == 0U) return null;
            var material = HashedGeneralValue<PhysicalMaterial>.Get(hash);
            if (material == null) throw new InvalidOperationException("Unknown ingredient material " + hash);
            return material;
        }
        private static readonly HashSet<GenericCraftingComponent> inserting = new HashSet<GenericCraftingComponent>();
        private static void TakeValidHeldIngredient(GenericCraftingComponent __instance, GenericCraftingStation __0)
        {
            if (!Mod.IsServer || !ArmorCrafting.Ready || __instance == null || !NativeBench(__0)) return;
            var project = Project(__0);
            if (project == null || ArmorCrafting.Index(project.Recipe) < 0 || inserting.Contains(__instance)) return;
            var pickup = __instance.Pickup;
            string diagnosticKey="take-"+__instance.GetInstanceID();
            ArmorCrafting.CraftLog(diagnosticKey+"-track","held Track callback station="+__0.GetInstanceID()+" recipe="+project.Recipe.Hash+
                " component="+__instance.GetInstanceID()+" pickup="+(pickup!=null)+
                " held="+(pickup!=null && pickup.IsInteractedWith)+" docked="+(pickup!=null && pickup.IsDocked));
            if (pickup == null || pickup.Entity == null || pickup.Entity.IsBeingDestroyed || pickup.IsDocked || !pickup.IsInteractedWith) return;
            int key = (int)ArmorCrafting.Get(__instance,"bestPieceKey");
            GenericCraftingPiece piece;
            if (key <= 0 || !project.MappedPieces.TryGetValue(key,out piece) || piece == null || !piece.AcceptsComponent(__instance))
            { ArmorCrafting.CraftLog(diagnosticKey,"auto-place waiting: no valid native target; cachedKey="+key); return; }
            float distance=Vector3.Distance((Vector3)ArmorCrafting.Get(__instance,"ObjectCentrePoint"),piece.CenterPosition);
            // Native Track already enforces its own preview distance. A valid
            // green target must not be blocked by a second, smaller radius.
            ArmorCrafting.CraftLog(diagnosticKey+"-attempt","auto-place attempting target="+key+" distance="+distance.ToString("F3"),0f);
            inserting.Add(__instance);
            try
            {
                // Use the native release event/coroutine exclusively. Mixing
                // synchronous insertion with delayed native insertion races sync.
                var hands = new List<Interactor>(pickup.Interactors);
                foreach (var hand in hands) if (hand != null) pickup.FinishInteraction(hand);
                // ComponentReleased schedules the native insertion at end of
                // frame. Do NOT insert/destroy here during hand-release events:
                // the pickup and interaction state must finish syncing first.
                ArmorCrafting.CraftLog(diagnosticKey+"-result","auto-place native hand release queued; target="+key+
                    " remainingHeld="+(pickup!=null && pickup.IsInteractedWith),0f);
            }
            catch (Exception error) { Mod.Log.Warning("Armor ingredient auto-place: " + error.GetBaseException().Message); }
            finally { inserting.Remove(__instance); }
        }
        private static ArmorCraftOrigin Capture(IProject project, int index, out uint primaryMaterial)
        {
            if (!project.IsComplete) throw new InvalidOperationException("Armor project is not complete");
            var definitions = project.Recipe.Pieces;
            uint[] sources = new uint[definitions.Length], materials = new uint[definitions.Length];
            primaryMaterial = 0; bool found = false;
            for (int i=0;i<definitions.Length;i++)
            {
                var piece = project.MappedPieces[definitions[i].hash];
                if (!piece.IsFilled || !piece.IsComplete) throw new InvalidOperationException("Missing completed armor ingredient");
                sources[i] = definitions[i].source.Prefab.Hash;
                materials[i] = MaterialHash(piece.Material);
                Resolve(materials[i]);
                if (!Primary(sources[i])) continue;
                // One native result channel: first primary input sets the result
                // substrate/stats; all mixed input identities remain in origin.
                if (!found) primaryMaterial = materials[i];
                found = true;
            }
            if (!found || (index >= 8 && primaryMaterial == 0)) throw new InvalidOperationException("Missing armor material");
            if (project.Recipe.ResultHash != 52830U+(uint)index) throw new InvalidOperationException("Armor result identity mismatch");
            return new ArmorCraftOrigin(project.Recipe.ResultHash,Digest(index),sources,materials);
        }
        private static bool SpawnResult(GenericCraftingStation __instance, ref NetworkEntity __result)
        {
            var project = Project(__instance);
            int index = project == null ? -1 : ArmorCrafting.Index(project.Recipe);
            if (index < 0 || !Mod.IsServer) return true;
            __result = null;
            if (!ArmorCrafting.Ready || !NativeBench(__instance)) return false;
            Claim previous;
            if (claimed.TryGetValue(project,out previous)) return false;
            NetworkEntity output = null;
            bool committed = false;
            try
            {
                uint material;
                var origin = Capture(project,index,out material);
                var offset = project.Recipe.SpawnOffset;
                var parent = __instance.PiecesParent;
                // Root.Township's callback runs AFTER initialization but BEFORE
                // assigning Chunk. Materials and origin are set before replication.
                var spawned = SpawnHelper.Spawn(project.Recipe.ResultHash,SpawnData.DontGenerate,__instance.Entity.Chunk,
                    parent.position+offset.position,parent.rotation*offset.rotation,entity =>
                    {
                        output = entity;
                        var record = entity.GetComponent<ArmorCraftRecord>();
                        if (record == null) throw new InvalidOperationException("Crafted armor has no native saved origin component");
                        record.Store(origin);
                        if (index >= 8) ArmorMaterials.Restore(entity,material);
                    });
                if (spawned == null || spawned != output || spawned.IsBeingDestroyed) throw new InvalidOperationException("Armor result spawn failed");
                claimed.Add(project,new Claim());
                committed = true; // do not ever spawn another result for this project
                __result = output;
                Invoke(__instance,"ClearProject",false);
                Invoke(__instance,"SetupProjectFromPage");
            }
            catch (Exception error)
            {
                if (!committed && output != null && !output.IsBeingDestroyed) output.SceneDestroy();
                if (committed)
                {
                    // A failed native cleanup must not leave a completed project
                    // in station saves alongside its already spawned output.
                    ArmorCrafting.Set(__instance,"currentProject",null);
                    try { project.Clear(false); Invoke(__instance,"ClearLOD"); }
                    catch (Exception cleanup) { Mod.Log.Error("Armor station cleanup: " + cleanup.GetBaseException().Message); }
                    ((MethodSync)ArmorCrafting.Get(__instance,"syncFull")).SendToChunks(new IPlayer[0]);
                }
                Mod.Log.Error("Armor crafting " + (committed ? "committed; station cleanup needs attention: " : "refused without consuming project: ") + error.GetBaseException().Message);
            }
            return false;
        }
        private static void OnReleased(GenericCraftingStation __instance, Interactable __0)
        {
            if (!Mod.IsServer || !ArmorCrafting.Ready || __0 == null || !NativeBench(__instance)) return;
            var activeProject=Project(__instance);
            if (activeProject!=null && ArmorCrafting.Index(activeProject.Recipe)>=0)
                ArmorCrafting.CraftLog("release-"+__instance.GetInstanceID(),"native ComponentReleased station="+__instance.GetInstanceID()+" recipe="+activeProject.Recipe.Hash,0f);
            var pickup = __0.Entity == null ? null : __0.Entity.CommonPickup;
            if (pickup == null || pickup.Entity.Prefab == null || !ArmorConstructionData.AllowedResult(pickup.Entity.Prefab.Hash)) return;
            if (pending.Count >= 128) return;
            foreach (var release in pending) if (release.Pickup == pickup && release.Station == __instance) return;
            pending.Add(new Released { Station=__instance, Pickup=pickup, At=Time.unscaledTime+.1f, Until=Time.unscaledTime+1f });
        }
        internal static void Tick()
        {
            for (int i=pending.Count-1;i>=0;i--)
            {
                var release = pending[i];
                if (Time.unscaledTime < release.At) continue;
                if (release.Pickup == null || release.Station == null || Time.unscaledTime > release.Until)
                { pending.RemoveAt(i); continue; }
                if (release.Pickup.IsInteractedWith) continue;
                pending.RemoveAt(i);
                try { Disassemble(release.Station,release.Pickup); }
                catch (Exception error) { Mod.Log.Warning("Armor dismantle refused: " + error.GetBaseException().Message); }
            }
        }
        private static void Disassemble(GenericCraftingStation station, Pickup armor)
        {
            if (!Mod.IsServer || !ArmorCrafting.Ready || !NativeBench(station) || Project(station) != null ||
                armor == null || armor.Entity == null || armor.Entity.IsBeingDestroyed || armor.IsDocked || armor.IsInteractedWith ||
                station.Entity.IsBeingDestroyed || station.Entity.Chunk == null || !(bool)ArmorCrafting.Get(station,"isSyncingAllowed")) return;
            var dock = (Alta.Inventory.PickupDock)ArmorCrafting.Get(station,"recipeDock");
            var tracked = (IDictionary)ArmorCrafting.Get(station,"trackedPickups");
            if (dock == null || dock.QuantityStored != 0 || armor.Rigidbody == null || !tracked.Contains(armor.Rigidbody)) return;
            int index = Catalog.PieceOf(armor.Entity.Prefab.Hash);
            if (index < 0 || index >= 16) return; // never turn Wyrm loot into a recipe
            var recipe = ArmorCrafting.Recipes[index];
            var definitions = recipe.Pieces;
            var record = armor.Entity.GetComponent<ArmorCraftRecord>();
            ArmorCraftOrigin origin = record == null ? null : record.Read();
            if (record != null && record.HasRecord && origin == null) throw new InvalidOperationException("Invalid saved armor origin");
            if (origin != null && (origin.Result != armor.Entity.Prefab.Hash || origin.Count != definitions.Length || !Equal(origin.RecipeDigest,Digest(index))))
                throw new InvalidOperationException("Saved armor recipe version is unavailable");
            var materials = new PhysicalMaterial[definitions.Length];
            for (int i=0;i<definitions.Length;i++)
            {
                if (origin != null)
                {
                    if (origin.SourceAt(i) != definitions[i].source.Prefab.Hash) throw new InvalidOperationException("Saved ingredient differs from recipe");
                    materials[i] = Resolve(origin.MaterialAt(i));
                }
                else
                {
                    // Loot/admin armor has no spent ingredients. Use the fixed
                    // recipe and its native input defaults, retaining the armor's
                    // actual primary metal. Never invent rare closure materials.
                    var template = definitions[i].source.Prefab.GetComponent<GenericCraftingComponent>();
                    materials[i] = index >= 8 && Primary(definitions[i].source.Prefab.Hash)
                        ? Resolve(ArmorMaterials.Read(armor.Entity)) : template.Material;
                }
                if (index >= 8 && Primary(definitions[i].source.Prefab.Hash) && materials[i] == null)
                    throw new InvalidOperationException("Armor primary material is unavailable");
            }
            NetworkEntity loose = null;
            IProject project = null;
            bool consumed = false;
            ArmorCrafting.Set(station,"isSyncingAllowed",false);
            try
            {
                project = recipe.InitializeProject(station);
                ArmorCrafting.Set(station,"currentProject",project);
                int outer = 0;
                for (int i=0;i<definitions.Length;i++)
                {
                    var piece = project.MappedPieces[definitions[i].hash];
                    piece.ApplySaveData(new PieceSaveData { hash=definitions[i].hash, isComplete=true, material=materials[i] },station.Entity.Chunk);
                    piece.SetEnabled(true);
                    if (definitions[i].level > definitions[outer].level) outer=i;
                }
                project.SetMaxLevel();
                var detached = project.MappedPieces[definitions[outer].hash];
                SpawnHelper.Spawn(definitions[outer].source.Prefab,SpawnData.DontGenerate,station.Entity.Chunk,
                    station.PiecesParent.TransformPoint(detached.CenterPosition),station.PiecesParent.rotation,entity =>
                    {
                        loose=entity;
                        var component=entity.GetComponent<GenericCraftingComponent>();
                        if (component==null) throw new InvalidOperationException("Refund is not a native crafting ingredient");
                        component.SetupAsRefuse(materials[outer]);
                    });
                if (loose==null || loose.IsBeingDestroyed) throw new InvalidOperationException("Could not detach outer ingredient");
                detached.Empty();
                Invoke(station,"SetupLOD");
                armor.Entity.SceneDestroy();
                consumed=armor.Entity==null || armor.Entity.IsBeingDestroyed;
                if (!consumed) throw new InvalidOperationException("Armor input was not consumed");
            }
            finally
            {
                consumed=consumed || armor==null || armor.Entity==null || armor.Entity.IsBeingDestroyed;
                try
                {
                    if (!consumed)
                    {
                        try { if (loose!=null && !loose.IsBeingDestroyed) loose.SceneDestroy(); }
                        finally
                        {
                            try { if (project!=null) project.Clear(false); }
                            finally { ArmorCrafting.Set(station,"currentProject",null); Invoke(station,"ClearLOD"); }
                        }
                    }
                }
                finally { ArmorCrafting.Set(station,"isSyncingAllowed",true); }
                if (consumed) ((MethodSync)ArmorCrafting.Get(station,"syncFull")).SendToChunks(new IPlayer[0]);
            }
        }
        private static bool BeforePop(GenericCraftingPiece __instance, out IProject __state)
        {
            __state = ArmorCrafting.Get(__instance,"project") as IProject;
            if (__state == null || ArmorCrafting.Index(__state.Recipe)<0) { __state=null; return true; }
            Claim consumed;
            if (claimed.TryGetValue(__state,out consumed)) { __state=null; return false; }
            ArmorCrafting.CraftLog("pop-"+__instance.GetInstanceID(),"native Pop recipe="+__state.Recipe.Hash+" filled="+__instance.IsFilled+" complete="+__instance.IsComplete,0f);
            return true;
        }
        private static void AfterPop(IProject __state)
        {
            if (__state==null || __state.Station==null || Project(__state.Station)!=__state) return;
            bool filled=false; foreach(var piece in __state.MappedPieces.Values) if(piece.IsFilled) filled=true;
            if (!filled) Invoke(__state.Station,"ChangedPieces");
        }
        private static object Invoke(object instance,string name,params object[] args)
        { return AccessTools.Method(instance.GetType(),name).Invoke(instance,args); }
    }
}
