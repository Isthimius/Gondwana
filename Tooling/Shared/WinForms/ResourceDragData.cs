using Gondwana.Tooling.Assets;

namespace Gondwana.Tooling.WinForms;

/// <summary>Managed transport for a saved resource and any selection needed on release.</summary>
/// <remarks>Targets match this object directly. External OLE data is never decoded as an authoring payload.</remarks>
public abstract class ResourceDragData : DataObject
{
    private readonly (long Length, DateTime Modified)? _initialStamp;

    /// <summary>Gets the source address, which may omit the member until selection completes.</summary>
    public ResourceAddress Address { get; }

    private protected ResourceDragData(Type type, ResourceAddress address, object data) : base(type.FullName!, data)
    {
        Address = address;
        var info = new FileInfo(address.Location.Path);
        if (info.Exists)
            _initialStamp = (info.Length, info.LastWriteTimeUtc);
    }

    /// <summary>Begins validation using the source version captured when the drag started.</summary>
    public AuthoringResourceReadSnapshot BeginRead()
    {
        var reads = new AuthoringResourceReadSnapshot(Address.Location.Path, _initialStamp);
        reads.Verify();
        return reads;
    }

    /// <summary>Completes any missing logical selection using the drop's resolver. Null means the user canceled.</summary>
    public IAuthoringResourceDrag? Resolve(IWin32Window owner, AuthoringResourceResolver resolver)
    {
        var payload = SelectPayload(owner, resolver);
        if (payload is not null &&
            (payload.Address with { Member = Address.Member, FieldPath = null } != Address with { FieldPath = null } ||
             string.IsNullOrWhiteSpace(payload.Address.Member) ||
             Address.Member is { } member && !DefinitionReferenceQuery.MembersEqual(Address.Location.AssetType, member, payload.Address.Member)))
            throw new InvalidDataException("The chosen member does not belong to the dragged resource.");

        return payload;
    }

    private protected abstract IAuthoringResourceDrag? SelectPayload(IWin32Window owner, AuthoringResourceResolver resolver);
}

/// <summary>Carries one typed authoring resource between WinForms controls on the editor UI thread.</summary>
public sealed class ResourceDragData<T> : ResourceDragData where T : class, IAuthoringResourceDrag
{
    private readonly Func<IWin32Window, AuthoringResourceResolver, T?>? _select;

    /// <summary>Gets the original payload if its logical selection was already complete.</summary>
    public T? Payload { get; }

    /// <summary>Creates a drag from a complete editor selection.</summary>
    public ResourceDragData(T payload) : base(typeof(T), payload.Address, payload) => Payload = payload;

    /// <summary>Creates a drag that completes its selection only when released onto a compatible target.</summary>
    public ResourceDragData(ResourceAddress address, Func<IWin32Window, AuthoringResourceResolver, T?> select)
        : base(typeof(T), address, address) => _select = select;

    private protected override IAuthoringResourceDrag? SelectPayload(IWin32Window owner, AuthoringResourceResolver resolver) =>
        Payload ?? _select?.Invoke(owner, resolver);
}
