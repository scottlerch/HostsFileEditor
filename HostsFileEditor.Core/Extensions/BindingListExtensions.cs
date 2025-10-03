// Copyright 2025 Scott M. Lerch
// Licensed under GPL-2.0-or-later

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace HostsFileEditor.Extensions;

internal static class BindingListExtensions
{
    public static void BatchUpdate<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(this BindingList<T> list, Action action)
    {
        list.RaiseListChangedEvents = false;
        action();
        list.RaiseListChangedEvents = true;
        list.ResetBindings();
    }
}
