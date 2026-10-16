namespace Gondwana.Tooling.Assets;

/// <summary>UI-independent packing options.</summary>
internal interface IPackSettings
{
    /// <summary>Gets the input directory.</summary>
    string Source { get; }
    /// <summary>Gets the destination archive.</summary>
    string Output { get; }
    /// <summary>Gets the archive password.</summary>
    string? Password { get; }
    /// <summary>Gets the encryption choice.</summary>
    bool Encrypt { get; }
    /// <summary>Gets the external dependency policy.</summary>
    bool AllowExternal { get; }
    /// <summary>Gets the append policy.</summary>
    bool Append { get; }
    /// <summary>Gets the directory recursion policy.</summary>
    bool Recurse { get; }
}
