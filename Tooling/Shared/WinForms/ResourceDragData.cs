namespace Gondwana.Tooling.WinForms;

/// <summary>Carries typed authoring data between WinForms controls on the editor UI thread.</summary>
/// <remarks>Targets match this managed object directly. External OLE data is never decoded as an authoring payload.</remarks>
public sealed class ResourceDragData<T>(T payload) : DataObject(typeof(T).FullName!, payload) where T : class
{
    /// <summary>Gets the original in-process payload.</summary>
    public T Payload { get; } = payload;
}
