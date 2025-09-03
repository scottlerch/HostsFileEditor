// <copyright file="BindingListExtensions.cs" company="N/A">
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

using System.ComponentModel;

namespace HostsFileEditor.Extensions;

/// <summary>
/// Helper BindingList extension methods.
/// </summary>
internal static class BindingListExtensions
{
    /// <summary>
    /// Perform batch update on list.  This will turn off ListChangedEvents
    /// while performing specified action and call a ResetBindings at the
    /// end.
    /// </summary>
    /// <param name="list">
    /// The binding list.
    /// </param>
    /// <param name="action">
    /// The action to perform.
    /// </param>
    /// <typeparam name="T">
    /// Type of object contained in BindingList.
    /// </typeparam>
    public static void BatchUpdate<T>(this BindingList<T> list, Action action)
    {
        list.RaiseListChangedEvents = false;
        action();
        list.RaiseListChangedEvents = true;
        list.ResetBindings();
    }
}