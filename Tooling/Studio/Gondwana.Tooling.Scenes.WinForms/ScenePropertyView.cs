using System.ComponentModel;
using System.Drawing.Design;
using System.Windows.Forms.Design;
using Gondwana.Tooling.Editing;
using Gondwana.Tooling.Scenes.Editing;

namespace Gondwana.Tooling.Scenes.WinForms;

/// <summary>Intercepts model writes before PropertyGrid overwrites values, including modal collection edits.</summary>
internal sealed class ScenePropertyView(SceneDocument document, object model) : CustomTypeDescriptor
{
    public override object GetPropertyOwner(PropertyDescriptor? pd) => model;
    public override PropertyDescriptorCollection GetProperties() => GetProperties(null);
    public override PropertyDescriptorCollection GetProperties(Attribute[]? attributes) => new(
        TypeDescriptor.GetProperties(model, attributes).Cast<PropertyDescriptor>().Select(p => new Field(document, model, p)).ToArray());

    private sealed class Field(SceneDocument document, object model, PropertyDescriptor inner) : PropertyDescriptor(inner)
    {
        public override Type ComponentType => inner.ComponentType;
        public override Type PropertyType => inner.PropertyType;
        public override bool IsReadOnly => inner.IsReadOnly || Name == "Source";
        public override TypeConverter Converter => inner.Converter;
        public override object? GetValue(object? component) => inner.GetValue(model);
        public override void SetValue(object? component, object? value)
        {
            if (!IsReadOnly) document.Edit(model, "Change " + DisplayName, () => inner.SetValue(model, value));
        }
        public override bool CanResetValue(object component) => false;
        public override void ResetValue(object component) { }
        public override bool ShouldSerializeValue(object component) => inner.ShouldSerializeValue(model);
        public override object? GetEditor(Type editorBaseType) => inner.GetEditor(editorBaseType) is UITypeEditor editor && !IsReadOnly
            ? new TrackedEditor(document, model, inner, editor) : inner.GetEditor(editorBaseType);
    }

    private sealed class TrackedEditor(SceneDocument document, object model, PropertyDescriptor property, UITypeEditor inner) : UITypeEditor
    {
        public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext? context) => inner.GetEditStyle(context);
        public override bool IsDropDownResizable => inner.IsDropDownResizable;
        public override bool GetPaintValueSupported(ITypeDescriptorContext? context) => inner.GetPaintValueSupported(context);
        public override void PaintValue(PaintValueEventArgs e) => inner.PaintValue(e);
        public override object? EditValue(ITypeDescriptorContext? context, IServiceProvider provider, object? value)
        {
            using var transaction = document.History.BeginTransaction("Edit " + property.DisplayName);
            var snapshot = new ModelSnapshot(value);
            var service = new EditorServices(provider);
            try
            {
                var edited = inner.EditValue(context, service, value);
                if (service.Cancelled) { snapshot.Restore(); return value; }
                document.RecordCollectionEdit(snapshot, "Edit " + property.DisplayName);
                document.Edit(model, "Change " + property.DisplayName, () => property.SetValue(model, edited));
                transaction.Commit();
                return edited;
            }
            catch { snapshot.Restore(); throw; }
        }
    }

    private sealed class EditorServices(IServiceProvider provider) : IServiceProvider, IWindowsFormsEditorService
    {
        public bool Cancelled { get; private set; }
        private IWindowsFormsEditorService Inner => (IWindowsFormsEditorService)provider.GetService(typeof(IWindowsFormsEditorService))!;
        public object? GetService(Type serviceType) => serviceType == typeof(IWindowsFormsEditorService) ? this : provider.GetService(serviceType);
        public void CloseDropDown() => Inner.CloseDropDown();
        public void DropDownControl(Control? control) => Inner.DropDownControl(control);
        public DialogResult ShowDialog(Form dialog)
        {
            var result = Inner.ShowDialog(dialog);
            Cancelled = result != DialogResult.OK;
            return result;
        }
    }
}
