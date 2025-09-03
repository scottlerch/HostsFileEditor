// <copyright file="HostsEntry.cs" company="N/A">
// Copyright 2025 Scott M. Lerch
// 
// This file is part of HostsFileEditor.
// 
// HostsFileEditor is free software: you can redistribute it and/or modify it 
// under the terms of the GNU General Public License as published by the Free 
// Software Foundation, either version 2 of the License, or (at your option)
// any later version.
// 
// HostsFileEditor is distributed in the hope that it will be useful, but 
// WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
// or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for
// more details.
// 
// You should have received a copy of the GNU General Public   License along
// with HostsFileEditor. If not, see http://www.gnu.org/licenses/.
// </copyright>

using HostsFileEditor.Properties;
using HostsFileEditor.Utilities;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Net;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;

namespace HostsFileEditor;

/// <summary>
/// This class represents an individual hosts entry line in a hosts file.
/// </summary>
internal partial class HostsEntry 
    : INotifyPropertyChanged, IDataErrorInfo, IDisposable
{
    /// <summary>
    /// The address regex group.
    /// </summary>
    private const string Address = "ipaddress";

    /// <summary>
    /// The after comment regex group.
    /// </summary>
    private const string AfterComment = "aftercomment";

    /// <summary>
    /// The blank regex group.
    /// </summary>
    private const string Blank = "blank";

    /// <summary>
    /// The disabled regex group.
    /// </summary>
    private const string Disabled = "disabled";

    /// <summary>
    /// The hostname regex group.
    /// </summary>
    private const string Hostname = "hostname";

    /// <summary>
    /// The line comment regex group.
    /// </summary>
    private const string LineComment = "comment";

    /// <summary>
    /// The pattern to match blank.
    /// </summary>
    private const string MatchGroupBlank = @"(?<" + Blank + @">\s*)";

    /// <summary>
    /// The pattern to match comment.
    /// </summary>
    private const string MatchGroupComment = @"(\s*#+\s?(?<" + LineComment + @">.*))";

    /// <summary>
    /// The pattern to match valid.
    /// </summary>
    private const string MatchGroupValidEntry =
        @"((?<" + Disabled + @">#+)?\s*(?<" + Address + @">" + MatchIpAddress + @")\s+(?<" + Hostname + @">"
        + MatchHostnames + @")\s*(#+(?<" + AfterComment + @">.*))?)";

    /// <summary>
    /// The pattern to match hostnames.
    /// </summary>
    private const string MatchHostnames = @"([\w-]+\.)*([\w-]+)((\s)+([\w-]+\.)*([\w-]+))*";

    /// <summary>
    /// The pattern to match IP addresses.
    /// Just match all non-whitespace and let IPAddress class do the rest.
    /// </summary>
    private const string MatchIpAddress = @"[^\s]+";

    /// <summary>
    /// The pattern to match valid host entry.
    /// </summary>
    private static readonly Regex MatchValidHostEntry = ValidHostsRegex();

    /// <summary>
    /// Error messages for each property.
    /// </summary>
    private readonly Dictionary<string, string> errors = [];

    /// <summary>
    /// The comment.
    /// </summary>
    private string comment;

    /// <summary>
    /// Determines if this host entry is enabled.
    /// </summary>
    private bool enabled;

    /// <summary>
    /// The hostnames.
    /// </summary>
    private string hostnames;

    /// <summary>
    /// The IP address.
    /// </summary>
    private string ipAddress;

    /// <summary>
    /// The unparsed text.
    /// </summary>
    private string unparsedText;

    /// <summary>
    /// The unparsed text invalid.
    /// </summary>
    private bool unparsedTextInvalid;

    /// <summary>
    /// Determines if this host entry is valid.
    /// </summary>
    private bool valid;

    /// <summary>
    /// Determines if IP address is valid.
    /// </summary>
    private bool ipAddressValid;

    /// <summary>
    /// Determine if hostnames is valid.
    /// </summary>
    private bool hostnamesValid;

    /// <summary>
    /// Object used to ping IP address for validation.
    /// </summary>
    private Ping? ping;

    /// <summary>
    /// Initializes a new instance of the <see cref="HostsEntry"/> class.
    /// </summary>
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

    /// <summary>
    /// Initializes a new instance of the <see cref="HostsEntry"/> class.
    /// </summary>
    /// <param name="unparsedTextLine">
    /// The unparsed text line.
    /// </param>
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

                // Replace all 2 or more spaces with 1 space for aesthetics
                hostnames = TwoSpaceMatchRegex().Replace(HostNames, " ");

                ValidateIpAddress();
                ValidateHostnames();

                // IP address or hostnames are not valid so
                // disable hosts entry and reset comment to entire line
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

            // Since comments usually have a space after # strip
            // that first blank since it will be added back when file 
            // written
            if (comment.Length > 0 && comment[0] == ' ')
            {
                comment = comment[1..];
            }
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HostsEntry"/> class.
    /// </summary>
    /// <param name="entry">The entry to copy from.</param>
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

    /// <summary>
    /// The property changed.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Gets or sets a value indicating whether auto ping IP addresses
    /// when they change.
    /// </summary>
    public static bool AutoPingIPAddress { get; set; }

    /// <summary>
    /// Gets an error message indicating what is wrong with this object.
    /// </summary>
    /// <returns>An error message indicating what is wrong with this 
    /// object. The default is an empty string ("").</returns>
    public string Error => string.Join(Environment.NewLine, errors.Values.ToArray());

    /// <summary>
    /// Gets or sets Comment.
    /// </summary>
    public string Comment
    {
        get
        {
            return comment;
        }

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

    /// <summary>
    /// Gets a value indicating whether this instance only contains a comment.
    /// </summary>
    public bool HasCommentOnly => !Valid && !Enabled;

    /// <summary>
    /// Gets or sets a value indicating whether Enabled.
    /// </summary>
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

    /// <summary>
    /// Gets or sets HostNames.
    /// </summary>
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

    /// <summary>
    /// Gets or sets IpAddress.
    /// </summary>
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

    /// <summary>
    /// Gets or sets UnparsedText.
    /// </summary>
    public string UnparsedText
    {
        get
        {
            if (unparsedTextInvalid)
            {
                if (HasCommentOnly)
                {
                    unparsedText = $"# {comment}";
                }
                else
                {
                    unparsedText = string.Format(
                        "{0}{1} {2}{3}",
                        !Enabled ? "# " : string.Empty,
                        IpAddress,
                        HostNames,
                        Comment.Trim() != string.Empty ? " # " + Comment : string.Empty);
                }

                // Comment out invalid lines so the hosts file still works
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

    /// <summary>
    /// Gets or sets a value indicating whether Valid.
    /// </summary>
    public bool Valid
    {
        get => valid;
        set => Update(ref valid, value, () => Valid);
    }

    /// <summary>
    /// Gets the error message for the property with the given name.
    /// </summary>
    /// <param name="columnName">Name of property.</param>
    /// <returns>The error message for the property. The default is an
    /// empty string ("").</returns>
    public string this[string propertyName] => errors.TryGetValue(propertyName, out string? value) ? value : string.Empty; 

    /// <summary>
    /// Returns a <see cref="string"/> that represents this instance.
    /// </summary>
    /// <returns>
    /// A <see cref="string"/> that represents this instance.
    /// </returns>
    public override string ToString()
    {
        return $"{IpAddress} {HostNames} {Comment}";
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, 
    /// releasing, or resetting unmanaged resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Pings the current IP address.
    /// </summary>
    public void Ping()
    {
        ping?.SendAsyncCancel();
        ping?.SendAsync(ipAddress, SynchronizationContext.Current);
    }

    /// <inheritdoc />
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

    /// <summary>
    /// Called when ping completed.
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The 
    /// <see cref="System.Net.NetworkInformation.PingCompletedEventArgs"/> 
    /// instance containing the event data.</param>
    private void OnPingCompleted(object? sender, PingCompletedEventArgs e)
    {
        if (!e.Cancelled && e.Reply != null)
        {
            if (e.Reply.Status != IPStatus.Success)
            {
                SynchronizationContext? syncContext = e.UserState as SynchronizationContext;
                syncContext?.Post(
                    state =>
                    {
                        errors[Reflect.GetPropertyName(() => IpAddress)] =
                            string.Format(Resources.PingFailed, e.Reply.Status.ToString());

                        OnPropertyChanged(Reflect.GetPropertyName(() => IpAddress));
                    }, 
                    null);
            }
        }
    }

    /// <summary>
    /// Raise PropertyChanged event.
    /// </summary>
    /// <param name="name">
    /// The name of the property.
    /// </param>
    private void OnPropertyChanged(string name)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>
    /// Update the backing field and raise PropertyChanged event as
    ///  necessary.
    /// </summary>
    /// <param name="backing">
    /// The backing field.
    /// </param>
    /// <param name="value">
    /// The new value.
    /// </param>
    /// <param name="property">
    /// The property being updated.
    /// </param>
    /// <typeparam name="T">
    /// Type of backing field.
    /// </typeparam>
    private void Update<T>(ref T backing, T value, Expression<Func<T>> property)
    {
        Update(ref backing, value, property, delegate { });
    }

    /// <summary>
    /// Update the backing field and raise PropertyChanged event as
    /// necessary.
    /// </summary>
    /// <typeparam name="T">
    /// Type of backing field.
    /// </typeparam>
    /// <param name="backing">
    /// The backing field.
    /// </param>
    /// <param name="value">
    /// The new value.
    /// </param>
    /// <param name="property">
    /// The property being updated.
    /// </param>
    /// <param name="performValidation">
    /// The delegate to perform validation.
    /// </param>
    private void Update<T>(ref T backing, T value, Expression<Func<T>> property, Action performValidation)
    {
        if (!Equals(backing, value))
        {
            backing = value;

            unparsedTextInvalid = true;

            bool prevValid = valid;
            performValidation();

            OnPropertyChanged(property.GetPropertyName());

            if (prevValid != valid)
            {
                OnPropertyChanged(Reflect.GetPropertyName(() => Valid));
                OnPropertyChanged(Reflect.GetPropertyName(() => IpAddress));
                OnPropertyChanged(Reflect.GetPropertyName(() => HostNames));
                OnPropertyChanged(Reflect.GetPropertyName(() => Enabled));
            }
        }
    }

    /// <summary>
    /// Validates the hostnames.
    /// </summary>
    private void ValidateHostnames()
    {
        if (string.IsNullOrWhiteSpace(hostnames) && !enabled)
        {
            errors[Reflect.GetPropertyName(() => HostNames)] = string.Empty;
            hostnamesValid = true;
        }
        else
        {
            hostnamesValid = HostNameRegex().IsMatch(hostnames);

            if (!hostnamesValid)
            {
                errors[Reflect.GetPropertyName(() => HostNames)] = Resources.InvalidHostnames;
            }
            else
            {
                errors[Reflect.GetPropertyName(() => HostNames)] = string.Empty;
            }
        }

        valid = ipAddressValid && hostnamesValid;
    }

    /// <summary>
    /// Validates the IP address.
    /// </summary>
    private void ValidateIpAddress()
    {
        if (string.IsNullOrWhiteSpace(ipAddress) && !enabled)
        {
            errors[Reflect.GetPropertyName(() => IpAddress)] = string.Empty;
            valid = false;
        }
        else
        {
            ipAddressValid = IPAddress.TryParse(IpAddress, out IPAddress? dummy);

            if (!ipAddressValid)
            {
                errors[Reflect.GetPropertyName(() => IpAddress)] = Resources.InvalidIPAddress;
            }
            else
            {
                errors[Reflect.GetPropertyName(() => IpAddress)] = string.Empty;

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