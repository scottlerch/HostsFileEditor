// Copyright 2025 Scott M. Lerch
// Licensed under GPL-2.0-or-later

namespace HostsFileEditor.Extensions;

internal static class StringExtensions
{
    public static string StripSpaces(this string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.Replace(" ", string.Empty, StringComparison.Ordinal);
    }
}
