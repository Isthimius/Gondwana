using System.Reflection;
using Gondwana.Mcp.Tools;
using ModelContextProtocol.Server;

namespace Gondwana.Tests.GondwanaMcp;

/// <summary>
/// Contains regression tests for mcp tool metadata.
/// </summary>
public sealed class McpToolMetadataTests
{
    /// <summary>
    /// Verifies every tool is explicitly read only non destructive idempotent and closed world.
    /// </summary>
    [Fact]
    public void EveryTool_IsExplicitlyReadOnlyNonDestructiveIdempotentAndClosedWorld()
    {
        Type[] toolTypes =
        [
            typeof(GondwanaRepositoryTools),
            typeof(GondwanaWikiTools)
        ];

        MethodInfo[] toolMethods = toolTypes
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Static))
            .Where(method => method.GetCustomAttribute<McpServerToolAttribute>() is not null)
            .ToArray();

        Assert.Equal(7, toolMethods.Length);

        foreach (MethodInfo method in toolMethods)
        {
            McpServerToolAttribute attribute =
                method.GetCustomAttribute<McpServerToolAttribute>()!;

            Assert.True(attribute.ReadOnly);
            Assert.False(attribute.Destructive);
            Assert.True(attribute.Idempotent);
            Assert.False(attribute.OpenWorld);
        }
    }
}
