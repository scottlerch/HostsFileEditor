using Equin.ApplicationFramework;

namespace HostsFileEditor;

/// <summary>
/// This class represents 
/// </summary>
internal sealed class HostsFilter : CompositeItemFilter<HostsEntry>
{
    /// <summary>
    /// The comment filter.
    /// </summary>
    private readonly PredicateItemFilter<HostsEntry> _commentFilter;

    /// <summary>
    /// The disabled filter.
    /// </summary>
    private readonly PredicateItemFilter<HostsEntry> _disabledFilter;

    /// <summary>
    /// The custom filter.
    /// </summary>
    private readonly PredicateItemFilter<HostsEntry> _customFilter;

    /// <summary>
    /// Initializes a new instance of the <see cref="HostsFilter"/> class.
    /// </summary>
    /// <param name="customFilter">The custom filter.</param>
    public HostsFilter(Predicate<HostsEntry> customFilter)
    {
        _commentFilter = new PredicateItemFilter<HostsEntry>(
            hostEntry => !hostEntry.HasCommentOnly);

        _disabledFilter = new PredicateItemFilter<HostsEntry>(
            hostEntry => hostEntry.Enabled || hostEntry.HasCommentOnly);

        _customFilter = new PredicateItemFilter<HostsEntry>(customFilter);

        AddFilter(_customFilter);
    }

    /// <summary>
    /// Gets or sets a value indicating whether disabled are filtered.
    /// </summary>
    public bool Disabled
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;

                if (field)
                {
                    AddFilter(_disabledFilter);
                }
                else
                {
                    RemoveFilter(_disabledFilter);
                }
            }
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether comments are filtered.
    /// </summary>
    public bool Comments
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;

                if (field)
                {
                    AddFilter(_commentFilter);
                }
                else
                {
                    RemoveFilter(_commentFilter);
                }
            }
        }
    }
}
