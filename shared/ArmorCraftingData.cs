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
            // Leather 52830..37, Metal 52838..45. Explicitly exclude Wyrm and
            // all other custom/native prefabs regardless of external settings.
            return hash >= 52830U && hash <= 52845U;
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
            if (!stream.CanSeek || stream.Length > 4 * 1024 * 1024) throw new InvalidDataException("Invalid construction resource size");
            var result = new ArmorConstructionData();
            using (var reader = new BinaryReader(stream))
            {
                if (reader.ReadUInt32() != 0x46524341U || reader.ReadInt32() != 1)
                    throw new InvalidDataException("Unsupported construction resource");
                byte[] digest = reader.ReadBytes(32);
                if (digest.Length != 32) throw new EndOfStreamException();
                if (BitConverter.ToString(digest).Replace("-", "") != "119EDC0C48D29C7DBA66F5EA3CC7537E17601BF32184E4EDDEACE6716E897B4A")
                    throw new InvalidDataException("Construction model identity mismatch");
                int count = Count(reader, 1, 16);
                int totalVertices = 0;
                for (int i = 0; i < count; i++)
                {
                    var plan = new Plan { Id = Text(reader), Glyph = Text(reader) };
                    string[] parts = plan.Id.Split('.');
                    if (parts.Length != 2 || (parts[0] != "leather" && parts[0] != "metal") || !ValidSlot(parts[1]))
                        throw new InvalidDataException("Unapproved crafting result: " + plan.Id);
                    if (plan.Glyph.IndexOf('/') >= 0 || plan.Glyph.IndexOf('\\') >= 0 || !plan.Glyph.EndsWith(".png", StringComparison.Ordinal))
                        throw new InvalidDataException("Invalid glyph resource name");
                    int ingredients = Count(reader, 1, 32);
                    plan.Ingredients = new Ingredient[ingredients];
                    for (int j = 0; j < ingredients; j++)
                    {
                        var input = new Ingredient();
                        input.Source = reader.ReadUInt32();
                        uint primary = parts[0] == "metal" ? 50988U : 47760U;
                        uint closure = parts[0] == "metal" ? 62594U : 63204U;
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
                        if (totalVertices > 100000) throw new InvalidDataException("Construction vertex budget exceeded");
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
            uint primary = result < 52838U ? 47760U : 50988U;
            uint closure = result < 52838U ? 63204U : 62594U;
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
}
