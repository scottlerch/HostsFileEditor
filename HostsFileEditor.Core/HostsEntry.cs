using HostsFileEditor.Properties;
using HostsFileEditor.Utilities;
using System.ComponentModel;
using System.Globalization;
using System.Linq.Expressions;
using System.Net;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;

namespace HostsFileEditor;

public partial class HostsEntry : INotifyPropertyChanged, IDataErrorInfo
{
    private const string Address = "ipaddress";
    private const string AfterComment = "aftercomment";
    private const string Blank = "blank";
    private const string Disabled = "disabled";
    private const string Hostname = "hostname";
    private const string LineComment = "comment";

    // Lazily allocated: a valid entry (the overwhelming majority in a real hosts file) has no
    // errors, so it never allocates this dictionary. Mutated only via SetError.
    private Dictionary<string, string>? _errors;

    private void SetError(string key, string? message)
    {
        if (string.IsNullOrEmpty(message))
        {
            _errors?.Remove(key);
        }
        else
        {
            (_errors ??= [])[key] = message;
        }
    }

    private string _comment;
    private bool _enabled;
    private string _hostnames;
    private string _ipAddress;
    private string _unparsedText;
    private bool _unparsedTextInvalid;
    private bool _valid;
    private bool _ipAddressValid;
    private bool _hostnamesValid;

    public HostsEntry()
    {
        _valid = false;
        _enabled = false;
        _comment = string.Empty;
        _hostnames = string.Empty;
        _ipAddress = string.Empty;
        _unparsedText = string.Empty;
    }

    public HostsEntry(string unparsedTextLine) : this()
    {
        _unparsedText = unparsedTextLine;
        ApplyStructuralParse(ParseStructuralSpan(unparsedTextLine));
    }

    // The line's structural shape, mirroring the three regex alternatives (plus "no match"). The
    // regex and span engines both produce this; ApplyStructuralParse then runs identical
    // validation/demotion so only the structural extraction can differ (guarded by a differential
    // test that asserts the two engines agree on a large corpus).
    private enum LineKind { Entry, Comment, Blank, NoMatch }

    private readonly record struct StructuralParse(LineKind Kind, bool Disabled, string Ip, string Hostname, string Comment);

    private void ApplyStructuralParse(in StructuralParse p)
    {
        if (p.Kind == LineKind.NoMatch)
        {
            // No pattern matched (e.g. an IP with a trailing port/path). Preserve the raw text as
            // a comment so it round-trips intact. _valid/_enabled already false, fields empty.
            _comment = _unparsedText.TrimStart(' ', '\t', '#');
            return;
        }

        switch (p.Kind)
        {
            case LineKind.Blank:
                _valid = false;
                _hostnamesValid = false;
                _ipAddressValid = false;
                _enabled = false;
                break;

            case LineKind.Comment:
                _comment = p.Comment;
                _enabled = false;
                _valid = false;
                _hostnamesValid = false;
                _ipAddressValid = false;
                break;

            default: // Entry
                _enabled = !p.Disabled;
                _hostnames = p.Hostname;
                _ipAddress = p.Ip;
                _comment = p.Comment;

                // Collapse runs of 2+ spaces between hostnames, but only pay for the regex when
                // there actually is a double space (the common case has none).
                if (_hostnames.Contains("  ", StringComparison.Ordinal))
                {
                    _hostnames = TwoSpaceMatchRegex().Replace(_hostnames, " ");
                }

                // The structural parse already gated entry-ness on a syntactically valid IP token
                // (issue #80), so the IP is known valid here — trust it instead of re-running
                // IPAddress.TryParse, which would double the per-entry IP-parse cost on the 400K-line
                // hot load path. The IpAddress setter (edit path) still validates via ValidateIpAddress.
                _ipAddressValid = true;
                if (AutoPingIPAddress)
                {
                    Ping();
                }

                ValidateHostnames();

                // Defensive: both structural engines now gate entry-ness on a valid IP token AND a
                // structurally valid hostname (issue #80), so an Entry reaching here is already valid
                // and this demotion is unreachable in practice. Kept as a belt-and-suspenders guard —
                // for a tool that rewrites the system hosts file, silently treating an invalid line as
                // a live entry would be the worst failure mode.
                if (!_valid)
                {
                    _enabled = false;
                    _hostnames = string.Empty;
                    _ipAddress = string.Empty;
                    _errors?.Clear();
                    _comment = _unparsedText.TrimStart(' ', '\t', '#');
                    _unparsedTextInvalid = false;
                }

                break;
        }

        if (_comment.Length > 0 && _comment[0] == ' ')
        {
            _comment = _comment[1..];
        }
    }

    // Reference engine (the original regex). Kept as the oracle the differential test compares the
    // span engine against, and as the fallback ParseStructuralSpan delegates to for inputs with
    // embedded CR/LF (which the two engines would otherwise disagree on). Effectively cold in
    // production — line-split loading never produces CR/LF — and a few KB in size, not worth
    // #if-gating, which would break `dotnet build -c Release` of the test project.
    private static StructuralParse ParseStructuralRegex(string line)
    {
        var match = ValidHostsRegex().Match(line);

        if (!match.Success)
        {
            return new StructuralParse(LineKind.NoMatch, false, string.Empty, string.Empty, string.Empty);
        }

        if (match.Groups[Blank].Success)
        {
            return new StructuralParse(LineKind.Blank, false, string.Empty, string.Empty, string.Empty);
        }

        if (match.Groups[LineComment].Success)
        {
            return new StructuralParse(LineKind.Comment, false, string.Empty, string.Empty, match.Groups[LineComment].Value);
        }

        // The entry alternative's ipaddress group is [^\s]+ (any non-space token). Gate entry-ness on
        // a syntactically valid IP (issue #80) so the two engines stay in agreement: a would-be entry
        // with an invalid IP demotes to a raw comment, matching the span engine's NoMatch path.
        if (!IsValidIpToken(match.Groups[Address].Value))
        {
            return new StructuralParse(LineKind.NoMatch, false, string.Empty, string.Empty, string.Empty);
        }

        return new StructuralParse(
            LineKind.Entry,
            match.Groups[Disabled].Success,
            match.Groups[Address].Value,
            match.Groups[Hostname].Value,
            match.Groups[AfterComment].Value);
    }

    // Allocation-light hand parser: no Match/Group objects; only the (unavoidable) field strings
    // that get stored are materialized. It intentionally does NOT validate hostname characters —
    // an entry requires a non-empty IP token and a non-empty hostname token; hostname-char
    // validity is deferred to ValidateHostnames (invalid hostnames demote to a comment, matching
    // the regex's alt1-fail -> comment behavior). See the differential test.
    private static StructuralParse ParseStructuralSpan(string line)
    {
        // Embedded CR/LF cannot occur on the normal load path (lines are already split), but the two
        // engines disagree on them: the regex alternatives' non-Multiline anchors and `.` stop at a
        // newline while this scanner would treat it as ordinary whitespace. Delegate the exotic case
        // to the regex oracle so both engines agree by construction, instead of hand-porting .NET's
        // `$`-before-trailing-newline subtleties.
        if (line.AsSpan().IndexOfAny('\r', '\n') >= 0)
        {
            return ParseStructuralRegex(line);
        }

        var s = line.AsSpan();
        var n = s.Length;

        var i = 0;
        while (i < n && char.IsWhiteSpace(s[i]))
        {
            i++;
        }

        if (i == n)
        {
            return new StructuralParse(LineKind.Blank, false, string.Empty, string.Empty, string.Empty);
        }

        // Try to parse an entry. Prefer treating a leading '#'-run as the "disabled" marker (the
        // regex's disabled group is greedy). If that yields no entry, retry treating the '#' as part
        // of the IP token — the regex backtracks to this, and it then demotes to a comment.
        //
        // Entry-ness is gated on a syntactically valid IP first token (issue #80): a line is an entry
        // iff it starts with a real IP AND has a structurally valid hostname. A line that is
        // structurally shaped like an entry but whose first token is not a valid IP (e.g.
        // "# just a note", "999.999.999.999 host") is not an entry — it is a comment. Those are
        // returned as NoMatch so ApplyStructuralParse preserves the raw text verbatim, which is
        // byte-identical to the old match-anything-then-demote path.
        if (s[i] == '#')
        {
            var contentStart = i;
            while (contentStart < n && s[contentStart] == '#') { contentStart++; }
            while (contentStart < n && char.IsWhiteSpace(s[contentStart])) { contentStart++; }

            if (TryParseEntry(s, contentStart, disabled: true, out var disabledEntry, out var disabledBadIp))
            {
                return disabledEntry;
            }

            // A '#'-led line whose content is a would-be entry with an invalid IP demotes to the raw
            // comment; there is no point retrying the non-disabled read (its IP token starts with '#',
            // which can never be a valid IP).
            if (disabledBadIp)
            {
                return new StructuralParse(LineKind.NoMatch, false, string.Empty, string.Empty, string.Empty);
            }
        }

        if (TryParseEntry(s, i, disabled: false, out var entry, out var badIp))
        {
            return entry;
        }

        // Structurally an entry but the first token is not a valid IP -> demote to a raw comment.
        if (badIp)
        {
            return new StructuralParse(LineKind.NoMatch, false, string.Empty, string.Empty, string.Empty);
        }

        // --- Comment: [ws] #+ [one ws] COMMENT ---
        if (s[i] == '#')
        {
            var c = i;
            while (c < n && s[c] == '#') { c++; }
            if (c < n && char.IsWhiteSpace(s[c])) { c++; }
            return new StructuralParse(LineKind.Comment, false, string.Empty, string.Empty, s[c..n].ToString());
        }

        // Blank handled above; anything else is unmatched.
        return new StructuralParse(LineKind.NoMatch, false, string.Empty, string.Empty, string.Empty);

        // Parses "IP  ws+  HOSTNAMES [ws]  (#+ AFTERCOMMENT)?" from 'start'. IP is the first
        // non-whitespace run; the hostname must be structurally valid AND the IP token must be a
        // syntactically valid IP (issue #80) — otherwise it is not an entry. Allocates only the field
        // strings, and only on success. 'ipInvalidButStructural' distinguishes "shaped like an entry
        // but the first token is not a valid IP" (caller demotes to a raw comment) from "not
        // structurally an entry at all" (caller falls through to the comment alternative).
        static bool TryParseEntry(ReadOnlySpan<char> s, int start, bool disabled, out StructuralParse result, out bool ipInvalidButStructural)
        {
            ipInvalidButStructural = false;
            var n = s.Length;

            var ipStart = start;
            var p = start;
            while (p < n && !char.IsWhiteSpace(s[p])) { p++; }
            var ipEnd = p;

            var wsStart = p;
            while (p < n && char.IsWhiteSpace(s[p])) { p++; }

            if (ipEnd > ipStart && p > wsStart && p < n)
            {
                var hostStart = p;
                var hashIndex = -1;
                for (var q = p; q < n; q++)
                {
                    if (s[q] == '#') { hashIndex = q; break; }
                }

                var hostEnd = hashIndex >= 0 ? hashIndex : n;
                while (hostEnd > hostStart && char.IsWhiteSpace(s[hostEnd - 1])) { hostEnd--; }

                if (hostEnd > hostStart && HostNameRegex().IsMatch(s[hostStart..hostEnd]))
                {
                    if (!IsValidIpToken(s[ipStart..ipEnd]))
                    {
                        // Structurally an entry, but the first token is not a valid IP -> not an entry.
                        ipInvalidButStructural = true;
                        result = default;
                        return false;
                    }

                    var afterComment = string.Empty;
                    if (hashIndex >= 0)
                    {
                        var c = hashIndex;
                        while (c < n && s[c] == '#') { c++; }
                        afterComment = s[c..n].ToString();
                    }

                    result = new StructuralParse(
                        LineKind.Entry,
                        disabled,
                        s[ipStart..ipEnd].ToString(),
                        s[hostStart..hostEnd].ToString(),
                        afterComment);
                    return true;
                }
            }

            result = default;
            return false;
        }
    }

    // Test hooks: build an entry via a specific engine so a differential test can assert the span
    // engine agrees with the regex oracle.
    internal static HostsEntry ParseViaRegexForTest(string line)
    {
        var e = new HostsEntry { _unparsedText = line };
        e.ApplyStructuralParse(ParseStructuralRegex(line));
        return e;
    }

    internal static HostsEntry ParseViaSpanForTest(string line)
    {
        var e = new HostsEntry { _unparsedText = line };
        e.ApplyStructuralParse(ParseStructuralSpan(line));
        return e;
    }

    public HostsEntry(HostsEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        _comment = entry._comment;
        _enabled = entry._enabled;
        _hostnames = entry._hostnames;
        _ipAddress = entry._ipAddress;
        _unparsedText = entry._unparsedText;
        _unparsedTextInvalid = entry._unparsedTextInvalid;
        _valid = entry._valid;
        _hostnamesValid = entry._hostnamesValid;
        _ipAddressValid = entry._ipAddressValid;
        _errors = entry._errors is null ? null : new Dictionary<string, string>(entry._errors);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public static bool AutoPingIPAddress { get; set; }

    /// <summary>
    /// UI-thread synchronization context used to marshal ping-failure notifications when the ping
    /// was started off the UI thread. Both apps parse the hosts file on a background thread
    /// (<c>HostsFile.PreloadAsync</c>), so auto-pings started during that parse capture a null
    /// <see cref="SynchronizationContext.Current"/> — without this fallback their PropertyChanged
    /// would fire on the thread pool into bound UI (a cross-thread violation in both WinForms and
    /// WinUI). Each app assigns this once at startup on its UI thread.
    /// </summary>
    public static SynchronizationContext? UiSynchronizationContext { get; set; }

    // Number of pings currently in flight across all entries. Pings are fire-and-forget and can be
    // started en masse (auto-ping fires one per valid entry during a load), so this is maintained with
    // Interlocked and the change event is raised only when the count crosses the 0 boundary — a ping
    // storm produces exactly one "started" and one "stopped" notification, not one per ping.
    private static int _pendingPingCount;

    /// <summary>
    /// True while at least one ping is in flight. Bound by both editions to show a busy indicator in
    /// the status bar (issue #9).
    /// </summary>
    public static bool IsPingInProgress => Volatile.Read(ref _pendingPingCount) > 0;

    /// <summary>
    /// Raised when ping activity starts (first ping in flight) and stops (last ping completed), so the
    /// UI can show/hide its progress indicator. Marshalled to <see cref="UiSynchronizationContext"/>
    /// when set (pings complete on the thread pool), so handlers run on the UI thread.
    /// </summary>
    public static event EventHandler? PingActivityChanged;

    private bool _pingFailed;

    /// <summary>
    /// True when this entry's most recent ping did not succeed (issue #9). Bindable so an edition can
    /// show a per-entry "ping failed" indicator; cleared when a later ping succeeds or the IP is edited.
    /// The classic edition instead surfaces the failure via <see cref="IDataErrorInfo"/> on the IP cell.
    /// </summary>
    public bool PingFailed => _pingFailed;

    // Change-gated so a load-time ping storm (mostly successes on already-not-failed entries) raises no
    // notifications, and so re-validating every entry on parse costs nothing.
    private void SetPingFailed(bool value)
    {
        if (_pingFailed == value)
        {
            return;
        }

        _pingFailed = value;
        OnPropertyChanged(nameof(PingFailed));
    }

    private static int _pingReportingSuspended;

    /// <summary>
    /// Suspends marshalling ping results onto the UI thread for the lifetime of the returned scope
    /// (issue #103). A ping completion posts <see cref="OnPropertyChanged"/> onto the UI context,
    /// which drives the bound <c>BindingList</c>'s child-change indexing. While a bulk mutation runs
    /// the entry list off the UI thread (the modern app's async Import/Merge/Refresh clears and
    /// refills the list on a thread-pool thread), that indexing races the mutation and can crash the
    /// UI. Callers wrap the background mutation in this scope; reports that would fire during it are
    /// dropped (a ping result is transient and the list is rebuilt and re-evaluated afterwards).
    /// </summary>
    public static IDisposable SuspendPingReporting()
    {
        Interlocked.Increment(ref _pingReportingSuspended);
        return new PingReportingSuspension();
    }

    private static bool PingReportingSuspended => Volatile.Read(ref _pingReportingSuspended) > 0;

    private sealed class PingReportingSuspension : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Interlocked.Decrement(ref _pingReportingSuspended);
        }
    }

    // internal (not private) so the 0-boundary event semantics can be unit-tested without a live
    // network ping.
    internal static void BeginPing()
    {
        if (Interlocked.Increment(ref _pendingPingCount) == 1)
        {
            RaisePingActivityChanged();
        }
    }

    internal static void EndPing()
    {
        if (Interlocked.Decrement(ref _pendingPingCount) == 0)
        {
            RaisePingActivityChanged();
        }
    }

    private static void RaisePingActivityChanged()
    {
        var handler = PingActivityChanged;
        if (handler is null)
        {
            return;
        }

        // SendPingAsync completes on the thread pool, so marshal onto the UI context (as ping-failure
        // reporting does) rather than raising into bound UI from a background thread.
        var context = UiSynchronizationContext;
        if (context is not null)
        {
            context.Post(_ => handler(null, EventArgs.Empty), null);
        }
        else
        {
            handler(null, EventArgs.Empty);
        }
    }

    public string Error => _errors is null ? string.Empty : string.Join(Environment.NewLine, _errors.Values);

    public string Comment
    {
        get => _comment;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            if (string.Equals(_comment, value, StringComparison.Ordinal))
            {
                return;
            }

            var local = _comment;
            UndoManager.Instance.AddActions(
                undoAction: () => Comment = local,
                redoAction: () => Comment = value);

            Update(ref _comment, value, nameof(Comment));
        }
    }

    public bool HasCommentOnly => !Valid && !Enabled;

    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (_enabled == value)
            {
                return;
            }

            var local = _enabled;
            UndoManager.Instance.AddActions(
                undoAction: () => Enabled = local,
                redoAction: () => Enabled = value);

            Update(ref _enabled, value, nameof(Enabled));
        }
    }

    // Toggles Enabled WITHOUT raising PropertyChanged or registering undo. Used by
    // HostsEntryList.SetEnabled for a batch enable/disable: setting Enabled the normal way fires one
    // PropertyChanged per row, which the bound Equin BindingListView reacts to per item (O(n^2); hung
    // ~2 min at 400K). The batch raises a single ListChanged(Reset) instead so bound views refresh
    // once. Still invalidates the serialized line so a later save reflects the new state.
    internal void SetEnabledSilently(bool value)
    {
        if (_enabled == value)
        {
            return;
        }

        _enabled = value;
        _unparsedTextInvalid = true;
    }

    public string HostNames
    {
        get => _hostnames;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            var trimmed = value.Trim();
            if (string.Equals(_hostnames, trimmed, StringComparison.Ordinal))
            {
                return;
            }

            var local = _hostnames;
            UndoManager.Instance.AddActions(
                undoAction: () => HostNames = local,
                redoAction: () => HostNames = trimmed);

            Update(ref _hostnames, trimmed, nameof(HostNames), ValidateHostnames);
        }
    }

    public string IpAddress
    {
        get => _ipAddress;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            var trimmed = value.Trim();
            if (string.Equals(_ipAddress, trimmed, StringComparison.Ordinal))
            {
                return;
            }

            var local = _ipAddress;
            UndoManager.Instance.AddActions(
                undoAction: () => IpAddress = local,
                redoAction: () => IpAddress = trimmed);

            Update(ref _ipAddress, trimmed, nameof(IpAddress), ValidateIpAddress);
        }
    }

    public string UnparsedText
    {
        get
        {
            if (_unparsedTextInvalid)
            {
                _unparsedText = HasCommentOnly
                    ? $"# {_comment}"
                    : $"{(!Enabled ? "# " : string.Empty)}{IpAddress} {HostNames}{(Comment.Trim().Length != 0 ? " # " + Comment : string.Empty)}";

                if (!_valid)
                {
                    _unparsedText = "#" + _unparsedText;
                }

                _unparsedTextInvalid = false;
            }

            return _unparsedText;
        }

        set
        {
            ArgumentNullException.ThrowIfNull(value);
            Update(ref _unparsedText, value, nameof(UnparsedText));
        }
    }

    public bool Valid
    {
        get => _valid;
        set => Update(ref _valid, value, nameof(Valid));
    }

    public string this[string propertyName] => _errors is not null && _errors.TryGetValue(propertyName, out var value) ? value : string.Empty;

    public override string ToString() => $"{IpAddress} {HostNames} {Comment}";

    /// <summary>
    /// The sortable columns of a hosts entry, for the shared display-sort comparer (issue #81).
    /// </summary>
    public enum SortColumn
    {
        IpAddress,
        HostNames,
        Comment,
        Enabled,
        Valid,
    }

    /// <summary>
    /// Builds a comparer over <see cref="HostsEntry"/> for a given column and direction (issue #81) —
    /// the single Core definition both editions share, so the sort <em>key</em> is identical across
    /// editions (cf. the classic edition's typed <c>ComparerFor</c>). Note this fixes the ordering key,
    /// not tie-break stability: the modern edition sorts with a stable <c>OrderBy</c>, while the classic
    /// grid's underlying Equin sort is unstable, so rows with an equal key can land in a different order
    /// between the two. The IP column sorts <em>numerically</em> by address value — IPv4 before IPv6,
    /// and rows with no parseable IP last, in <em>both</em> directions (only the address value within a
    /// family reverses for descending) — see <see cref="GetIpSortKey"/>. The other string columns use
    /// culture-sensitive comparison, matching the framework's original property sort. The comparer is
    /// allocation-free per comparison (no boxing) and does not mutate entry order.
    /// </summary>
    public static IComparer<HostsEntry> GetComparer(SortColumn column, bool descending)
    {
        // IP sorts by numeric address value, not lexically — "10.0.0.2" < "10.0.0.10" and "8.8.8.8" <
        // "172.16.0.4", which a string sort gets backwards (issue #81). The per-entry key is computed
        // once and cached (GetIpSortKey), so a 400K-row sort parses each IP once. The Rank (family +
        // the no-IP bucket) is compared direction-INDEPENDENTLY so IPv4 stays before IPv6 and no-IP
        // rows stay last whether ascending or descending; only the address value within a rank flips.
        if (column == SortColumn.IpAddress)
        {
            return Comparer<HostsEntry>.Create((x, y) =>
            {
                var kx = x.GetIpSortKey();
                var ky = y.GetIpSortKey();
                if (kx.Rank != ky.Rank)
                {
                    return kx.Rank.CompareTo(ky.Rank);
                }

                var c = kx.Hi.CompareTo(ky.Hi);
                if (c == 0)
                {
                    c = kx.Lo.CompareTo(ky.Lo);
                }

                return descending ? -c : c;
            });
        }

        // Culture-sensitive string comparison is intentional (CA1309 suppressed): it matches the
        // classic edition's typed ComparerFor and the framework's original property sort, which routed
        // through string.CompareTo.
#pragma warning disable CA1309
        Comparer<HostsEntry> comparer = column switch
        {
            SortColumn.HostNames => Comparer<HostsEntry>.Create(static (x, y) => string.Compare(x.HostNames, y.HostNames, StringComparison.CurrentCulture)),
            SortColumn.Comment => Comparer<HostsEntry>.Create(static (x, y) => string.Compare(x.Comment, y.Comment, StringComparison.CurrentCulture)),
            SortColumn.Enabled => Comparer<HostsEntry>.Create(static (x, y) => x.Enabled.CompareTo(y.Enabled)),
            SortColumn.Valid => Comparer<HostsEntry>.Create(static (x, y) => x.Valid.CompareTo(y.Valid)),
            _ => throw new ArgumentOutOfRangeException(nameof(column)),
        };
#pragma warning restore CA1309

        return descending
            ? Comparer<HostsEntry>.Create((x, y) => comparer.Compare(y, x))
            : comparer;
    }

    // A numeric, allocation-free sort key for the IP column (issue #81). Ordering: Rank groups the
    // families (0 = IPv4, 1 = IPv6, 2 = no parseable IP → sorts last), then Hi/Lo hold the address
    // bytes big-endian so a plain unsigned compare == numeric address order. IPv4 packs its 4 bytes
    // into the top of Hi (Lo stays 0); IPv6 packs all 16 bytes across Hi and Lo.
    internal readonly record struct IpSortKey(byte Rank, ulong Hi, ulong Lo) : IComparable<IpSortKey>
    {
        public int CompareTo(IpSortKey other)
        {
            var c = Rank.CompareTo(other.Rank);
            if (c != 0)
            {
                return c;
            }

            c = Hi.CompareTo(other.Hi);
            return c != 0 ? c : Lo.CompareTo(other.Lo);
        }
    }

    // Cached IP sort key. Lazily computed from _ipAddress and invalidated whenever the IP changes
    // (see ValidateIpAddress), so a 400K-row IP sort parses each address once (O(n)) instead of on
    // every comparison (O(n log n)).
    private IpSortKey? _ipSortKey;

    internal IpSortKey GetIpSortKey() => _ipSortKey ??= ComputeIpSortKey(_ipAddress);

    private static IpSortKey ComputeIpSortKey(string ip)
    {
        if (!IPAddress.TryParse(ip, out var addr))
        {
            // Comment-only / invalid-IP line: no address to order by, so sort after all real IPs.
            return new IpSortKey(2, 0, 0);
        }

        var bytes = addr.GetAddressBytes();
        if (bytes.Length == 4)
        {
            var hi = ((ulong)bytes[0] << 56) | ((ulong)bytes[1] << 48) | ((ulong)bytes[2] << 40) | ((ulong)bytes[3] << 32);
            return new IpSortKey(0, hi, 0);
        }

        // IPv6 (16 bytes): first 8 → Hi, last 8 → Lo, big-endian.
        ulong high = 0, low = 0;
        for (var i = 0; i < 8; i++)
        {
            high = (high << 8) | bytes[i];
        }

        for (var i = 8; i < 16; i++)
        {
            low = (low << 8) | bytes[i];
        }

        return new IpSortKey(1, high, low);
    }

    /// <summary>
    /// The canonical hosts-entry filter predicate shared by both editions (issue #75). Returns
    /// <see langword="true"/> when <paramref name="entry"/> should remain visible under the three
    /// filter rules, applied in this order:
    /// <list type="number">
    ///   <item>hide comment-only lines when <paramref name="hideComments"/> is set;</item>
    ///   <item>hide disabled (non-comment) entries when <paramref name="hideDisabled"/> is set;</item>
    ///   <item>keep only rows whose text contains <paramref name="filterText"/>.</item>
    /// </list>
    /// The text match is case-insensitive (<see cref="StringComparison.OrdinalIgnoreCase"/>) over
    /// <see cref="ToString"/>. <paramref name="filterText"/> is matched as-is — callers hoist any
    /// trimming/normalization out of the per-row loop (an O(n) filter pass over a 400K-entry file
    /// must not re-trim per row), and an empty or <see langword="null"/> value matches everything.
    /// </summary>
    public static bool MatchesFilter(HostsEntry entry, bool hideComments, bool hideDisabled, string? filterText)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (hideComments && entry.HasCommentOnly)
        {
            return false;
        }

        if (hideDisabled && !entry.Enabled && !entry.HasCommentOnly)
        {
            return false;
        }

        return string.IsNullOrEmpty(filterText)
            || entry.ToString().Contains(filterText, StringComparison.OrdinalIgnoreCase);
    }

    public void Ping()
    {
        // Only ping addresses that parse. The Ping is created on demand and disposed
        // deterministically when the async operation completes (see PingAsync), so
        // HostsEntry instances do not need to be IDisposable or hold native resources.
        if (!IPAddress.TryParse(_ipAddress, out _))
        {
            return;
        }

        // Capture the current synchronization context (if any) so the result is
        // marshaled back to the originating (UI) thread.
        _ = PingAsync(_ipAddress, SynchronizationContext.Current);
    }

    private async Task PingAsync(string address, SynchronizationContext? syncContext)
    {
        IPStatus status;

        BeginPing();
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(address);
            status = reply.Status;
        }
        catch (Exception)
        {
            // A failed/cancelled/invalid ping must not crash the fire-and-forget caller.
            return;
        }
        finally
        {
            EndPing();
        }

        // Nothing to report on a success unless a PRIOR ping had failed (recovered) — this keeps a
        // load-time storm of successful pings from marshalling 400K no-op updates onto the UI thread.
        if (status == IPStatus.Success && !_pingFailed)
        {
            return;
        }

        void Report()
        {
            if (status == IPStatus.Success)
            {
                // Recovered: clear the ping-failure state. Only a ping failure could have set an IP
                // error on a valid IP (an invalid IP never pings), so clearing it here is safe.
                SetError(nameof(IpAddress), null);
                SetPingFailed(false);
            }
            else
            {
                SetError(nameof(IpAddress), string.Format(CultureInfo.CurrentCulture, Resources.PingFailed, status));
                SetPingFailed(true);
            }

            OnPropertyChanged(nameof(IpAddress));
        }

        // Prefer the context captured at Ping() time; fall back to the app-registered UI context
        // (pings started during the background parse capture null). Only report inline when neither
        // exists (headless/tests) — never onto bound UI from the thread pool. The suspension check
        // runs where Report would execute (on the UI thread for the posted path) so a bulk list
        // mutation in progress isn't raced by this report — see SuspendPingReporting (issue #103).
        var context = syncContext ?? UiSynchronizationContext;
        if (context is not null)
        {
            context.Post(_ => { if (!PingReportingSuspended) { Report(); } }, null);
        }
        else if (!PingReportingSuspended)
        {
            Report();
        }
    }

    private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private void Update<T>(ref T backing, T value, string property) => Update(ref backing, value, property, delegate { });

    private void Update<T>(ref T backing, T value, string property, Action performValidation)
    {
        if (!Equals(backing, value))
        {
            backing = value;

            _unparsedTextInvalid = true;

            var prevValid = _valid;
            performValidation();

            OnPropertyChanged(property);

            if (prevValid != _valid)
            {
                OnPropertyChanged(nameof(Valid));
                OnPropertyChanged(nameof(IpAddress));
                OnPropertyChanged(nameof(HostNames));
                OnPropertyChanged(nameof(Enabled));
            }

            OnPropertyChanged(nameof(Self));
        }
    }

    public HostsEntry Self => this;

    private void ValidateHostnames()
    {
        if (string.IsNullOrWhiteSpace(_hostnames) && !_enabled)
        {
            SetError(nameof(HostNames), null);
            _hostnamesValid = true;
        }
        else
        {
            _hostnamesValid = HostNameRegex().IsMatch(_hostnames);

            SetError(nameof(HostNames), !_hostnamesValid ? Resources.InvalidHostnames : null);
        }

        _valid = _ipAddressValid && _hostnamesValid;
    }

    private void ValidateIpAddress()
    {
        // The IP is (re)validated because it was set/changed, so the cached numeric sort key (issue
        // #81) is stale — drop it so the next IP sort recomputes from the new address.
        _ipSortKey = null;

        // The IP is (re)validated because it was set/changed, so any prior ping result is stale —
        // clear the failure flag (change-gated, so it's free on the parse path where it's already
        // false). If auto-ping is on, the ping below re-establishes it from the fresh result.
        SetPingFailed(false);

        if (string.IsNullOrWhiteSpace(_ipAddress) && !_enabled)
        {
            // A disabled entry with a blank IP is being edited, not an error. Mirror
            // ValidateHostnames (which sets _hostnamesValid = true here): set the flag the
            // final _valid recompute reads, instead of writing _valid directly — that write
            // was dead code, immediately overwritten using the stale _ipAddressValid below.
            SetError(nameof(IpAddress), null);
            _ipAddressValid = true;
        }
        else
        {
            _ipAddressValid = IsValidIpToken(IpAddress);

            if (!_ipAddressValid)
            {
                SetError(nameof(IpAddress), Resources.InvalidIPAddress);
            }
            else
            {
                SetError(nameof(IpAddress), null);

                if (AutoPingIPAddress)
                {
                    Ping();
                }
            }
        }

        _valid = _ipAddressValid && _hostnamesValid;
    }

    // Single source of truth for "is this token a syntactically valid IP address." Gates entry-ness
    // at parse time (issue #80) in both the span and regex engines, and backs ValidateIpAddress so an
    // edited address is flagged by the exact same rule. Uses IPAddress.TryParse, so IPv4 and every
    // IPv6 form the framework accepts (::1, zone IDs, etc.) are accepted identically. The
    // ReadOnlySpan<char> overload lets the structural parser validate the IP token without allocating
    // it (string converts implicitly, so the edit path can pass IpAddress directly).
    private static bool IsValidIpToken(ReadOnlySpan<char> token) => IPAddress.TryParse(token, out _);

    [GeneratedRegex(@"[ ]{2,}")]
    private static partial Regex TwoSpaceMatchRegex();

    [GeneratedRegex(@"^([\w-]+\.)*([\w-]+)\.?((\s)+([\w-]+\.)*([\w-]+)\.?)*$")]
    private static partial Regex HostNameRegex();

    // Each alternative is anchored with ^...$ so a line must match a pattern in full. Without
    // the per-alternative anchors, ^ bound only to the entry alternative and $ only to the
    // blank one, so trailing junk (e.g. a port after the hostname) was silently dropped and
    // an unmatched remainder still "parsed". Lines matching nothing are preserved as comments
    // by the constructor.
    //
    // The entry alternative leads with \s* so a line with leading whitespace before the
    // optional '#' still parses as an entry (issue #22). Without it, \s* consumed the leading
    // space and '#' was captured as the ipaddress, demoting a valid disabled entry (e.g.
    // " # 1.2.3.4 host") to a plain comment.
    // No RegexOptions.Compiled: the source generator already emits the fully compiled matcher, so
    // Compiled would only add redundant runtime IL compilation at startup.
    //
    // Each hostname token allows one optional trailing '.' so fully-qualified names (e.g. the
    // Tailscale MagicDNS "host.tailnet.ts.net.") parse as entries rather than falling through
    // to the comment alternative.
    [GeneratedRegex(@"^(\s*(?<disabled>#+)?\s*(?<ipaddress>[^\s]+)\s+(?<hostname>([\w-]+\.)*([\w-]+)\.?((\s)+([\w-]+\.)*([\w-]+)\.?)*)\s*(#+(?<aftercomment>.*))?)$|^(\s*#+\s?(?<comment>.*))$|^(?<blank>\s*)$")]
    private static partial Regex ValidHostsRegex();
}
