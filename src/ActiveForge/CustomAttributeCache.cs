using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace ActiveForge
{
    /// <summary>
    /// Thread-safe cache for custom attribute lookups, avoiding repeated reflection calls.
    /// </summary>
    public static class CustomAttributeCache
    {
        private static readonly ConcurrentDictionary<string, object[]> _cache
            = new ConcurrentDictionary<string, object[]>(StringComparer.Ordinal);

        private static object[] GetOrAdd(string key, Func<object[]> factory)
            => _cache.GetOrAdd(key, _ => factory());

        // ── Class attributes ──────────────────────────────────────────────────
        public static object[] GetClassAttributes(Type type, Type attrType, bool inherit)
            => GetOrAdd(Key(type.FullName, attrType.FullName, inherit),
                        () => type.GetCustomAttributes(attrType, inherit));

        public static Attribute GetClassAttribute(Type type, Type attrType, bool inherit)
        {
            var arr = GetClassAttributes(type, attrType, inherit);
            return arr.Length > 0 ? (Attribute)arr[0] : null;
        }

        // ── Field attributes ──────────────────────────────────────────────────
        public static object[] GetFieldAttributes(FieldInfo field, Type attrType, bool inherit)
            => GetOrAdd(Key(field.DeclaringType?.FullName, field.Name, attrType.FullName, inherit),
                        () => field.GetCustomAttributes(attrType, inherit));

        public static Attribute GetFieldAttribute(FieldInfo field, Type attrType, bool inherit)
        {
            var arr = GetFieldAttributes(field, attrType, inherit);
            return arr.Length > 0 ? (Attribute)arr[0] : null;
        }

        // ── Method attributes ─────────────────────────────────────────────────
        public static object[] GetMethodAttributes(MethodInfo method, Type attrType, bool inherit)
            => GetOrAdd(Key(method.DeclaringType?.FullName, method.Name, attrType.FullName, inherit),
                        () => method.GetCustomAttributes(attrType, inherit));

        public static Attribute GetMethodAttribute(MethodInfo method, Type attrType, bool inherit)
        {
            var arr = GetMethodAttributes(method, attrType, inherit);
            return arr.Length > 0 ? (Attribute)arr[0] : null;
        }

        public static void Clear() => _cache.Clear();

        // ── Key builders ──────────────────────────────────────────────────────
        private static string Key(string s1, string s2, bool b)
            => $"{s1}|{s2}|{b}";
        private static string Key(string s1, string s2, string s3, bool b)
            => $"{s1}|{s2}|{s3}|{b}";
    }
}
