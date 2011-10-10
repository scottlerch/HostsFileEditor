// <copyright file="ReferenceExtensions.cs" company="N/A">
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
    /// Helper class extension methods.
    /// </summary>
    public static class ReferenceExtensions
    {
        #region Public Methods

        /// <summary>
        /// Throw ArgumentNullException if value is null.  Used to make
        /// argument checking more readable.
        /// </summary>
        /// <remarks>
        /// Should eventually use code contracts.
        /// </remarks>
        /// <typeparam name="T">Type of object.</typeparam>
        /// <param name="value">The value.</param>
        /// <param name="name">The name.</param>
        /// <exception cref="ArgumentNullException">
        /// Argument cannot be null.
        /// </exception>
        public static void ThrowIfNull<T>(this T value, string name = "") where T : class
        {
            if (value == null)
            {
                throw new ArgumentNullException(name);
            }
        }

        #endregion
    }
}