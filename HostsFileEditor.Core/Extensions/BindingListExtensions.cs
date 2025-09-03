// Copyright 2025 Scott M. Lerch
// Licensed under GPL-2.0-or-later

using System.ComponentModel;

namespace HostsFileEditor.Extensions;

internal static class BindingListExtensions
{
    public static void BatchUpdate<T>(this BindingList<T> list, Action action)
    {
        list.RaiseListChangedEvents = false;
        action();
        list.RaiseListChangedEvents = true;
        list.ResetBindings();
    }
}
