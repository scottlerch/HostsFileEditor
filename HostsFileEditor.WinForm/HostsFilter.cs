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
    /// The disabled filtered enabled setting.
    /// </summary>
    private bool _disabled;

    /// <summary>
    /// The comments filtered enabled setting.
    /// </summary>
    private bool _comments;

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
        get => _disabled;
        set
        {
            if (_disabled != value)
            {
                _disabled = value;

                if (_disabled)
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
        get => _comments;
        set
        {
            if (_comments != value)
            {
                _comments = value;

                if (_comments)
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
