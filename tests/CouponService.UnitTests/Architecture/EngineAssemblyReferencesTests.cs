using System.Reflection;

namespace CouponService.UnitTests.Architecture;

public sealed class EngineAssemblyReferencesTests
{
    [Fact]
    public void Engine_transitive_assembly_references_exclude_asp_net_core_azure_and_cosmos()
    {
        var engineAssembly = Assembly.Load(new AssemblyName("CouponService.Engine"));
        var referencedNames = CollectTransitiveAssemblyNames(engineAssembly).ToArray();

        var forbidden = referencedNames
            .Where(IsForbiddenAssemblyName)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(forbidden);
    }

    private static IEnumerable<string> CollectTransitiveAssemblyNames(Assembly assembly)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pending = new Stack<Assembly>([assembly]);

        while (pending.Count > 0)
        {
            var current = pending.Pop();
            foreach (var reference in current.GetReferencedAssemblies())
            {
                if (!visited.Add(reference.Name!))
                {
                    continue;
                }

                yield return reference.Name!;

                var loaded = Assembly.Load(reference);
                pending.Push(loaded);
            }
        }
    }

    private static bool IsForbiddenAssemblyName(string assemblyName) =>
        assemblyName.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal)
        || assemblyName.StartsWith("Microsoft.Azure", StringComparison.Ordinal);
}
