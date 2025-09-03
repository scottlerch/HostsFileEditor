using Equin.ApplicationFramework;

namespace HostsFileEditor;

/// <summary>
/// This class represents 
/// </summary>
internal class HostsFilter : CompositeItemFilter<HostsEntry>
{
    /// <summary>
    /// The comment filter.
    /// </summary>
    private readonly PredicateItemFilter<HostsEntry> commentFilter;

    /// <summary>
    /// The disabled filter.
    /// </summary>
    private readonly PredicateItemFilter<HostsEntry> disabledFilter;

    /// <summary>
    /// The custom filter.
    /// </summary>
    private readonly PredicateItemFilter<HostsEntry> customFilter;

    /// <summary>
    /// The disabled filtered enabled setting.
    /// </summary>
    private bool disabled;

    /// <summary>
    /// The comments filtered enabled setting.
    /// </summary>
    private bool comments;

    /// <summary>
    /// Initializes a new instance of the <see cref="HostsFilter"/> class.
    /// </summary>
    /// <param name="customFilter">The custom filter.</param>
    public HostsFilter(Predicate<HostsEntry> customFilter)
    {
        commentFilter = new PredicateItemFilter<HostsEntry>(
            hostEntry => !hostEntry.HasCommentOnly);

        disabledFilter = new PredicateItemFilter<HostsEntry>(
            hostEntry => hostEntry.Enabled || hostEntry.HasCommentOnly);

        this.customFilter = new PredicateItemFilter<HostsEntry>(customFilter);

        AddFilter(this.customFilter);
    }

    /// <summary>
    /// Gets or sets a value indicating whether disabled are filtered.
    /// </summary>
    public bool Disabled
    {
        get => disabled;
        set
        {
            if (disabled != value)
            {
                disabled = value;

                if (disabled)
                {
                    AddFilter(disabledFilter);
                }
                else
                {
                    RemoveFilter(disabledFilter);
                }
            }
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether comments are filtered.
    /// </summary>
    public bool Comments
    {
        get => comments;
        set
        {
            if (comments != value)
            {
                comments = value;

                if (comments)
                {
                    AddFilter(commentFilter);
                }
                else
                {
                    RemoveFilter(commentFilter);
                }
            }
        }
    }
}