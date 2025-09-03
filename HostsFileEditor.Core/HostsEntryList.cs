using HostsFileEditor.Extensions;
using HostsFileEditor.Properties;
using HostsFileEditor.Utilities;
using System.ComponentModel;

namespace HostsFileEditor;

public class HostsEntryList : BindingList<HostsEntry>
{
    public static readonly string[] DefaultLines = Resources.hosts.Split(
        [Environment.NewLine],
        StringSplitOptions.None);

    public HostsEntryList(IEnumerable<string> entryLines, bool filterDefault)
        : this()
    {
        AddLines(entryLines, filterDefault);
    }

    public HostsEntryList()
    {
        AllowEdit = true;
        AllowNew = true;
        AllowRemove = true;
        RaiseListChangedEvents = true;
    }

    public string Error => this.Any(entry => !entry.Valid) ? Resources.InvalidHostEntries : string.Empty;

    public void AddLines(IEnumerable<string> lines, bool removeDefault = true)
    {
        ArgumentNullException.ThrowIfNull(lines);

        UndoManager.Instance.SuspendUndoRedo(() =>
        {
            int index = 0;
            foreach (string line in lines)
            {
                bool isDefaultLine =
                    index < DefaultLines.Length &&
                    line.Trim() == DefaultLines[index++].Trim();

                if (!removeDefault || !isDefaultLine)
                {
                    Add(new HostsEntry(line));
                }
            }
        });
    }

    public void MoveBefore(IEnumerable<HostsEntry> entries, HostsEntry beforeEntry)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(beforeEntry);

        this.BatchUpdate(() =>
        {
            var copy = entries.ToList();
            int insertIndex = IndexOf(beforeEntry) - 1;

            if (insertIndex >= 0)
            {
                UndoManager.Instance.BatchActions(() =>
                {
                    Remove(copy);

                    if (insertIndex > Count)
                    {
                        insertIndex = Count;
                    }
                    else if (insertIndex < 0)
                    {
                        insertIndex = 0;
                    }

                    foreach (HostsEntry entry in copy)
                    {
                        Insert(insertIndex++, entry);
                    }
                });
            }
        });
    }

    public void MoveAfter(IEnumerable<HostsEntry> entries, HostsEntry afterEntry)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(afterEntry);

        this.BatchUpdate(() =>
        {
            var copy = entries.ToList();
            int insertIndex = IndexOf(afterEntry) + 1;

            if (insertIndex < Count)
            {
                UndoManager.Instance.BatchActions(() =>
                {
                    Remove(copy);

                    if (insertIndex > Count)
                    {
                        insertIndex = Count;
                    }
                    else if (insertIndex < 0)
                    {
                        insertIndex = 0;
                    }

                    foreach (HostsEntry entry in copy)
                    {
                        Insert(insertIndex++, entry);
                    }
                });
            }
        });
    }

    public void InsertBefore(HostsEntry entry, HostsEntry? newEntry = null)
    {
        ArgumentNullException.ThrowIfNull(entry);

        int insertIndex = IndexOf(entry);
        Insert(insertIndex, newEntry ?? new HostsEntry());
    }

    public void InsertAfter(HostsEntry entry, HostsEntry? newEntry = null)
    {
        ArgumentNullException.ThrowIfNull(entry);

        int insertIndex = IndexOf(entry) + 1;
        Insert(insertIndex, newEntry ?? new HostsEntry());
    }

    public void Insert(HostsEntry entry, IEnumerable<HostsEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(entries);

        int insertIndex = IndexOf(entry);

        UndoManager.Instance.BatchActions(() =>
        {
            foreach (var newEntry in entries.ToList())
            {
                Insert(insertIndex++, newEntry);
            }
        });
    }

    public void Remove(IEnumerable<HostsEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        this.BatchUpdate(() =>
        {
            UndoManager.Instance.BatchActions(() =>
            {
                foreach (HostsEntry entry in entries.ToList())
                {
                    Remove(entry);
                }
            });
        });
    }

    public void Add()
    {
        Add(new HostsEntry());
    }

    public void SetEnabled(IEnumerable<HostsEntry> entries, bool isEnabled)
    {
        ArgumentNullException.ThrowIfNull(entries);

        this.BatchUpdate(() =>
        {
            UndoManager.Instance.BatchActions(() =>
            {
                foreach (HostsEntry entry in entries)
                {
                    entry.Enabled = isEnabled;
                }
            });
        });
    }

    protected override object AddNewCore()
    {
        return new HostsEntry(string.Empty);
    }

    protected override void InsertItem(int index, HostsEntry item)
    {
        UndoManager.Instance.AddActions(
            undoAction: () => Remove(item),
            redoAction: () => Insert(index, item));

        base.InsertItem(index, item);
    }

    protected override void RemoveItem(int index)
    {
        var item = this[index];

        UndoManager.Instance.AddActions(
            undoAction: () => Insert(index, item),
            redoAction: () => Remove(item));

        base.RemoveItem(index);
    }
}
