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

    private static readonly Regex _matchValidHostEntry = ValidHostsRegex();

    private readonly Dictionary<string, string> _errors = [];

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

        var match = _matchValidHostEntry.Match(_unparsedText);

        _valid = match.Success;

        if (match.Success)
        {
            if (match.Groups[Blank].Success)
            {
                _valid = false;
                _hostnamesValid = false;
                _ipAddressValid = false;
                _enabled = false;
            }
            else if (match.Groups[LineComment].Success)
            {
                _comment = match.Groups[LineComment].Value;
                _enabled = false;
                _valid = false;
                _hostnamesValid = false;
                _ipAddressValid = false;
            }
            else
            {
                _enabled = !match.Groups[Disabled].Success;
                _hostnames = match.Groups[Hostname].Value;
                _ipAddress = match.Groups[Address].Value;
                _comment = match.Groups[AfterComment].Value;

                _hostnames = TwoSpaceMatchRegex().Replace(HostNames, " ");

                ValidateIpAddress();
                ValidateHostnames();

                if (!_valid)
                {
                    _enabled = false;
                    _hostnames = string.Empty;
                    _ipAddress = string.Empty;
                    _errors.Clear();
                    _comment = _unparsedText.TrimStart(' ', '\t', '#');
                    _unparsedTextInvalid = false;
                }
            }

            if (_comment.Length > 0 && _comment[0] == ' ')
            {
                _comment = _comment[1..];
            }
        }
        else
        {
            // The line matched no pattern (e.g. an IP with a trailing port/path, or other
            // malformed content). Preserve the raw text as a comment so it round-trips intact
            // instead of rendering as an empty, uneditable row. _valid/_enabled are already
            // false and the ip/hostname fields empty.
            _comment = _unparsedText.TrimStart(' ', '\t', '#');
        }
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
        _errors = new Dictionary<string, string>(entry._errors);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public static bool AutoPingIPAddress { get; set; }

    public string Error => string.Join(Environment.NewLine, [.. _errors.Values]);

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

    public string this[string propertyName] => _errors.TryGetValue(propertyName, out var value) ? value : string.Empty;

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
            _errors[nameof(IpAddress)] = string.Format(CultureInfo.CurrentCulture, Resources.PingFailed, status);
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
            _errors[nameof(HostNames)] = string.Empty;
            _hostnamesValid = true;
        }
        else
        {
            _hostnamesValid = HostNameRegex().IsMatch(_hostnames);

            _errors[nameof(HostNames)] = !_hostnamesValid ? Resources.InvalidHostnames : string.Empty;
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
            _errors[nameof(IpAddress)] = string.Empty;
            _ipAddressValid = true;
        }
        else
        {
            _ipAddressValid = IPAddress.TryParse(IpAddress, out var _);

            if (!_ipAddressValid)
            {
                _errors[nameof(IpAddress)] = Resources.InvalidIPAddress;
            }
            else
            {
                _errors[nameof(IpAddress)] = string.Empty;

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
    [GeneratedRegex(@"^((?<disabled>#+)?\s*(?<ipaddress>[^\s]+)\s+(?<hostname>([\w-]+\.)*([\w-]+)((\s)+([\w-]+\.)*([\w-]+))*)\s*(#+(?<aftercomment>.*))?)$|^(\s*#+\s?(?<comment>.*))$|^(?<blank>\s*)$", RegexOptions.Compiled)]
    private static partial Regex ValidHostsRegex();
}
