using System.ComponentModel;
using Gondwana.Audio.GSND;
using Gondwana.Tooling.Audio.Editing;

namespace Gondwana.Tooling.Audio.WinForms;

/// <summary>Keeps the model's descriptors and captures old values before PropertyGrid setters run.</summary>
internal sealed class AudioResourceProperties(AudioDocument document, AudioResourceDefinition resource) : CustomTypeDescriptor
{
    public override PropertyDescriptorCollection GetProperties() => GetProperties(null);
    public override PropertyDescriptorCollection GetProperties(Attribute[]? attributes) => new(
        TypeDescriptor.GetProperties(resource, attributes).Cast<PropertyDescriptor>()
            .Select(property => new TrackedProperty(document, resource, property)).ToArray());
    public override object GetPropertyOwner(PropertyDescriptor? pd) => this;

    private sealed class TrackedProperty(AudioDocument document, AudioResourceDefinition resource, PropertyDescriptor inner)
        : PropertyDescriptor(inner)
    {
        public override Type ComponentType => typeof(AudioResourceProperties);
        public override Type PropertyType => inner.PropertyType;
        public override bool IsReadOnly => inner.IsReadOnly;
        public override object? GetValue(object? component) => inner.GetValue(resource);
        public override bool CanResetValue(object component) => !IsReadOnly && inner.CanResetValue(resource);
        public override bool ShouldSerializeValue(object component) => inner.ShouldSerializeValue(resource);
        public override void ResetValue(object component) => document.EditResource(resource, "Reset audio " + DisplayName, () => inner.ResetValue(resource));
        public override void SetValue(object? component, object? value)
        {
            if (IsReadOnly) return;
            document.EditResource(resource, "Change audio " + DisplayName, () => inner.SetValue(resource, value));
            OnValueChanged(component, EventArgs.Empty);
        }
    }
}
