using System.ComponentModel;
using System.Windows.Forms.Design;

namespace HostsFileEditor.Controls;

/// <summary>
/// Tool strip menu item that support simple data binding.
/// </summary>
[ToolStripItemDesignerAvailability(ToolStripItemDesignerAvailability.StatusStrip)]
[ToolboxBitmap(typeof(ToolStripStatusLabel))]
internal sealed class ToolStripBindableStatusLabel : ToolStripStatusLabel, IBindableComponent
{

    /// <summary>
    /// Data bindings.
    /// </summary>
    private ControlBindingsCollection? _dataBindings;

    /// <summary>
    /// Gets or sets the collection of currency managers for the <see cref="T:System.Windows.Forms.IBindableComponent"/>.
    /// </summary>
    /// <value></value>
    /// <returns>
    /// The collection of <see cref="T:System.Windows.Forms.BindingManagerBase"/> objects for this <see cref="T:System.Windows.Forms.IBindableComponent"/>.
    /// </returns>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public new BindingContext? BindingContext
    {
        get
        {
            field ??= Owner?.BindingContext != null
                    ? Owner.BindingContext
                    : Parent?.BindingContext != null ? Parent.BindingContext : new BindingContext();

            return field;
        }

        set;
    }

    /// <summary>
    /// Gets the collection of data-binding objects for this <see cref="T:System.Windows.Forms.IBindableComponent"/>.
    /// </summary>
    /// <value></value>
    /// <returns>
    /// The <see cref="T:System.Windows.Forms.ControlBindingsCollection"/> for this <see cref="T:System.Windows.Forms.IBindableComponent"/>.
    /// </returns>
    [Category("Data")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
    public new ControlBindingsCollection DataBindings
    {
        get
        {
            _dataBindings ??= new ControlBindingsCollection(this);

            return _dataBindings;
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            Events.Dispose();
            _dataBindings?.Clear();
            _dataBindings = null;

            BindingContext = null;
        }
    }
}
