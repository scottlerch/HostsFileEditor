// <copyright file="ToolStripBindableStatusLabel.cs" company="N/A">
// Copyright 2025 Scott M. Lerch
// 
// This file is part of HostsFileEditor.
// 
// HostsFileEditor is free software: you can redistribute it and/or modify it 
// under the terms of the GNU General Public License as published by the Free 
// Software Foundation, either version 2 of the License, or (at your option)
// any later version.
// 
// HostsFileEditor is distributed in the hope that it will be useful, but 
// WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
// or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for
// more details.
// 
// You should have received a copy of the GNU General Public   License along
// with HostsFileEditor. If not, see http://www.gnu.org/licenses/.
// </copyright>

using System.ComponentModel;
using System.Windows.Forms.Design;

namespace HostsFileEditor.Controls;

/// <summary>
/// Tool strip menu item that support simple data binding.
/// </summary>
[ToolStripItemDesignerAvailability(ToolStripItemDesignerAvailability.StatusStrip)]
[ToolboxBitmap(typeof(ToolStripStatusLabel))]
internal class ToolStripBindableStatusLabel : ToolStripStatusLabel, IBindableComponent
{
    /// <summary>
    /// Binding context.
    /// </summary>
    private BindingContext? bindingContext;

    /// <summary>
    /// Data bindings.
    /// </summary>
    private ControlBindingsCollection? dataBindings;

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
            if (bindingContext == null)
            {
                if (Owner?.BindingContext != null)
                {
                    bindingContext = Owner.BindingContext;
                }
                else if (Parent?.BindingContext != null)
                {
                    bindingContext = Parent.BindingContext;
                }
                else
                {
                    bindingContext = new BindingContext();
                }
            }

            return bindingContext;
        }

        set
        {
            bindingContext = value;
        }
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
            dataBindings ??= new ControlBindingsCollection(this);

            return dataBindings;
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            Events.Dispose();

            if (dataBindings != null)
            {
                dataBindings.Clear();
                dataBindings = null;
            }

            bindingContext = null;
        }
    }
}