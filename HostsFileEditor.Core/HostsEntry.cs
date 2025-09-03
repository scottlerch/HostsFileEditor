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

    private static readonly Regex MatchValidHostEntry = ValidHostsRegex();

    private readonly Dictionary<string, string> errors = [];

    private string comment;
    private bool enabled;
    private string hostnames;
    private string ipAddress;
    private string unparsedText;
    private bool unparsedTextInvalid;
    private bool valid;
    private bool ipAddressValid;
    private bool hostnamesValid;
    private Ping? ping;

    public HostsEntry()
    {
        valid = false;
        enabled = false;
        comment = string.Empty;
        hostnames = string.Empty;
        ipAddress = string.Empty;
        unparsedText = string.Empty;

        ping = new Ping();
        ping.PingCompleted += OnPingCompleted;
    }

    public HostsEntry(string unparsedTextLine) : this()
    {
        unparsedText = unparsedTextLine;

        Match match = MatchValidHostEntry.Match(unparsedText);

        valid = match.Success;

        if (match.Success)
        {
            if (match.Groups[Blank].Success)
            {
                valid = false;
                hostnamesValid = false;
                ipAddressValid = false;
                enabled = false;
            }
            else if (match.Groups[LineComment].Success)
            {
                comment = match.Groups[LineComment].Value;
                enabled = false;
                valid = false;
                hostnamesValid = false;
                ipAddressValid = false;
            }
            else
            {
                enabled = !match.Groups[Disabled].Success;
                hostnames = match.Groups[Hostname].Value;
                ipAddress = match.Groups[Address].Value;
                comment = match.Groups[AfterComment].Value;

                hostnames = TwoSpaceMatchRegex().Replace(HostNames, " ");

                ValidateIpAddress();
                ValidateHostnames();

                if (!valid)
                {
                    enabled = false;
                    hostnames = string.Empty;
                    ipAddress = string.Empty;
                    errors.Clear();
                    comment = unparsedText.TrimStart(' ', '\t', '#');
                    unparsedTextInvalid = false;
                }
            }

            if (comment.Length > 0 && comment[0] == ' ')
            {
                comment = comment[1..];
            }
        }
    }

    public HostsEntry(HostsEntry entry)
    {
        comment = entry.comment;
        enabled = entry.enabled;
        hostnames = entry.hostnames;
        ipAddress = entry.ipAddress;
        unparsedText = entry.unparsedText;
        unparsedTextInvalid = entry.unparsedTextInvalid;
        valid = entry.valid;
        hostnamesValid = entry.hostnamesValid;
        ipAddressValid = entry.ipAddressValid;
        errors = new Dictionary<string, string>(entry.errors);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public static bool AutoPingIPAddress { get; set; }

    public string Error => string.Join(Environment.NewLine, [.. errors.Values]);

    public string Comment
    {
        get => comment;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            var local = comment;
            UndoManager.Instance.AddActions(
                undoAction: () => Comment = local,
                redoAction: () => Comment = value);

            Update(ref comment, value, () => Comment);
        }
    }

    public bool HasCommentOnly => !Valid && !Enabled;

    public bool Enabled
    {
        get => enabled;
        set
        {
            var local = enabled;
            UndoManager.Instance.AddActions(
                undoAction: () => Enabled = local,
                redoAction: () => Enabled = value);

            Update(ref enabled, value, () => Enabled);
        }
    }

    public string HostNames
    {
        get => hostnames;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            var local = hostnames;
            UndoManager.Instance.AddActions(
                undoAction: () => HostNames = local,
                redoAction: () => HostNames = value.Trim());

            Update(ref hostnames, value.Trim(), () => HostNames, ValidateHostnames);
        }
    }

    public string IpAddress
    {
        get => ipAddress;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            var local = ipAddress;
            UndoManager.Instance.AddActions(
                undoAction: () => IpAddress = local,
                redoAction: () => IpAddress = value.Trim());

            Update(ref ipAddress, value.Trim(), () => IpAddress, ValidateIpAddress);
        }
    }

    public string UnparsedText
    {
        get
        {
            if (unparsedTextInvalid)
            {
                unparsedText = HasCommentOnly
                    ? $"# {comment}"
                    : string.Format(
                        "{0}{1} {2}{3}",
                        !Enabled ? "# " : string.Empty,
                        IpAddress,
                        HostNames,
                        Comment.Trim() != string.Empty ? " # " + Comment : string.Empty);

                if (!valid)
                {
                    unparsedText = "#" + unparsedText;
                }

                unparsedTextInvalid = false;
            }

            return unparsedText;
        }

        set
        {
            ArgumentNullException.ThrowIfNull(value);
            Update(ref unparsedText, value, () => UnparsedText);
        }
    }

    public bool Valid
    {
        get => valid;
        set => Update(ref valid, value, () => Valid);
    }

    public string this[string propertyName] => errors.TryGetValue(propertyName, out var value) ? value : string.Empty;

    public override string ToString() => $"{IpAddress} {HostNames} {Comment}";

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public void Ping()
    {
        ping?.SendAsyncCancel();
        ping?.SendAsync(ipAddress, SynchronizationContext.Current);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (ping != null)
            {
                ping.PingCompleted -= OnPingCompleted;
                ping.SendAsyncCancel();
                ping.Dispose();
                ping = null;
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
                        errors[Utilities.Reflect.GetPropertyName(() => IpAddress)] =
                            string.Format(Resources.PingFailed, e.Reply.Status.ToString());

                        OnPropertyChanged(Utilities.Reflect.GetPropertyName(() => IpAddress));
                    },
                    null);
            }
        }
    }

    private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private void Update<T>(ref T backing, T value, Expression<Func<T>> property) => Update(ref backing, value, property, delegate { });

    private void Update<T>(ref T backing, T value, Expression<Func<T>> property, Action performValidation)
    {
        if (!Equals(backing, value))
        {
            backing = value;

            unparsedTextInvalid = true;

            var prevValid = valid;
            performValidation();

            OnPropertyChanged(property.GetPropertyName());

            if (prevValid != valid)
            {
                OnPropertyChanged(Utilities.Reflect.GetPropertyName(() => Valid));
                OnPropertyChanged(Utilities.Reflect.GetPropertyName(() => IpAddress));
                OnPropertyChanged(Utilities.Reflect.GetPropertyName(() => HostNames));
                OnPropertyChanged(Utilities.Reflect.GetPropertyName(() => Enabled));
            }
        }
    }

    private void ValidateHostnames()
    {
        if (string.IsNullOrWhiteSpace(hostnames) && !enabled)
        {
            errors[Utilities.Reflect.GetPropertyName(() => HostNames)] = string.Empty;
            hostnamesValid = true;
        }
        else
        {
            hostnamesValid = HostNameRegex().IsMatch(hostnames);

            errors[Utilities.Reflect.GetPropertyName(() => HostNames)] = !hostnamesValid ? Resources.InvalidHostnames : string.Empty;
        }

        valid = ipAddressValid && hostnamesValid;
    }

    private void ValidateIpAddress()
    {
        if (string.IsNullOrWhiteSpace(ipAddress) && !enabled)
        {
            errors[Utilities.Reflect.GetPropertyName(() => IpAddress)] = string.Empty;
            valid = false;
        }
        else
        {
            ipAddressValid = IPAddress.TryParse(IpAddress, out IPAddress? dummy);

            if (!ipAddressValid)
            {
                errors[Utilities.Reflect.GetPropertyName(() => IpAddress)] = Resources.InvalidIPAddress;
            }
            else
            {
                errors[Utilities.Reflect.GetPropertyName(() => IpAddress)] = string.Empty;

                if (AutoPingIPAddress)
                {
                    Ping();
                }
            }
        }

        valid = ipAddressValid && hostnamesValid;
    }

    [GeneratedRegex(@"[ ]{2,}")]
    private static partial Regex TwoSpaceMatchRegex();

    [GeneratedRegex(@"^([\w-]+\.)*([\w-]+)((\s)+([\w-]+\.)*([\w-]+))*$")]
    private static partial Regex HostNameRegex();

    [GeneratedRegex(@"^((?<disabled>#+)?\s*(?<ipaddress>[^\s]+)\s+(?<hostname>([\w-]+\.)*([\w-]+)((\s)+([\w-]+\.)*([\w-]+))*)\s*(#+(?<aftercomment>.*))?)|(\s*#+\s?(?<comment>.*))|(?<blank>\s*)$", RegexOptions.Compiled)]
    private static partial Regex ValidHostsRegex();
}
