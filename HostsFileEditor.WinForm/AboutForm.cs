using System.Diagnostics;
using System.Reflection;

namespace HostsFileEditor;

/// <summary>
/// The about dialog.
/// </summary>
partial class AboutForm : Form
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AboutForm"/> class.
    /// </summary>
    public AboutForm()
    {
        InitializeComponent();

        Text = $"About {AssemblyTitle}";
        labelProductName.Text = AssemblyProduct;
        labelVersion.Text = $"Version {AssemblyVersion}";
        labelCopyright.Text = AssemblyCopyright;
        textBoxDescription.Text = AssemblyDescription;
    }

    /// <summary>
    /// Gets the assembly title.
    /// </summary>
    public static string AssemblyTitle
    {
        get
        {
            var attributes = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyTitleAttribute), false);

            if (attributes.Length > 0)
            {
                AssemblyTitleAttribute titleAttribute = (AssemblyTitleAttribute)attributes[0];
                if (titleAttribute.Title != "")
                {
                    return titleAttribute.Title;
                }
            }

            return Path.GetFileNameWithoutExtension(Assembly.GetExecutingAssembly().Location);
        }
    }

    /// <summary>
    /// Gets the assembly version.
    /// </summary>
    public static string AssemblyVersion => Assembly.GetExecutingAssembly()?.GetName()?.Version?.ToString() ?? "1.0.0.0";

    /// <summary>
    /// Gets the assembly description.
    /// </summary>
    public static string AssemblyDescription => ((AssemblyDescriptionAttribute)Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyDescriptionAttribute), false)[0]).Description;

    /// <summary>
    /// Gets the assembly product.
    /// </summary>
    public static string AssemblyProduct
    {
        get
        {
            var attributes = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyProductAttribute), false);

            if (attributes.Length == 0)
            {
                return "";
            }

            return ((AssemblyProductAttribute)attributes[0]).Product;
        }
    }

    /// <summary>
    /// Gets the assembly copyright.
    /// </summary>
    public static string AssemblyCopyright
    {
        get
        {
            var attributes = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyCopyrightAttribute), false);

            if (attributes.Length == 0)
            {
                return "";
            }

            return ((AssemblyCopyrightAttribute)attributes[0]).Copyright;
        }
    }

    /// <summary>
    /// Gets the assembly company.
    /// </summary>
    public static string AssemblyCompany
    {
        get
        {
            var attributes = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyCompanyAttribute), false);

            if (attributes.Length == 0)
            {
                return "";
            }

            return ((AssemblyCompanyAttribute)attributes[0]).Company;
        }
    }

    private void OnLinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
    {
        Process.Start(githubLink.Text);
    }
}
