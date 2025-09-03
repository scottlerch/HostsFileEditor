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
