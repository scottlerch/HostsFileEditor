using Shouldly;

namespace HostsFileEditor.WinForm.Tests;

/// <summary>
/// Tests the classic edition's Equin view filter, which must delegate the three filter rules to the
/// single canonical <see cref="HostsEntry.MatchesFilter"/> predicate (issue #75) rather than restate
/// them — so its <c>Include</c> result stays identical to the modern edition's for the same inputs.
/// </summary>
[TestClass]
public sealed class HostsFilterTests
{
    private static readonly HostsEntry Enabled = new("127.0.0.1 localhost");
    private static readonly HostsEntry CommentOnly = new("# just a comment");
    private static readonly HostsEntry Disabled = new("# 10.0.0.1 disabled.test"); // disabled entry, valid IP

    [TestMethod]
    public void Include_NoFilters_KeepsEverything()
    {
        var filter = new HostsFilter(() => string.Empty);

        filter.Include(Enabled).ShouldBeTrue();
        filter.Include(CommentOnly).ShouldBeTrue();
        filter.Include(Disabled).ShouldBeTrue();
    }

    [TestMethod]
    public void Include_HideComments_DropsCommentOnlyRows()
    {
        var filter = new HostsFilter(() => string.Empty) { Comments = true };

        filter.Include(CommentOnly).ShouldBeFalse();
        filter.Include(Enabled).ShouldBeTrue();
        filter.Include(Disabled).ShouldBeTrue();
    }

    [TestMethod]
    public void Include_HideDisabled_DropsDisabledEntriesButNotComments()
    {
        var filter = new HostsFilter(() => string.Empty) { Disabled = true };

        filter.Include(Disabled).ShouldBeFalse();
        filter.Include(CommentOnly).ShouldBeTrue();
        filter.Include(Enabled).ShouldBeTrue();
    }

    [TestMethod]
    public void Include_TextFilter_IsReadPerCall_AndMatchesCanonicalPredicate()
    {
        var text = "localhost";
        var filter = new HostsFilter(() => text);

        filter.Include(Enabled).ShouldBeTrue();
        filter.Include(Disabled).ShouldBeFalse();

        // The provider is read on each Include call, so a changed filter takes effect without re-wiring.
        text = "disabled";
        filter.Include(Enabled).ShouldBeFalse();
        filter.Include(Disabled).ShouldBeTrue();
    }

    [TestMethod]
    public void Include_MatchesHostsEntryMatchesFilter_ForAllFlagCombinations()
    {
        foreach (var hideComments in new[] { false, true })
        {
            foreach (var hideDisabled in new[] { false, true })
            {
                var filter = new HostsFilter(() => "test") { Comments = hideComments, Disabled = hideDisabled };
                foreach (var entry in new[] { Enabled, CommentOnly, Disabled })
                {
                    filter.Include(entry).ShouldBe(
                        HostsEntry.MatchesFilter(entry, hideComments, hideDisabled, "test"));
                }
            }
        }
    }
}
