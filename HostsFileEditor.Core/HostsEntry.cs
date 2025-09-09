using HostsFileEditor.Properties;
using HostsFileEditor.Utilities;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Net;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;

namespace HostsFileEditor;

public partial class HostsEntry : INotifyPropertyChanged, IDataErrorInfo, IDisposable
{
    private const string Address = "ipaddress";
    private const string AfterComment = "aftercomment";
    private const string Blank = "blank";
    private const string Disabled = "disabled";
    private const string Hostname = "hostname";
    private const string LineComment = "comment";

    private const string MatchGroupBlank = @"(?<" + Blank + @">\s*)";
    private const string MatchGroupComment = @"(\s*#+\s?(?<" + LineComment + @">.*))";
    private const string MatchGroupValidEntry =
        @"((?<" + Disabled + @">#+)?\s*(?<" + Address + @">" + MatchIpAddress + @")\s+(?<" + Hostname + @">"
        + MatchHostnames + @")\s*(#+(?<" + AfterComment + @">.*))?)";

    private const string MatchHostnames = @"([\w-]+\.)*([\w-]+)((\s)+([\w-]+\.)*([\w-]+))*";
    private const string MatchIpAddress = @"[^\s]+";

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
    private Ping? _ping;

    public HostsEntry()
    {
        _valid = false;
        _enabled = false;
        _comment = string.Empty;
        _hostnames = string.Empty;
        _ipAddress = string.Empty;
        _unparsedText = string.Empty;

        _ping = new Ping();
        _ping.PingCompleted += OnPingCompleted;
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
    }

    public HostsEntry(HostsEntry entry)
    {
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

            var local = _hostnames;
            UndoManager.Instance.AddActions(
                undoAction: () => HostNames = local,
                redoAction: () => HostNames = value.Trim());

            Update(ref _hostnames, value.Trim(), nameof(HostNames), ValidateHostnames);
        }
    }

    public string IpAddress
    {
        get => _ipAddress;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            var local = _ipAddress;
            UndoManager.Instance.AddActions(
                undoAction: () => IpAddress = local,
                redoAction: () => IpAddress = value.Trim());

            Update(ref _ipAddress, value.Trim(), nameof(IpAddress), ValidateIpAddress);
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
                    : string.Format(
                        "{0}{1} {2}{3}",
                        !Enabled ? "# " : string.Empty,
                        IpAddress,
                        HostNames,
                        Comment.Trim() != string.Empty ? " # " + Comment : string.Empty);

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

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public void Ping()
    {
        _ping?.SendAsyncCancel();
        _ping?.SendAsync(_ipAddress, SynchronizationContext.Current);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_ping != null)
            {
                _ping.PingCompleted -= OnPingCompleted;
                _ping.SendAsyncCancel();
                _ping.Dispose();
                _ping = null;
            }
        }
    }

    private void OnPingCompleted(object? sender, PingCompletedEventArgs e)
    {
        if (!e.Cancelled && e.Reply != null)
        {
            if (e.Reply.Status != IPStatus.Success)
            {
                var syncContext = e.UserState as SynchronizationContext;
                syncContext?.Post(
                    state =>
                    {
                        _errors[nameof(IpAddress)] =
                            string.Format(Resources.PingFailed, e.Reply.Status.ToString());

                        OnPropertyChanged(nameof(IpAddress));
                    },
                    null);
            }
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
            _errors[nameof(IpAddress)] = string.Empty;
            _valid = false;
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

    [GeneratedRegex(@"^((?<disabled>#+)?\s*(?<ipaddress>[^\s]+)\s+(?<hostname>([\w-]+\.)*([\w-]+)((\s)+([\w-]+\.)*([\w-]+))*)\s*(#+(?<aftercomment>.*))?)|(\s*#+\s?(?<comment>.*))|(?<blank>\s*)$", RegexOptions.Compiled)]
    private static partial Regex ValidHostsRegex();
}
