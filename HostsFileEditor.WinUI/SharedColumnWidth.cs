using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Concurrent;
using Windows.Foundation;

namespace HostsFileEditor;

/// <summary>
/// Behavior to keep all elements (e.g. TextBoxes) that share a ColumnKey at the same width.
/// Shared width = maximum natural (unconstrained) width of realized elements.
/// Supports expansion + shrink. Uses MinWidth to align columns; Width is never set so editing can grow.
/// Implementation avoids infinite layout loops by:
///  - Measuring elements with MinWidth temporarily set to 0 via Measure() (no UpdateLayout calls)
///  - Suppressing event-triggered recalculations during a recalculation pass
///  - Coalescing pending recalculations per key
/// </summary>
public static class SharedColumnWidth
{
    private static readonly ConcurrentDictionary<string, List<WeakReference<FrameworkElement>>> _elements = new();
    private static readonly HashSet<string> _pendingRecalc = [];
    private static readonly object _pendingLock = new();
    private static int _suppressEvents; // >0 when recalculating

    public static string? GetColumnKey(DependencyObject obj) => (string?)obj.GetValue(ColumnKeyProperty);

    public static void SetColumnKey(DependencyObject obj, string? value) => obj.SetValue(ColumnKeyProperty, value);

    public static readonly DependencyProperty ColumnKeyProperty = DependencyProperty.RegisterAttached(
        "ColumnKey",
        typeof(string),
        typeof(SharedColumnWidth),
        new PropertyMetadata(null, OnColumnKeyChanged));

    private static void OnColumnKeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement fe)
        {
            return;
        }

        // Unhook previous
        fe.Loaded -= OnElementLoaded;
        fe.Unloaded -= OnElementUnloaded;
        fe.SizeChanged -= OnElementSizeChanged;
        if (fe is TextBox oldTb)
        {
            oldTb.TextChanged -= OnTextChanged;
        }

        if (e.NewValue is string key && !string.IsNullOrWhiteSpace(key))
        {
            fe.Loaded += OnElementLoaded;
            fe.Unloaded += OnElementUnloaded;
            fe.SizeChanged += OnElementSizeChanged;
            if (fe is TextBox tb)
            {
                tb.TextChanged += OnTextChanged;
            }

            RegisterElement(key, fe);
            ScheduleRecalc(key, fe);
        }
    }

    private static void RegisterElement(string key, FrameworkElement fe)
    {
        var list = _elements.GetOrAdd(key, _ => []);
        lock (list)
        {
            if (!list.Any(wr => wr.TryGetTarget(out var existing) && ReferenceEquals(existing, fe)))
            {
                list.Add(new WeakReference<FrameworkElement>(fe));
            }

            if (list.Count > 64)
            {
                list.RemoveAll(wr => !wr.TryGetTarget(out _));
            }
        }
    }

    private static void OnElementLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && GetColumnKey(fe) is { } key)
        {
            ScheduleRecalc(key, fe);
        }
    }

    private static void OnElementUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && GetColumnKey(fe) is { } key && _elements.TryGetValue(key, out var list))
        {
            lock (list)
            {
                list.RemoveAll(wr => !wr.TryGetTarget(out var existing) || ReferenceEquals(existing, fe));
            }
            ScheduleRecalc(key, fe); // potential shrink
        }
    }

    private static void OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressEvents > 0)
        {
            return;
        }

        if (sender is FrameworkElement fe && GetColumnKey(fe) is { } key)
        {
            ScheduleRecalc(key, fe);
        }
    }

    private static void OnElementSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_suppressEvents > 0)
        {
            return;
        }

        if (e.NewSize.Width != e.PreviousSize.Width && sender is FrameworkElement fe && GetColumnKey(fe) is { } key)
        {
            ScheduleRecalc(key, fe);
        }
    }

    private static void ScheduleRecalc(string key, FrameworkElement context)
    {
        lock (_pendingLock)
        {
            if (!_pendingRecalc.Add(key))
            {
                return; // already queued
            }
        }
        context.DispatcherQueue.TryEnqueue(() =>
        {
            try { Recalculate(key); }
            finally
            {
                lock (_pendingLock)
                {
                    _pendingRecalc.Remove(key);
                }
            }
        });
    }

    private static void Recalculate(string key)
    {
        if (!_elements.TryGetValue(key, out var list))
        {
            return;
        }

        List<FrameworkElement> live;
        lock (list)
        {
            live = [.. list.Select(wr => { wr.TryGetTarget(out var fe); return fe; })
                       .Where(fe => fe != null)
                       .Cast<FrameworkElement>()];
            if (live.Count != list.Count)
            {
                list.RemoveAll(wr => !wr.TryGetTarget(out _));
            }
        }
        if (live.Count == 0)
        {
            return;
        }

        Interlocked.Increment(ref _suppressEvents);
        try
        {
            // Capture natural widths
            var widths = new List<double>(live.Count);
            foreach (var fe in live)
            {
                var originalMin = fe.MinWidth;
                fe.MinWidth = 0; // allow shrink when measuring natural content
                fe.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                var natural = fe.DesiredSize.Width;
                widths.Add(natural);
                fe.MinWidth = originalMin; // restore before applying shared value later
            }
            var max = widths.Max();
            if (max <= 0)
            {
                return;
            }

            foreach (var fe in live)
            {
                if (fe.MinWidth != max)
                {
                    fe.MinWidth = max;
                }
            }
        }
        finally
        {
            Interlocked.Decrement(ref _suppressEvents);
        }
    }
}
