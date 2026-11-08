using System.Collections.Generic;

namespace ArmorRestoredQuest
{
    internal static class NativeEnumerable
    {
        // Generated generic interfaces omit inherited IEnumerator.MoveNext.
        // Call it through the native non-generic interface, while reading the
        // strongly typed Current from the generic interface.
        internal static IEnumerable<T> Items<T>(Il2CppSystem.Collections.Generic.IEnumerable<T> source)
        {
            if (source == null) yield break;
            var current = source.GetEnumerator();
            var iterator = current.Cast<Il2CppSystem.Collections.IEnumerator>();
            try
            {
                while (iterator.MoveNext()) yield return current.Current;
            }
            finally
            {
                var disposable = current.TryCast<Il2CppSystem.IDisposable>();
                if (disposable != null) disposable.Dispose();
            }
        }
    }
}
