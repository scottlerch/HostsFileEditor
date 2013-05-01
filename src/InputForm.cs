// <copyright file="InputForm.cs" company="N/A">
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

namespace HostsFileEditor
{
    using System;
    using System.Windows.Forms;

    /// <summary>
    /// Reusable input dialog.
    /// </summary>
    public partial class InputForm : Form
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="InputForm"/> class.
        /// </summary>
        public InputForm()
        {
            InitializeComponent();

            this.buttonOk.Enabled = false;
            isMod = false; //assume it isn't a mod save request.
        }

        /// <summary>
        /// Gets or sets the mod bool
        /// </summary>
        public bool isMod
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the input.
        /// </summary>
        public string Input
        {
            get { return this.textBox.Text; }
            set { this.textBox.Text = value; }
        }

        /// <summary>
        /// Gets or sets the prompt.
        /// </summary>
        public string Prompt
        {
            get { return this.labelPrompt.Text; }
            set { this.labelPrompt.Text = value; }
        }

        /// <summary>
        /// Called when OK clicked.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
        private void OnButtonOkClick(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        /// <summary>
        /// Called when cancel clicked.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
        private void OnButtonCancelClick(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

        /// <summary>
        /// Called when text changed.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
        private void OnTextChanged(object sender, EventArgs e)
        {
            string error;
            if (isMod)
            {
                this.buttonOk.Enabled = HostsMod.Validate(this.Input, out error);
            }
            else
            {
                this.buttonOk.Enabled = HostsArchive.Validate(this.Input, out error);
            }
            this.errorProvider.SetError(this.textBox, error);
        }
    }
}
