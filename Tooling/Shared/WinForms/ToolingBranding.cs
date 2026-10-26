namespace Gondwana.Tooling.WinForms;

/// <summary>Provides Gondwana's embedded application icon to editor windows and dialogs.</summary>
internal static class ToolingBranding
{
    private const string ResourceName = "Gondwana.Tooling.BrandingIcon";

    /// <summary>Creates a form-owned icon without accessing loose runtime files.</summary>
    internal static Icon CreateIcon()
    {
        using var stream = typeof(ToolingBranding).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Missing embedded Gondwana icon resource: {ResourceName}.");
        return new Icon(stream);
    }
}
