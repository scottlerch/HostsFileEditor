using Equin.ApplicationFramework;

namespace HostsFileEditor;

/// <summary>
/// The classic edition's Equin view filter. It drives the single canonical predicate
/// <see cref="HostsEntry.MatchesFilter"/> so the three filter rules (hide comment-only, hide
/// disabled-non-comment, text-contains) stay byte-for-byte identical to the modern edition (issue
/// #75) instead of being restated as separate <see cref="PredicateItemFilter{T}"/>s that could
/// silently drift. Toggling <see cref="Comments"/>/<see cref="Disabled"/> only flips a flag; the
/// caller re-runs the filter via <c>BindingListView.Refresh()</c> (as it already did), so no dynamic
/// add/remove of sub-filters is needed.
/// </summary>
internal sealed class HostsFilter : IItemFilter<HostsEntry>
{
    /// <summary>
    /// Supplies the current filter text on demand. Read per <see cref="Include"/> call (as the old
    /// closure read <c>textFilter.Text</c>) so a keystroke is reflected on the next Refresh without
    /// re-wiring; the caller is responsible for trimming/normalizing what it returns.
    /// </summary>
    private readonly Func<string> _filterText;

    /// <summary>
    /// Initializes a new instance of the <see cref="HostsFilter"/> class.
    /// </summary>
    /// <param name="filterText">Provider for the current (trimmed) filter text.</param>
    public HostsFilter(Func<string> filterText)
    {
        _filterText = filterText;
    }

    /// <summary>
    /// Gets or sets a value indicating whether comment-only lines are hidden.
    /// </summary>
    public bool Comments { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether disabled (non-comment) entries are hidden.
    /// </summary>
    public bool Disabled { get; set; }

    /// <inheritdoc />
    public bool Include(HostsEntry item) =>
        HostsEntry.MatchesFilter(item, hideComments: Comments, hideDisabled: Disabled, _filterText());
}
