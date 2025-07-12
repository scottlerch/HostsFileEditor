// <copyright file="InputForm.cs" company="N/A">
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

namespace HostsFileEditor;

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

        buttonOk.Enabled = false;
    }

    /// <summary>
    /// Gets or sets the input.
    /// </summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
    public string Input
    {
        get => textBox.Text;
        set => textBox.Text = value;
    }

    /// <summary>
    /// Gets or sets the prompt.
    /// </summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
    public string Prompt
    {
        get => labelPrompt.Text;
        set => labelPrompt.Text = value;
    }

    /// <summary>
    /// Called when OK clicked.
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
    private void OnButtonOkClick(object sender, EventArgs e)
    {
        DialogResult = DialogResult.OK;
        Close();
    }

    /// <summary>
    /// Called when cancel clicked.
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
    private void OnButtonCancelClick(object sender, EventArgs e)
    {
        DialogResult = DialogResult.Cancel;
        Close();
    }

    /// <summary>
    /// Called when text changed.
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
    private void OnTextChanged(object sender, EventArgs e)
    {
        buttonOk.Enabled = HostsArchive.Validate(Input, out string error);
        errorProvider.SetError(textBox, error);
    }
}
