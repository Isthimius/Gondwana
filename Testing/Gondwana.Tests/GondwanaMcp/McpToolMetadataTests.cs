using System.Reflection;
using Gondwana.Mcp.Tools;
using ModelContextProtocol.Server;

namespace Gondwana.Tests.GondwanaMcp;

public sealed class McpToolMetadataTests
{
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
