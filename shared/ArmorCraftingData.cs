using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
#if ARMOR_QUEST
namespace ArmorRestoredQuest
#else
namespace ArmorRestored
#endif
{
    // Platform-neutral construction data, not a replacement for armor.pack.
    // Loaded and validated before native recipe publication.
    internal sealed class ArmorConstructionData
    {
        internal sealed class Ingredient
        {
            internal uint Source;
            internal int Parent, Level, Clearance;
            internal bool RequiresNail;
            internal float[] NailPose;
            internal float[] Vertices; // xyz, normal xyz, uv: eight floats per vertex
            internal int[][] Submeshes;
        }
        internal sealed class Plan
        {
            internal string Id, Glyph;
            internal Ingredient[] Ingredients;
            // Combined hips in the donor must NEVER register as a left/right recipe.
            internal bool ReferenceOnly { get { return Id.EndsWith(".tassets", StringComparison.Ordinal); } }
        }
        private readonly Dictionary<string, Plan> plans = new Dictionary<string, Plan>(StringComparer.Ordinal);
        internal Plan Find(string id)
        {
            Plan plan;
            return id != null && plans.TryGetValue(id, out plan) && !plan.ReferenceOnly ? plan : null;
        }
        internal static bool AllowedResult(uint hash)
        {
            // Saved origins: Leather/Metal, legacy Hebios, and Wood. Exclude Wyrm and
            // Mushroom. Runtime Craftable() separately excludes legacy Hebios recipes.
            return hash >= 52830U && hash <= 52845U || hash >= 52855U && hash <= 52870U;
        }
        private static int Count(BinaryReader reader, int min, int max)
        {
            int value = reader.ReadInt32();
            if (value < min || value > max) throw new InvalidDataException("Invalid construction count");
            return value;
        }
        private static string Text(BinaryReader reader)
        {
            int length = Count(reader, 1, 160);
            byte[] bytes = reader.ReadBytes(length);
            if (bytes.Length != length) throw new EndOfStreamException();
            return new UTF8Encoding(false, true).GetString(bytes);
        }
        private static float Number(BinaryReader reader)
        {
            float value = reader.ReadSingle();
            if (float.IsNaN(value) || float.IsInfinity(value)) throw new InvalidDataException("Non-finite construction value");
            return value;
        }
        internal static ArmorConstructionData Read(Stream stream)
        {
            // Embedded resources are seekable. Bound allocation even if a resource
            // is accidentally replaced with corrupt or incompatible content.
            if (stream == null) throw new ArgumentNullException("stream");
            if (!stream.CanSeek || stream.Length > 8 * 1024 * 1024) throw new InvalidDataException("Invalid construction resource size");
            var result = new ArmorConstructionData();
            using (var reader = new BinaryReader(stream))
            {
                if (reader.ReadUInt32() != 0x46524341U || reader.ReadInt32() != 1)
                    throw new InvalidDataException("Unsupported construction resource");
                byte[] digest = reader.ReadBytes(32);
                if (digest.Length != 32) throw new EndOfStreamException();
                if (BitConverter.ToString(digest).Replace("-", "") != "119EDC0C48D29C7DBA66F5EA3CC7537E17601BF32184E4EDDEACE6716E897B4A")
                    throw new InvalidDataException("Construction model identity mismatch");
                int count = Count(reader, 1, 32);
                int totalVertices = 0;
                for (int i = 0; i < count; i++)
                {
                    var plan = new Plan { Id = Text(reader), Glyph = Text(reader) };
                    string[] parts = plan.Id.Split('.');
                    if (parts.Length != 2 || (parts[0] != "leather" && parts[0] != "metal" && parts[0] != "hebios" && parts[0] != "wood") || !ValidSlot(parts[1]))
                        throw new InvalidDataException("Unapproved crafting result: " + plan.Id);
                    if (plan.Glyph.IndexOf('/') >= 0 || plan.Glyph.IndexOf('\\') >= 0 || !plan.Glyph.EndsWith(".png", StringComparison.Ordinal))
                        throw new InvalidDataException("Invalid glyph resource name");
                    int ingredients = Count(reader, 1, 32);
                    plan.Ingredients = new Ingredient[ingredients];
                    for (int j = 0; j < ingredients; j++)
                    {
                        var input = new Ingredient();
                        input.Source = reader.ReadUInt32();
                        uint primary = parts[0] == "wood" ? 47118U : parts[0] != "leather" ? 50988U : 47760U;
                        uint closure = parts[0] == "wood" ? 43836U : parts[0] == "metal" ? 62594U : 63204U;
                        if (input.Source != primary && input.Source != closure)
                            throw new InvalidDataException("Unexpected ingredient source");
                        input.Parent = reader.ReadInt32();
                        input.Level = Count(reader, 0, 31);
                        byte nail = reader.ReadByte();
                        if (nail > 1) throw new InvalidDataException("Invalid nail flag");
                        input.RequiresNail = nail == 1;
                        input.NailPose = new float[7];
                        for (int k = 0; k < 7; k++) input.NailPose[k] = Number(reader);
                        float norm = 0;
                        for (int k = 3; k < 7; k++) norm += input.NailPose[k] * input.NailPose[k];
                        if (Math.Abs(norm - 1f) > .01f) throw new InvalidDataException("Invalid nail rotation");
                        input.Clearance = Count(reader, -2, 180);
                        int vertices = Count(reader, 1, 65000);
                        totalVertices += vertices;
                        if (totalVertices > 200000) throw new InvalidDataException("Construction vertex budget exceeded");
                        input.Vertices = new float[vertices * 8];
                        for (int k = 0; k < input.Vertices.Length; k++) input.Vertices[k] = Number(reader);
                        input.Submeshes = new int[Count(reader, 1, 16)][];
                        int totalIndices = 0;
                        for (int k = 0; k < input.Submeshes.Length; k++)
                        {
                            int indices = Count(reader, 0, 390000);
                            totalIndices += indices;
                            if (indices % 3 != 0 || totalIndices > 390000)
                                throw new InvalidDataException("Invalid triangle index budget");
                            input.Submeshes[k] = new int[indices];
                            for (int n = 0; n < indices; n++) input.Submeshes[k][n] = Count(reader, 0, vertices - 1);
                        }
                        if (totalIndices == 0) throw new InvalidDataException("Empty ingredient mesh");
                        plan.Ingredients[j] = input;
                    }
                    ValidateHierarchy(plan);
                    result.plans.Add(plan.Id, plan);
                }
                if (stream.Position != stream.Length) throw new InvalidDataException("Trailing construction data");
            }
            return result;
        }
        private static bool ValidSlot(string slot)
        {
            switch (slot)
            {
                case "helmet": case "chest": case "shoulder_l": case "shoulder_r":
                case "gauntlet_l": case "gauntlet_r": case "tasset_l": case "tasset_r": case "tassets": return true;
                default: return false;
            }
        }
        private static void ValidateHierarchy(Plan plan)
        {
            for (int i = 0; i < plan.Ingredients.Length; i++)
            {
                var seen = new HashSet<int>();
                int current = i;
                while (current != -1)
                {
                    if (current < 0 || current >= plan.Ingredients.Length || !seen.Add(current))
                        throw new InvalidDataException("Invalid construction hierarchy");
                    current = plan.Ingredients[current].Parent;
                }
            }
        }
    }

    // Recipe identity and per-input material provenance, used by ArmorCraftRecord
    // and the worn-slot save; it is NOT an external object-id sidecar (native
    // entity ids are reused). Missing/invalid data must never trigger a refund.
    internal sealed class ArmorCraftOrigin
    {
        private readonly byte[] recipeDigest;
        private readonly uint[] sources, materials;
        internal readonly uint Result;
        internal int Count { get { return sources.Length; } }
        internal uint SourceAt(int index) { return sources[index]; }
        internal uint MaterialAt(int index) { return materials[index]; }
        internal byte[] RecipeDigest { get { return (byte[])recipeDigest.Clone(); } }
        internal ArmorCraftOrigin(uint result, byte[] recipe, uint[] inputSources, uint[] inputMaterials)
        {
            if (!ArmorConstructionData.AllowedResult(result)) throw new InvalidDataException("Result is not craftable armor");
            if (recipe == null || recipe.Length != 32) throw new InvalidDataException("Invalid recipe identity");
            if (inputSources == null || inputMaterials == null || inputSources.Length == 0 ||
                inputSources.Length > 32 || inputSources.Length != inputMaterials.Length)
                throw new InvalidDataException("Invalid ingredient provenance");
            uint primary = result >= 52863U ? 47118U : result < 52838U ? 47760U : 50988U;
            uint closure = result >= 52863U ? 43836U : result < 52838U || result >= 52855U ? 63204U : 62594U;
            for (int i = 0; i < inputSources.Length; i++)
                if (inputSources[i] != primary && inputSources[i] != closure)
                    throw new InvalidDataException("Invalid ingredient source");
            Result = result;
            recipeDigest = (byte[])recipe.Clone();
            sources = (uint[])inputSources.Clone();
            materials = (uint[])inputMaterials.Clone(); // zero means native unmaterialed input, not 'reroll'
        }
        internal byte[] Encode()
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream))
                {
                    writer.Write(0x4749524FU); // ORIG
                    writer.Write((byte)1);
                    writer.Write(Result);
                    writer.Write(recipeDigest);
                    writer.Write((byte)Count);
                    for (int i = 0; i < Count; i++) { writer.Write(sources[i]); writer.Write(materials[i]); }
                    writer.Flush();
                    byte[] payload = stream.ToArray();
                    using (var sha = SHA256.Create()) writer.Write(sha.ComputeHash(payload));
                    writer.Flush();
                    return stream.ToArray();
                }
            }
        }
        internal static ArmorCraftOrigin Decode(byte[] data)
        {
            // Header 42, at least one 8-byte ingredient, SHA256 32.
            if (data == null || data.Length < 82 || data.Length > 330)
                throw new InvalidDataException("Invalid craft record size");
            int payloadLength = data.Length - 32;
            using (var sha = SHA256.Create())
            {
                byte[] expected = sha.ComputeHash(data, 0, payloadLength);
                int mismatch = 0;
                for (int i = 0; i < 32; i++) mismatch |= expected[i] ^ data[payloadLength + i];
                if (mismatch != 0) throw new InvalidDataException("Corrupt craft record");
            }
            using (var stream = new MemoryStream(data, false))
            using (var reader = new BinaryReader(stream))
            {
                if (reader.ReadUInt32() != 0x4749524FU || reader.ReadByte() != 1)
                    throw new InvalidDataException("Unsupported craft record version");
                uint result = reader.ReadUInt32();
                byte[] recipe = reader.ReadBytes(32);
                int count = reader.ReadByte();
                if (count < 1 || count > 32 || payloadLength != 42 + 8 * count)
                    throw new InvalidDataException("Invalid craft record ingredient count");
                uint[] sources = new uint[count], materials = new uint[count];
                for (int i = 0; i < count; i++) { sources[i] = reader.ReadUInt32(); materials[i] = reader.ReadUInt32(); }
                return new ArmorCraftOrigin(result, recipe, sources, materials);
            }
        }
    }
    // BEGIN WORN GROUPS
    internal static class WornIngredientGroups
    {
        private static readonly Dictionary<string,string> Maps=new Dictionary<string,string> {
        // Generated by tools/build_worn_groups.py; triangle order unchanged.
            {"leather_helmet_cap_LOD0", "AEI="},
            {"leather_helmet_flop_LOD0", "Av8CHw=="},
            {"leather_helmet_main_LOD0", "Af8BJQ=="},
            {"leather_chest_buckle_front_LOD0", "AoI="},
            {"leather_chest_buckle_side_LOD0", "AoI="},
            {"leather_chest_main_LOD0", "Af8BNQAQAQEABAEDAAoBAgAUAQYAIgECAAIBJAAQAQEABQEDABIBAgAFAQYALA=="},
            {"leather_chest_ring_LOD0", "AMA="},
            {"leather_chest_rivets_back_LOD0", "AEA="},
            {"leather_chest_rivets_front_LOD0", "AEA="},
            {"leather_chest_rivets_strap_LOD0", "AEA="},
            {"leather_chest_stitches_LOD0", "AZA="},
            {"leather_chest_strap_bottom_LOD0", "Akw="},
            {"leather_chest_strap_L_LOD0", "Av8CKQ=="},
            {"leather_chest_strap_R_LOD0", "Apw="},
            {"leather_chest_strap_side_LOD0", "Akw="},
            {"leather_chest_under_back_LOD0", "AVA="},
            {"leather_chest_under_front_LOD0", "AFA="},
            {"sash_buckle_LOD0", "AoI="},
            {"sash_main_LOD0", "Av8CAQ=="},
            {"sash_pad_LOD0", "Amg="},
            {"sash_rivets_LOD0", "AIA="},
            {"sash_side_L_pad_LOD0", "Amg="},
            {"sash_side_L_rivets_LOD0", "AIA="},
            {"sash_side_R_pad_LOD0", "Amg="},
            {"sash_side_R_rivets_LOD0", "AIA="},
            {"sash_strap_LOD0", "AmQ="},
            {"leather_shoulder_L_pad_LOD0", "AHw="},
            {"leather_shoulder_L_rivets_back_LOD0", "AEA="},
            {"leather_shoulder_L_rivets_front_LOD0", "AEA="},
            {"leather_upperarm_L_buckle_LOD0", "AoI="},
            {"leather_upperarm_L_pad_LOD0", "Adg="},
            {"leather_upperarm_L_rivets_back_LOD0", "AEA="},
            {"leather_upperarm_L_rivets_front_LOD0", "AEA="},
            {"leather_upperarm_L_strap_LOD0", "Aqw="},
            {"leather_shoulder_R_pad_LOD0", "AHw="},
            {"leather_shoulder_R_rivets_back_LOD0", "AEA="},
            {"leather_shoulder_R_rivets_front_LOD0", "AEA="},
            {"leather_upperarm_R_buckle_LOD0", "AoI="},
            {"leather_upperarm_R_pad_LOD0", "Adg="},
            {"leather_upperarm_R_rivets_back_LOD0", "AEA="},
            {"leather_upperarm_R_rivets_front_LOD0", "AEA="},
            {"leather_upperarm_R_strap_LOD0", "Aqw="},
            {"leather_gauntlet_L_glove_flair_LOD0", "AP8A/wD/AP8A/wD/AP8A/wD/ABs="},
            {"leather_gauntlet_L_buckle_lower_LOD0", "AII="},
            {"leather_gauntlet_L_buckle_upper_LOD0", "AII="},
            {"leather_gauntlet_L_pad_LOD0", "AP8A3w=="},
            {"leather_gauntlet_L_strap_lower_LOD0", "Aqw="},
            {"leather_gauntlet_L_strap_upper_LOD0", "Aaw="},
            {"leather_gauntlet_R_glove_flair_LOD0", "AP8A/wD/AP8A/wD/AP8A/wD/ABs="},
            {"leather_gauntlet_R_buckle_lower_LOD0", "AII="},
            {"leather_gauntlet_R_buckle_upper_LOD0", "AII="},
            {"leather_gauntlet_R_pad_LOD0", "AP8A3w=="},
            {"leather_gauntlet_R_strap_lower_LOD0", "Aqw="},
            {"leather_gauntlet_R_strap_upper_LOD0", "Aaw="},
            {"leather_tasset_L_lower_LOD0", "AdA="},
            {"leather_tasset_L_upper_LOD0", "ADQCEAAk"},
            {"leather_tasset_R_lower_LOD0", "AdA="},
            {"leather_tasset_R_upper_LOD0", "ADQCEAAk"},
            {"helmetFrame_01_LOD0", "AP8AVgMYAL8DGAAw"},
            {"grill_02_LOD0", "Aiw="},
            {"grill_03_LOD0", "Aiw="},
            {"grill_04_LOD0", "Aiw="},
            {"grill_05_LOD0", "Aiw="},
            {"grill_06_LOD0", "Aiw="},
            {"helmetVisor_01_LOD0", "AeQ="},
            {"cuirass_01_LOD0 leather-fit", "AP8AoQIEAP8ADgIEAOM="},
            {"cuirass_02_LOD0 leather-fit", "Af8BBw=="},
            {"pauldron_L_01_LOD0 leather-fit", "AOA="},
            {"rerebrace_L_01_LOD0 leather-fit", "AXQ="},
            {"rerebrace_L_02_LOD0 leather-fit", "AnQ="},
            {"pauldron_R_01_LOD0 leather-fit", "AOA="},
            {"rerebrace_R_01_LOD0 leather-fit", "AXQ="},
            {"rerebrace_R_02_LOD0 leather-fit", "AnQ="},
            {"metal_gauntlet_L_glove_flair_LOD0", "/////////////////////////xs="},
            {"knuckle_L_index3_LOD0 leather-fit", "AEw="},
            {"knuckle_L_index2_LOD0 leather-fit", "ADg="},
            {"knuckle_L_index1_LOD0 leather-fit", "ADg="},
            {"knuckle_L_index_LOD0 leather-fit", "ADg="},
            {"knuckle_L_little1_LOD0 leather-fit", "ADg="},
            {"knuckle_L_little_LOD0 leather-fit", "ADg="},
            {"knuckle_L_little2_LOD0 leather-fit", "ADg="},
            {"knuckle_L_little3_LOD0 leather-fit", "AEw="},
            {"knuckle_L_middle1_LOD0 leather-fit", "ADg="},
            {"knuckle_L_middle_LOD0 leather-fit", "ADg="},
            {"knuckle_L_middle2_LOD0 leather-fit", "ADg="},
            {"knuckle_L_middle3_LOD0 leather-fit", "AEw="},
            {"palmGuard_L_LOD0 leather-fit", "AIg="},
            {"knuckle_L_ring1_LOD0 leather-fit", "ADg="},
            {"knuckle_L_ring_LOD0 leather-fit", "ADg="},
            {"knuckle_L_ring2_LOD0 leather-fit", "ADg="},
            {"knuckle_L_ring3_LOD0 leather-fit", "AEw="},
            {"knuckle_L_thumb1_LOD0 leather-fit", "ADg="},
            {"knuckle_L_thumb_LOD0 leather-fit", "ADg="},
            {"knuckle_L_thumb_01_LOD0 leather-fit", "ADg="},
            {"knuckle_L_thumb_02_LOD0 leather-fit", "AEw="},
            {"couter_L_01_LOD0 leather-fit", "Af8BpQ=="},
            {"vambrace_L_01_LOD0 leather-fit", "Af8BXQ=="},
            {"vambrace_L_02_LOD0 leather-fit", "Ang="},
            {"metal_gauntlet_R_glove_flair_LOD0", "/////////////////////////xs="},
            {"knuckle_R_index3_LOD0 leather-fit", "AEw="},
            {"knuckle_R_index2_LOD0 leather-fit", "ADg="},
            {"knuckle_R_index1_LOD0 leather-fit", "ADg="},
            {"knuckle_R_index_LOD0 leather-fit", "ADg="},
            {"knuckle_R_little1_LOD0 leather-fit", "ADg="},
            {"knuckle_R_little_LOD0 leather-fit", "ADg="},
            {"knuckle_R_little2_LOD0 leather-fit", "ADg="},
            {"knuckle_R_little3_LOD0 leather-fit", "AEw="},
            {"knuckle_R_middle1_LOD0 leather-fit", "ADg="},
            {"knuckle_R_middle_LOD0 leather-fit", "ADg="},
            {"knuckle_R_middle2_LOD0 leather-fit", "ADg="},
            {"knuckle_R_middle3_LOD0 leather-fit", "AEw="},
            {"palmGuard_R_LOD0 leather-fit", "AIg="},
            {"knuckle_R_ring1_LOD0 leather-fit", "ADg="},
            {"knuckle_R_ring_LOD0 leather-fit", "ADg="},
            {"knuckle_R_ring2_LOD0 leather-fit", "ADg="},
            {"knuckle_R_ring3_LOD0 leather-fit", "AEw="},
            {"knuckle_R_thumb1_LOD0 leather-fit", "ADg="},
            {"knuckle_R_thumb_LOD0 leather-fit", "ADg="},
            {"knuckle_R_thumb_01_LOD0 leather-fit", "ADg="},
            {"knuckle_R_thumb_02_LOD0 leather-fit", "AEw="},
            {"couter_R_01_LOD0 leather-fit", "Af8BpQ=="},
            {"vambrace_R_01_LOD0 leather-fit", "Af8BXQ=="},
            {"vambrace_R_02_LOD0 leather-fit", "Ang="},
            {"Tasset_lower_L_01_LOD0 leather-fit", "AawCMA=="},
            {"Tasset_upper_L_01_LOD0 leather-fit", "AKQ="},
            {"Tasset_lower_R_01_LOD0 leather-fit", "AawCMA=="},
            {"Tasset_upper_R_01_LOD0 leather-fit", "AKQ="},
        };
        internal static byte[] Get(string name)
        {
            if (name != null && (name.StartsWith("hebios/",StringComparison.Ordinal) || name.StartsWith("wood/",StringComparison.Ordinal)))
            {
                string[] fields=name.Split('/'); int group,count;
                if(fields.Length!=5 || !int.TryParse(fields[2],out group) || !int.TryParse(fields[3],out count) ||
                    (group!=255 && (group<0 || group>3)) || count<1 || count>30000)
                    throw new InvalidDataException("Invalid Hebios ingredient mesh name");
                var groups=new byte[count]; for(int i=0;i<count;i++) groups[i]=(byte)group;
                return groups;
            }
            string encoded; if(!Maps.TryGetValue(name,out encoded)) return null;
            byte[] runs=Convert.FromBase64String(encoded); var values=new List<byte>();
            for(int i=0;i<runs.Length;i+=2) for(int j=0;j<runs[i+1];j++) values.Add(runs[i]);
            return values.ToArray();
        }
        internal static uint Material(int[] row,int slot,int group,uint fallback)
        {
            int index=16+slot*32+group;
            return group<0 || group>=32 || row==null || index>=row.Length ? fallback : unchecked((uint)row[index]);
        }
        internal static string Key(int[] row,int slot)
        {
            if(row==null || row.Length<272) return "";
            var values=new string[32]; for(int i=0;i<32;i++) values[i]=row[16+slot*32+i].ToString();
            return string.Join(",",values);
        }
    }
    // END WORN GROUPS
}
