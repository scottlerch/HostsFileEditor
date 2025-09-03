// <copyright file="FormExtensions.cs" company="N/A">
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

namespace HostsFileEditor.Extensions;

/// <summary>
/// Helper Form extension methods.
/// </summary>
internal static class FormExtensions
{
    /// <summary>
    /// Show form if not visible, otherwise just Activate.
    /// </summary>
    /// <param name="form">
    /// The form to show or activate.
    /// </param>
    public static void ShowOrActivate(this Form form)
    {
        if (form.Visible)
        {
            form.Activate();
        }
        else
        {
            form.Show();
        }
    }
}