// <copyright file="HostsFilter.cs" company="N/A">
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

    using Equin.ApplicationFramework;

    /// <summary>
    /// This class represents 
    /// </summary>
    internal class HostsFilter : CompositeItemFilter<HostsEntry>
    {
        /// <summary>
        /// The comment filter.
        /// </summary>
        private PredicateItemFilter<HostsEntry> commentFilter;

        /// <summary>
        /// The disabled filter.
        /// </summary>
        private PredicateItemFilter<HostsEntry> disabledFilter;

        /// <summary>
        /// The custom filter.
        /// </summary>
        private PredicateItemFilter<HostsEntry> customFilter;

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
            this.commentFilter = new PredicateItemFilter<HostsEntry>(
                hostEntry => !hostEntry.HasCommentOnly);

            this.disabledFilter = new PredicateItemFilter<HostsEntry>(
                hostEntry => hostEntry.Enabled || hostEntry.HasCommentOnly);

            this.customFilter = new PredicateItemFilter<HostsEntry>(customFilter);

            this.AddFilter(this.customFilter);
        }

        /// <summary>
        /// Gets or sets a value indicating whether disabled are filtered.
        /// </summary>
        public bool Disabled
        {
            get
            { 
                return this.disabled;
            }

            set
            {
                if (this.disabled != value)
                {
                    this.disabled = value;

                    if (this.disabled)
                    {
                        this.AddFilter(this.disabledFilter);
                    }
                    else
                    {
                        this.RemoveFilter(this.disabledFilter);
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether comments are filtered.
        /// </summary>
        public bool Comments
        {
            get
            {
                return this.comments;
            }

            set
            {
                if (this.comments != value)
                {
                    this.comments = value;

                    if (this.comments)
                    {
                        this.AddFilter(this.commentFilter);
                    }
                    else
                    {
                        this.RemoveFilter(this.commentFilter);
                    }
                }
            }
        }
    }
}