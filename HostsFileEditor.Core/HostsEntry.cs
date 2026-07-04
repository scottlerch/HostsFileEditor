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

                ValidateIpAddress();
                ValidateHostnames();

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
    // span engine against.
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
        if (s[i] == '#')
        {
            var contentStart = i;
            while (contentStart < n && s[contentStart] == '#') { contentStart++; }
            while (contentStart < n && char.IsWhiteSpace(s[contentStart])) { contentStart++; }

            if (TryParseEntry(s, contentStart, disabled: true, out var disabledEntry))
            {
                return disabledEntry;
            }
        }

        if (TryParseEntry(s, i, disabled: false, out var entry))
        {
            return entry;
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
        // non-whitespace run; the hostname must be structurally valid (else the regex would fall
        // through to the comment alternative). Allocates only the field strings, and only on success.
        static bool TryParseEntry(ReadOnlySpan<char> s, int start, bool disabled, out StructuralParse result)
        {
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

        if (status == IPStatus.Success)
        {
            return;
        }

        void ReportFailure()
        {
            SetError(nameof(IpAddress), string.Format(CultureInfo.CurrentCulture, Resources.PingFailed, status));
            OnPropertyChanged(nameof(IpAddress));
        }

        if (syncContext is not null)
        {
            syncContext.Post(_ => ReportFailure(), null);
        }
        else
        {
            ReportFailure();
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
            _ipAddressValid = IPAddress.TryParse(IpAddress, out var _);

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

    [GeneratedRegex(@"[ ]{2,}")]
    private static partial Regex TwoSpaceMatchRegex();

    [GeneratedRegex(@"^([\w-]+\.)*([\w-]+)((\s)+([\w-]+\.)*([\w-]+))*$")]
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
    [GeneratedRegex(@"^(\s*(?<disabled>#+)?\s*(?<ipaddress>[^\s]+)\s+(?<hostname>([\w-]+\.)*([\w-]+)((\s)+([\w-]+\.)*([\w-]+))*)\s*(#+(?<aftercomment>.*))?)$|^(\s*#+\s?(?<comment>.*))$|^(?<blank>\s*)$")]
    private static partial Regex ValidHostsRegex();
}
