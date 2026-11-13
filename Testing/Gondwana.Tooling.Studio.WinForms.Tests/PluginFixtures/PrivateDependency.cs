namespace StudioPluginFixtures;

public static class PrivateDependency
{
    public static string Version => typeof(PrivateDependency).Assembly.GetName().Version!.ToString();
}
