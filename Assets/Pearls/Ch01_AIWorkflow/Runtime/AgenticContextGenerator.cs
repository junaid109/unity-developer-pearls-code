using UnityEngine;
using System.Text;
using System.Linq;
using System.Reflection;

namespace Pearls.AI
{
    /// <summary>
    /// A senior-level tool that generates an "Environment DNA" for AI agents.
    /// It scans the project for interfaces and critical settings to 
    /// provide high-fidelity context to an LLM.
    /// </summary>
    public static class AgenticContextGenerator
    {
        public static string GenerateProjectContext()
        {
            var context = new StringBuilder();
            context.AppendLine("# UNITY PROJECT CONTEXT");
            context.AppendLine($"Unity Version: {Application.unityVersion}");
            context.AppendLine($"Platform: {Application.platform}");

            // 1. Scan for Public Interfaces (The "Contracts")
            context.AppendLine("\n## CORE CONTRACTS (INTERFACES)");
            // GetExecutingAssembly() would only see the assembly this class compiles into,
            // so interfaces living in other asmdefs would silently be missing. Scan every
            // loaded assembly and filter by namespace instead.
            var interfaces = System.AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(SafeGetTypes)
                .Where(t => t.IsInterface && t.Namespace != null && t.Namespace.StartsWith("Pearls"))
                .OrderBy(t => t.FullName);

            foreach (var i in interfaces)
            {
                context.AppendLine($"- {i.Name}: {GetSummary(i)}");
                foreach (var method in i.GetMethods())
                {
                    context.AppendLine($"  * {method.Name}({GetParams(method)}) -> {method.ReturnType.Name}");
                }
            }

            // 2. Capture Active Scene Context
            context.AppendLine("\n## ACTIVE SCENE HIERARCHY");
            var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (var obj in roots.Take(5)) // Limit depth to prevent context blowup
            {
                context.AppendLine($"- [GameObject] {obj.name} (Tags: {obj.tag})");
            }

            return context.ToString();
        }

        // Assembly.GetTypes() throws ReflectionTypeLoadException if any type fails to load;
        // keep the types that did load rather than losing the whole assembly.
        private static System.Collections.Generic.IEnumerable<System.Type> SafeGetTypes(Assembly a)
        {
            try { return a.GetTypes(); }
            catch (ReflectionTypeLoadException e) { return e.Types.Where(t => t != null); }
        }

        private static string GetSummary(System.Type t) => "Interface defining core system interaction.";
        private static string GetParams(MethodInfo m) => string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name));
    }
}