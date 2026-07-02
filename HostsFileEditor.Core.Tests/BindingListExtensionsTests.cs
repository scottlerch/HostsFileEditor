using HostsFileEditor.Extensions;
using System.ComponentModel;

namespace HostsFileEditor.Core.Tests;

[TestClass]
public class BindingListExtensionsTests
{
    [TestMethod]
    public void BatchUpdate_ActionThrows_RestoresListChangedEvents()
    {
        var list = new BindingList<int> { 1, 2, 3 };

        Should.Throw<InvalidOperationException>(() =>
            list.BatchUpdate(() => throw new InvalidOperationException()));

        // The finally must have re-enabled notifications; otherwise bound views freeze
        // for the rest of the session.
        list.RaiseListChangedEvents.ShouldBeTrue();

        var changes = 0;
        list.ListChanged += (_, _) => changes++;
        list.Add(4);
        changes.ShouldBeGreaterThan(0);
    }
}
