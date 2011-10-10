// <copyright file="ToolStripBindableStatusLabel.cs" company="N/A">
// Copyright 2011 Scott M. Lerch
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

namespace HostsFileEditor.Controls
{
    using System.ComponentModel;
    using System.Drawing;
    using System.Windows.Forms;
    using System.Windows.Forms.Design;

    /// <summary>
    /// Tool strip menu item that support simple data binding.
    /// </summary>
    [ToolStripItemDesignerAvailability(ToolStripItemDesignerAvailability.StatusStrip)]
    [ToolboxBitmap(typeof(ToolStripStatusLabel))]
    internal class ToolStripBindableStatusLabel : ToolStripStatusLabel, IBindableComponent
    {
        #region Constants and Fields

        /// <summary>
        /// Binding context.
        /// </summary>
        private BindingContext bindingContext;

        /// <summary>
        /// Data bindings.
        /// </summary>
        private ControlBindingsCollection dataBindings;

        #endregion

        #region Public Properties

        /// <summary>
        /// Gets or sets the collection of currency managers for the <see cref="T:System.Windows.Forms.IBindableComponent"/>.
        /// </summary>
        /// <value></value>
        /// <returns>
        /// The collection of <see cref="T:System.Windows.Forms.BindingManagerBase"/> objects for this <see cref="T:System.Windows.Forms.IBindableComponent"/>.
        /// </returns>
        [Browsable(false)]
        public BindingContext BindingContext
        {
            get
            {
                if (this.bindingContext == null)
                {
                    if (this.Owner != null && this.Owner.BindingContext != null)
                    {
                        this.bindingContext = this.Owner.BindingContext;
                    }
                    else if (this.Parent != null && this.Parent.BindingContext != null)
                    {
                        this.bindingContext = this.Parent.BindingContext;
                    }
                    else
                    {
                        this.bindingContext = new BindingContext();
                    }
                }

                return this.bindingContext;
            }

            set
            {
                this.bindingContext = value;
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
        public ControlBindingsCollection DataBindings
        {
            get
            {
                if (this.dataBindings == null)
                {
                    this.dataBindings = new ControlBindingsCollection(this);
                }

                return this.dataBindings;
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Releases the unmanaged resources used by the 
        /// <see cref="T:System.Windows.Forms.ToolStripControlHost"/> and 
        /// optionally releases the managed resources.
        /// </summary>
        /// <param name="disposing">
        /// true to release both managed and unmanaged 
        /// resources; false to release only unmanaged resources.
        /// </param>
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
            {
                this.Events.Dispose();

                if (this.dataBindings != null)
                {
                    this.dataBindings.Clear();
                    this.dataBindings = null;
                }

                this.bindingContext = null;
            }
        }

        #endregion
    }
}