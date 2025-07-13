// <copyright file="FileEx.cs" company="N/A">
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

namespace HostsFileEditor.Utilities;

/// <summary>
/// File helper methods.
/// </summary>
public static class FileEx
{
    /// <summary>
    /// Disables the attributes specified on the file if the file exists.
    /// </summary>
    /// <param name="filePath">The file path.</param>
    /// <param name="attributes">The attributes.</param>
    /// <returns>Disposable interface to reset attributes.</returns>
    public static IDisposable DisableAttributes(string filePath, FileAttributes attributes)
    {
        return new AttributeDisabler(filePath, attributes);
    }

    /// <summary>
    /// Private class used to reset attributes with Disposable pattern.
    /// </summary>
    private class AttributeDisabler : IDisposable
    {
        /// <summary>
        /// Determines if attributes are disabled.
        /// </summary>
        private readonly bool areAttributesDisabled;

        /// <summary>
        /// Original file attributes.
        /// </summary>
        private readonly FileAttributes originalAttributes;

        /// <summary>
        /// Attributes to disable.
        /// </summary>
        private readonly FileAttributes disableAttributes;

        /// <summary>
        /// The file path.
        /// </summary>
        private readonly string filePath;

        /// <summary>
        /// Initializes a new instance of the <see cref="AttributeDisabler"/> class.
        /// </summary>
        /// <param name="filePath">The file path.</param>
        /// <param name="disableAttributes">The disable attributes.</param>
        public AttributeDisabler(string filePath, FileAttributes disableAttributes)
        {
            ArgumentNullException.ThrowIfNull(filePath);

            this.filePath = filePath;
            this.disableAttributes = disableAttributes;

            if (File.Exists(filePath))
            {
                originalAttributes = File.GetAttributes(filePath);
                areAttributesDisabled = originalAttributes.HasFlag(disableAttributes);
                
                if (areAttributesDisabled)
                {
                    File.SetAttributes(filePath, ~this.disableAttributes & originalAttributes);
                }
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            // Reset attributes if it was read-only
            if (areAttributesDisabled && File.Exists(filePath))
            {
                File.SetAttributes(filePath, originalAttributes);
            }
        }
    }
}
