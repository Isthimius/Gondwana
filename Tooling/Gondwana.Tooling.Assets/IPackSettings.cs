namespace Gondwana.Tooling.Assets;

/// <summary>UI-independent packing options.</summary>
internal interface IPackSettings
{
    /// <summary>Gets the input directory.</summary>
    string Source { get; }
    /// <summary>Gets the destination archive.</summary>
    string Output { get; }
    /// <summary>Gets the archive password, or null when none is supplied.</summary>
    string? Password { get; }
    /// <summary>Gets whether the output archive is to use encryption.</summary>
    bool Encrypt { get; }
    /// <summary>Gets whether valid dependencies outside the packed inventory may remain external references.</summary>
    bool AllowExternal { get; }
    /// <summary>Gets whether existing typed archive entries are loaded before incoming entries replace matching identities.</summary>
    bool Append { get; }
    /// <summary>Gets whether source enumeration includes subdirectories.</summary>
    bool Recurse { get; }
}
