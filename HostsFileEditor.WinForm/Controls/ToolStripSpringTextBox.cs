using System.Windows.Forms.Design;

namespace HostsFileEditor.Controls;

/// <summary>
/// Tool strip text box that springs to fill tool strip.
/// </summary>
/// <remarks>
/// Implementation from MSDN:
/// http://msdn.microsoft.com/en-us/library/ms404304.aspx
/// </remarks>
[ToolStripItemDesignerAvailability(
    ToolStripItemDesignerAvailability.ToolStrip |
    ToolStripItemDesignerAvailability.MenuStrip |
    ToolStripItemDesignerAvailability.ContextMenuStrip)]
[ToolboxBitmap(typeof(ToolStripTextBox))]
internal class ToolStripSpringTextBox : ToolStripTextBox
{
    /// <inheritdoc />
    public override Size GetPreferredSize(Size constrainingSize)
    {
        // Use the default size if the text box is on the overflow menu
        // or is on a vertical ToolStrip.
        if (IsOnOverflow || Owner?.Orientation == Orientation.Vertical)
        {
            return DefaultSize;
        }

        // Return default size if Owner is null
        if (Owner == null)
        {
            return DefaultSize;
        }

        // Declare a variable to store the total available width as 
        // it is calculated, starting with the display width of the 
        // owning ToolStrip.
        int width = Owner.DisplayRectangle.Width;

        // Subtract the width of the overflow button if it is displayed. 
        if (Owner.OverflowButton.Visible)
        {
            width = width - Owner.OverflowButton.Width -
                Owner.OverflowButton.Margin.Horizontal;
        }

        // Declare a variable to maintain a count of ToolStripSpringTextBox 
        // items currently displayed in the owning ToolStrip. 
        int springBoxCount = 0;

        foreach (ToolStripItem item in Owner.Items)
        {
            // Ignore items on the overflow menu.
            if (item.IsOnOverflow)
            {
                continue;
            }

            if (item is ToolStripSpringTextBox)
            {
                // For ToolStripSpringTextBox items, increment the count and 
                // subtract the margin width from the total available width.
                springBoxCount++;
                width -= item.Margin.Horizontal;
            }
            else
            {
                // For all other items, subtract the full width from the total
                // available width.
                width = width - item.Width - item.Margin.Horizontal;
            }
        }

        // If there are multiple ToolStripSpringTextBox items in the owning
        // ToolStrip, divide the total available width between them. 
        if (springBoxCount > 1)
        {
            width /= springBoxCount;
        }

        // If the available width is less than the default width, use the
        // default width, forcing one or more items onto the overflow menu.
        if (width < DefaultSize.Width)
        {
            width = DefaultSize.Width;
        }

        // Retrieve the preferred size from the base class, but change the
        // width to the calculated width. 
        Size size = base.GetPreferredSize(constrainingSize);
        size.Width = width;

        return size;
    }
}
