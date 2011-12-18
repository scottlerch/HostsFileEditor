// <copyright file="HostsEntry.cs" company="N/A">
// Copyright 2011 Scott M. Lerch
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

namespace HostsFileEditor
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Net;
    using System.Net.NetworkInformation;
    using System.Text.RegularExpressions;
    using System.Threading;
    using HostsFileEditor.Extensions;
    using HostsFileEditor.Properties;
    using HostsFileEditor.Utilities;

    /// <summary>
    /// This class represents an individual hosts entry line in a hosts file.
    /// </summary>
    internal class HostsEntry 
        : INotifyPropertyChanged, IDataErrorInfo, IDisposable
    {
        #region Constants and Fields

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
        private static readonly Regex MatchValidHostEntry =
            new Regex(
                string.Format("^{0}|{1}|{2}$", MatchGroupValidEntry, MatchGroupComment, MatchGroupBlank),
                RegexOptions.Compiled);

        /// <summary>
        /// Error messages for each property.
        /// </summary>
        private Dictionary<string, string> errors = new Dictionary<string, string>();

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
        private Ping ping;

        #endregion

        #region Constructors and Destructors

        /// <summary>
        /// Initializes a new instance of the <see cref="HostsEntry"/> class.
        /// </summary>
        public HostsEntry()
        {
            this.valid = false;
            this.enabled = false;
            this.comment = string.Empty;
            this.hostnames = string.Empty;
            this.ipAddress = string.Empty;
            this.unparsedText = string.Empty;

            this.ping = new Ping();
            this.ping.PingCompleted += this.OnPingCompleted;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="HostsEntry"/> class.
        /// </summary>
        /// <param name="unparsedTextLine">
        /// The unparsed text line.
        /// </param>
        public HostsEntry(string unparsedTextLine) : this()
        {
            this.unparsedText = unparsedTextLine;

            Match match = MatchValidHostEntry.Match(this.unparsedText);

            this.valid = match.Success;

            if (match.Success)
            {
                if (match.Groups[Blank].Success)
                {
                    this.valid = false;
                    this.hostnamesValid = false;
                    this.ipAddressValid = false;
                    this.enabled = false;
                }
                else if (match.Groups[LineComment].Success)
                {
                    this.comment = match.Groups[LineComment].Value;
                    this.enabled = false;
                    this.valid = false;
                    this.hostnamesValid = false;
                    this.ipAddressValid = false;
                }
                else
                {
                    this.enabled = !match.Groups[Disabled].Success;
                    this.hostnames = match.Groups[Hostname].Value;
                    this.ipAddress = match.Groups[Address].Value;
                    this.comment = match.Groups[AfterComment].Value;

                    // Replace all 2 or more spaces with 1 space for aesthetics
                    this.hostnames = Regex.Replace(this.HostNames, @"[ ]{2,}", " ");

                    this.ValidateIpAddress();
                    this.ValidateHostnames();
                }

                // Since comments usually have a space after # strip
                // that first blank since it will be added back when file 
                // written
                if (this.comment.Length > 0 && this.comment[0] == ' ')
                {
                    this.comment = this.comment.Substring(1);
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="HostsEntry"/> class.
        /// </summary>
        /// <param name="entry">The entry to copy from.</param>
        public HostsEntry(HostsEntry entry)
        {
            this.comment = entry.comment;
            this.enabled = entry.enabled;
            this.hostnames = entry.hostnames;
            this.ipAddress = entry.ipAddress;
            this.unparsedText = entry.unparsedText;
            this.unparsedTextInvalid = entry.unparsedTextInvalid;
            this.valid = entry.valid;
            this.hostnamesValid = entry.hostnamesValid;
            this.ipAddressValid = entry.ipAddressValid;
            this.errors = new Dictionary<string, string>(entry.errors);
        }

        #endregion

        #region Public Events

        /// <summary>
        /// The property changed.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged = delegate { };

        #endregion

        #region Public Properties

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
        public string Error
        {
            get { return string.Join(Environment.NewLine, this.errors.Values.ToArray()); }
        }

        /// <summary>
        /// Gets or sets Comment.
        /// </summary>
        public string Comment
        {
            get
            {
                return this.comment;
            }

            set
            {
                value.ThrowIfNull("value");

                var local = this.comment;
                UndoManager.Instance.AddActions(
                    undoAction: () => { this.Comment = local; },
                    redoAction: () => { this.Comment = value; });

                this.Update(ref this.comment, value, () => this.Comment);
            }
        }

        /// <summary>
        /// Gets a value indicating whether this instance only contains a comment.
        /// </summary>
        public bool HasCommentOnly
        {
            get
            {
                return !this.Valid && !this.Enabled;
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether Enabled.
        /// </summary>
        public bool Enabled
        {
            get
            {
                return this.enabled;
            }

            set
            {
                var local = this.enabled;
                UndoManager.Instance.AddActions(
                    undoAction: delegate { this.Enabled = local; },
                    redoAction: delegate { this.Enabled = value; });

                this.Update(ref this.enabled, value, () => this.Enabled);
            }
        }

        /// <summary>
        /// Gets or sets HostNames.
        /// </summary>
        public string HostNames
        {
            get
            {
                return this.hostnames;
            }

            set
            {
                value.ThrowIfNull("value");

                var local = this.hostnames;
                UndoManager.Instance.AddActions(
                    undoAction: delegate { this.HostNames = local; },
                    redoAction: delegate { this.HostNames = value.Trim(); });

                this.Update(ref this.hostnames, value.Trim(), () => this.HostNames, this.ValidateHostnames);
            }
        }

        /// <summary>
        /// Gets or sets IpAddress.
        /// </summary>
        public string IpAddress
        {
            get
            {
                return this.ipAddress;
            }

            set
            {
                value.ThrowIfNull("value");

                var local = this.ipAddress;
                UndoManager.Instance.AddActions(
                    undoAction: delegate { this.IpAddress = local; },
                    redoAction: delegate { this.IpAddress = value.Trim(); });

                this.Update(ref this.ipAddress, value.Trim(), () => this.IpAddress, this.ValidateIpAddress);
            }
        }

        /// <summary>
        /// Gets or sets UnparsedText.
        /// </summary>
        public string UnparsedText
        {
            get
            {
                if (this.unparsedTextInvalid)
                {
                    if (this.HasCommentOnly)
                    {
                        this.unparsedText = string.Format("# {0}", this.comment);
                    }
                    else
                    {
                        this.unparsedText = string.Format(
                            "{0}{1} {2}{3}",
                            !this.Enabled ? "# " : string.Empty,
                            this.IpAddress,
                            this.HostNames,
                            this.Comment.Trim() != string.Empty ? " # " + this.Comment : string.Empty);
                    }

                    // Comment out invalid lines so the hosts file still works
                    if (!this.valid)
                    {
                        this.unparsedText = "#" + this.unparsedText;
                    }

                    this.unparsedTextInvalid = false;
                }

                return this.unparsedText;
            }

            set
            {
                value.ThrowIfNull("value");
                this.Update(ref this.unparsedText, value, () => this.UnparsedText);
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether Valid.
        /// </summary>
        public bool Valid
        {
            get
            {
                return this.valid;
            }

            set
            {
                this.Update(ref this.valid, value, () => this.Valid);
            }
        }

        /// <summary>
        /// Gets the error message for the property with the given name.
        /// </summary>
        /// <param name="columnName">Name of property.</param>
        /// <returns>The error message for the property. The default is an
        /// empty string ("").</returns>
        public string this[string propertyName]
        {
            get 
            { 
                return this.errors.ContainsKey(propertyName) ? 
                    this.errors[propertyName] : 
                    string.Empty; 
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Returns a <see cref="System.String"/> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String"/> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            return string.Format("{0} {1} {2}", this.IpAddress, this.HostNames, this.Comment);
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, 
        /// releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Pings the current IP address.
        /// </summary>
        public void Ping()
        {
            this.ping.SendAsyncCancel();
            this.ping.SendAsync(this.ipAddress, SynchronizationContext.Current);
        }

        #endregion

        #region Methods

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources
        /// </summary>
        /// <param name="disposing"><c>true</c> to release both managed and 
        /// unmanaged resources; <c>false</c> to release only unmanaged 
        /// resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (this.ping != null)
                {
                    this.ping.PingCompleted -= this.OnPingCompleted;
                    this.ping.SendAsyncCancel();
                    this.ping.Dispose();
                    this.ping = null;
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
            this.PropertyChanged(this, new PropertyChangedEventArgs(name));
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
            this.Update(ref backing, value, property, delegate { });
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

                this.unparsedTextInvalid = true;

                bool prevValid = this.valid;
                performValidation();

                this.OnPropertyChanged(property.GetPropertyName());

                if (prevValid != this.valid)
                {
                    this.OnPropertyChanged(Reflect.GetPropertyName(() => this.Valid));
                    this.OnPropertyChanged(Reflect.GetPropertyName(() => this.IpAddress));
                    this.OnPropertyChanged(Reflect.GetPropertyName(() => this.HostNames));
                    this.OnPropertyChanged(Reflect.GetPropertyName(() => this.Enabled));
                }
            }
        }

        /// <summary>
        /// Validates the hostnames.
        /// </summary>
        private void ValidateHostnames()
        {
            if (string.IsNullOrWhiteSpace(this.hostnames) && !this.enabled)
            {
                this.errors[Reflect.GetPropertyName(() => this.HostNames)] = string.Empty;
                this.hostnamesValid = true;
            }
            else
            {
                this.hostnamesValid = Regex.IsMatch(this.hostnames, "^" + MatchHostnames + "$");

                if (!this.hostnamesValid)
                {
                    this.errors[Reflect.GetPropertyName(() => this.HostNames)] = Resources.InvalidHostnames;
                }
                else
                {
                    this.errors[Reflect.GetPropertyName(() => this.HostNames)] = string.Empty;
                }
            }

            this.valid = this.ipAddressValid && this.hostnamesValid;
        }

        /// <summary>
        /// Validates the IP address.
        /// </summary>
        private void ValidateIpAddress()
        {
            if (string.IsNullOrWhiteSpace(this.ipAddress) && !this.enabled)
            {
                this.errors[Reflect.GetPropertyName(() => this.IpAddress)] = string.Empty;
                this.valid = false;
            }
            else
            {
                IPAddress dummy;
                this.ipAddressValid = IPAddress.TryParse(this.IpAddress, out dummy);

                if (!this.ipAddressValid)
                {
                    this.errors[Reflect.GetPropertyName(() => this.IpAddress)] = Resources.InvalidIPAddress;
                }
                else
                {
                    this.errors[Reflect.GetPropertyName(() => this.IpAddress)] = string.Empty;

                    if (AutoPingIPAddress)
                    {
                        this.Ping();
                    }
                }
            }

            this.valid = this.ipAddressValid && this.hostnamesValid;
        }

        /// <summary>
        /// Called when ping completed.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The 
        /// <see cref="System.Net.NetworkInformation.PingCompletedEventArgs"/> 
        /// instance containing the event data.</param>
        private void OnPingCompleted(object sender, PingCompletedEventArgs e)
        {
            if (!e.Cancelled)
            {
                if (e.Reply.Status != IPStatus.Success)
                {
                    SynchronizationContext syncContext = e.UserState as SynchronizationContext;
                    syncContext.Post(
                        state =>
                        {
                            this.errors[Reflect.GetPropertyName(() => this.IpAddress)] =
                                string.Format(Resources.PingFailed, e.Reply.Status.ToString());

                            this.OnPropertyChanged(Reflect.GetPropertyName(() => this.IpAddress));
                        }, 
                        null);
                }
            }
        }

        #endregion
    }
}