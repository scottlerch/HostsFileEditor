// <copyright file="StringExtensions.cs" company="N/A">
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

namespace HostsFileEditor.Extensions
{
    using System;

    /// <summary>
    /// Helper string extension methods.
    /// </summary>
    internal static class StringExtensions
    {
        #region Public Methods

        /// <summary>
        /// Strip all spaces from a string.
        /// </summary>
        /// <param name="value">
        /// The value.
        /// </param>
        /// <returns>
        /// String with all spaced stripped out.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Argument cannot be null.
        /// </exception>
        public static string StripSpaces(this string value)
        {
            value.ThrowIfNull();
            return value.Replace(" ", string.Empty);
        }

        #endregion
    }
}