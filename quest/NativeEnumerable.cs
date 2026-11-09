using System;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace ArmorRestoredQuest
{
    internal static class NativeEnumerable
    {
        // Avoid generated value-type enumerator interface bridges altogether.
        // Native HashSet/List/ValueCollection implementations expose ICollection.
        // Complete the copy before any cosmetic packing/native callbacks occur.
        internal static T[] Items<T>(Il2CppSystem.Collections.Generic.IEnumerable<T> source)
            where T : Il2CppObjectBase
        {
            if (source == null) return new T[0];
            var collection = source.TryCast<Il2CppSystem.Collections.Generic.ICollection<T>>();
            if (collection == null) throw new InvalidOperationException("Native collection does not support snapshot CopyTo");
            var copy = new Il2CppReferenceArray<T>(collection.Count);
            collection.CopyTo(copy, 0);
            T[] result = new T[copy.Length];
            for (int i = 0; i < result.Length; i++) result[i] = copy[i];
            return result;
        }
    }
}
