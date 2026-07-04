namespace HostsFileEditor.Core.Tests;

// Asserts the allocation-light span parser produces byte-for-byte the same result as the original
// regex parser, across a large corpus of realistic and adversarial lines. If these ever diverge,
// the span parser must not ship.
[TestClass]
public class HostsEntrySpanParserDifferentialTests
{
    private static readonly string[] EdgeCases =
    [
        // blanks / whitespace
        "", " ", "   ", "\t", " \t ", "\f", "\v",
        // simple entries
        "127.0.0.1 localhost",
        "127.0.0.1 localhost # comment",
        "0.0.0.0 ads.example.com",
        "  0.0.0.0 ads.example.com  ",
        "\t10.0.0.1\thost.example.com\t",
        // disabled entries
        "# 127.0.0.1 localhost",
        "#127.0.0.1 localhost",
        "#  127.0.0.1  localhost  # trailing",
        "## 127.0.0.1 localhost",
        // multiple hostnames / double spaces
        "1.2.3.4 host1.com host2.com host3",
        "1.2.3.4 host1  host2   host3",
        "1.2.3.4 a.b.c.d.e.f",
        // comments
        "# comment", "#comment", "#  two-space", "#   three-space comment",
        "## double hash", "#", "##", "#   ", "# ",
        "   # indented comment",
        "\t#\ttabbed",
        // invalid ip -> demoted to comment
        "notanip host.com",
        "# notanip host.com",
        "999.999.999.999 host.example.com",
        "# 1.2.3.4 bad/host",
        "1.2.3.4 bad/host",
        // hash inside content
        "1.2.3.4 #immediatehash",
        "1.2.3.4 host # a # b # c",
        "1.2.3.4 ho#st",
        "1.2.3.4#5 host",
        // single token / no hostname
        "1.2.3.4", "just-one-token", "1.2.3.4   ",
        // unmatched / odd
        "1.2.3.4 host/path?query=1",
        "text with spaces but no structure!",
        "192.168.1.1:8080 host",
        // ipv6
        "::1 localhost",
        "fe80::1 host.example.com",
        "2001:db8::1 host # v6",
        "# ::1 localhost",
        // unicode
        "1.2.3.4 höst.example.com",       // unicode letter in hostname (\w matches it)
        "1.2.3.4 host name.com",          // non-breaking space (whitespace nuance)
        "1.2.3.4　host",                   // ideographic space
        // trailing/leading oddities
        " ", "\r", "1.2.3.4 host\r", "1.2.3.4 host ",
        "#comment ends with spaces   ",
    ];

    private static IEnumerable<string> GeneratedCorpus()
    {
        for (var i = 0; i < 3000; i++)
        {
            yield return (i % 11) switch
            {
                0 => $"0.0.0.0 adserver{i}.tracking.example.com # blocked {i}",
                1 => $"127.0.0.1 host{i}.local",
                2 => $"# {i % 3}.{i % 5}.{i % 7}.{i % 9} disabled{i}.example.net",
                3 => $"192.168.1.{i % 255} node{i}.corp.example.com app{i}.example.com",
                4 => $"# just a comment number {i} with words",
                5 => $"   {i}.{i}.{i}.{i}   spaced{i}.example.com   ",
                6 => $"10.0.0.{i % 255} a{i}  b{i}   c{i} # notes",
                7 => $"badip{i} host{i}.example.com",
                8 => $"# {i}",
                9 => $"1.2.3.{i % 255} host{i}.com/path{i}",
                _ => (i % 2 == 0 ? "" : "   "),
            };
        }
    }

    // Deterministic random lines from a charset chosen to stress the parser (hashes, spaces, tabs,
    // dots, slashes, colons, digits, letters) — catches structural edge cases the curated corpus
    // might miss.
#pragma warning disable CA5394 // deterministic, seeded pseudo-random test fuzzing — not security
    private static IEnumerable<string> FuzzCorpus()
    {
        const string charset = "abc01. -#\t/:_";
        var rng = new Random(12345);
        for (var i = 0; i < 20000; i++)
        {
            var len = rng.Next(0, 24);
            var chars = new char[len];
            for (var j = 0; j < len; j++)
            {
                chars[j] = charset[rng.Next(charset.Length)];
            }

            yield return new string(chars);
        }
    }
#pragma warning restore CA5394

    [TestMethod]
    public void SpanParser_MatchesRegexParser_ForEntireCorpus()
    {
        var failures = new List<string>();
        var chec2 = 0;

        foreach (var line in EdgeCases.Concat(GeneratedCorpus()).Concat(FuzzCorpus()).Concat(HostsEntryList.DefaultLines))
        {
            chec2++;
            var r = HostsEntry.ParseViaRegexForTest(line);
            var s = HostsEntry.ParseViaSpanForTest(line);

            if (r.Valid != s.Valid ||
                r.Enabled != s.Enabled ||
                r.IpAddress != s.IpAddress ||
                r.HostNames != s.HostNames ||
                r.Comment != s.Comment ||
                r.HasCommentOnly != s.HasCommentOnly ||
                r.UnparsedText != s.UnparsedText)
            {
                failures.Add(
                    $"LINE [{line}]\n" +
                    $"  regex: valid={r.Valid} en={r.Enabled} ip=[{r.IpAddress}] host=[{r.HostNames}] cmt=[{r.Comment}] commentOnly={r.HasCommentOnly} unparsed=[{r.UnparsedText}]\n" +
                    $"  span : valid={s.Valid} en={s.Enabled} ip=[{s.IpAddress}] host=[{s.HostNames}] cmt=[{s.Comment}] commentOnly={s.HasCommentOnly} unparsed=[{s.UnparsedText}]");
            }
        }

        failures.ShouldBeEmpty($"checked {chec2} lines; {failures.Count} diverged:\n" + string.Join("\n", failures.Take(20)));
    }
}
