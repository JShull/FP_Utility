namespace FuzzPhyte.Utility.Editor
{
    using System;
    using System.Linq;
    using System.Reflection;
    using UnityEditor;
    using UnityEngine;
    public class FPDebugHelp
    {
        /// <summary>
        /// All Non-Unity Assemblies
        /// </summary>
        [MenuItem("FuzzPhyte/Utility/Editor/Debug/List RuntimeInitializeOnLoad Methods")]
        public static void ListAll()
        {
            int total = 0;
            int genericHits = 0;

            var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a =>
                {
                    var name = a.GetName().Name;

                    // Skip obvious engine/BCL stuff to reduce noise
                    if (name == "mscorlib" || name == "netstandard")
                        return false;

                    if (name.StartsWith("System") || name.StartsWith("Microsoft"))
                        return false;

                    if (name.StartsWith("UnityEngine") || name.StartsWith("UnityEditor"))
                        return false;

                    // Everything else (Assembly-CSharp, packages, paid assets, your asmdefs, etc.)
                    return true;
                });

            Debug.Log("RuntimeInitGenericScanner: Scanning assemblies:\n" +
                      string.Join("\n", assemblies.Select(a => "- " + a.GetName().Name)));

            foreach (var asm in assemblies)
            {
                Type[] types;
                try
                {
                    types = asm.GetTypes();
                }
                catch (ReflectionTypeLoadException e)
                {
                    types = e.Types.Where(t => t != null).ToArray();
                }

                foreach (var type in types)
                {
                    if (type == null) continue;

                    var methods = type.GetMethods(
                        BindingFlags.Static |
                        BindingFlags.Public |
                        BindingFlags.NonPublic);

                    foreach (var method in methods)
                    {
                        if (!method.GetCustomAttributes(typeof(RuntimeInitializeOnLoadMethodAttribute), false).Any())
                            continue;

                        total++;

                        bool inGeneric = IsInGenericTypeChain(type);
                        if (inGeneric) genericHits++;

                        var script = FindScriptAsset(type);

                        string msg =
                            $"[RuntimeInitializeOnLoadMethod] method '{method.Name}' " +
                            $"in type '{type.FullName}' (Assembly: {asm.GetName().Name}) " +
                            $"-> InGenericTypeChain = {inGeneric}";

                        if (inGeneric)
                            Debug.LogError(msg, script);  // 🚨 likely the one causing your error
                        else
                            Debug.Log(msg, script);
                    }
                }
            }

            Debug.Log($"RuntimeInitGenericScanner: Found {total} methods with [RuntimeInitializeOnLoadMethod]. " +
                      $"{genericHits} of them are in/under generic types.");
        }

        private static bool IsInGenericTypeChain(Type type)
        {
            // Walk up the declaring-type chain to see if any are generic
            for (Type t = type; t != null; t = t.DeclaringType)
            {
                if (t.IsGenericType || t.IsGenericTypeDefinition)
                    return true;
            }
            return false;
        }

        private static MonoScript FindScriptAsset(Type type)
        {
            // For assets & packages, AssetDatabase still sees scripts in Packages/
            string typeName = type.Name;

            var guids = AssetDatabase.FindAssets($"{typeName} t:MonoScript");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var ms = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (ms == null) continue;

                if (ms.GetClass() == type || ms.name == typeName)
                    return ms;
            }

            return null; // Fallback: we still log type.FullName & assembly name
        }
    }
}
