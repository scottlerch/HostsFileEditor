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
        // FQDN trailing dot (issue #65 / PR #66): Tailscale MagicDNS writes a root '.', and each
        // hostname label may now carry one optional trailing dot. The span tokenizer captures the
        // dot (it stops only at whitespace / '#') and defers validity to HostNameRegex, so the two
        // engines must agree — including on the adversarial double-dot which neither accepts.
        "100.64.0.1 host.tailnet.ts.net.",
        "100.64.0.1 host.tailnet.ts.net. # magicdns",
        "# 100.64.0.1 host.tailnet.ts.net.",
        "1.2.3.4 host.",
        "1.2.3.4 a. b. c.",
        "1.2.3.4 host.. doubledot",
        "fe80::1 host.example.com.",
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
        // ASCII-position fuzz. The char.IsWhiteSpace-vs-regex-\s boundary (nbsp, ideographic space,
        // NEL, vtab, form feed, \r) is covered explicitly by WhitespaceBoundaryCases below, placed
        // in the disabled-marker / IP-token positions where a mismatch would actually matter.
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

    // Whitespace chars where char.IsWhiteSpace (span parser) and regex \s can disagree, placed in
    // the token positions (leading, IP/host separator, disabled marker) where it would matter. Built
    // from char codes because a literal NEL/line-separator in a string constant is a compile error.
    private static string[] WhitespaceBoundaryCases()
    {
        char nbsp = (char)0x00A0, ideo = (char)0x3000, nel = (char)0x0085, vt = (char)0x000B, ff = (char)0x000C;
        return
        [
            $"1.2.3.4{nbsp}host",
            $"1.2.3.4{ideo}host",
            $"1.2.3.4{nel}host",
            $"1.2.3.4 host{vt}comment",
            $"1.2.3.4 host{ff}comment",
            $"{nbsp}127.0.0.1 localhost",
            $"{vt}# 1.2.3.4 disabled",
            $"#{nbsp}1.2.3.4 host",
            $"1.2.3.4{nbsp}{ideo}host",
            "1.2.3.4 host\r",
            // Embedded CR/LF can't come from the line-splitting load path, but the public
            // HostsEntry(string) ctor accepts them and the engines natively disagree (regex `.`/`$`
            // stop at \n; the span scanner treats it as whitespace) — the span side delegates these
            // to the regex engine, and these cases pin that.
            "1.2.3.4 host #c\nd",
            "1.2.3.4 host\n",
            "# comment\n",
            "\n",
            "\r\n",
            "1.2.3.4\rhost",
            "a\nb",
        ];
    }

    [TestMethod]
    public void SpanParser_MatchesRegexParser_ForEntireCorpus()
    {
        var failures = new List<string>();
        var chec2 = 0;

        foreach (var line in EdgeCases.Concat(WhitespaceBoundaryCases()).Concat(GeneratedCorpus()).Concat(FuzzCorpus()).Concat(HostsEntryList.DefaultLines))
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

    // The corpus test compares UnparsedText at PARSE time, where it equals the input for both engines,
    // so it cannot catch a re-serialization bug. Exercise the edit -> re-serialize -> round-trip path
    // that only runs once a property setter invalidates the cached text.
    [TestMethod]
    public void EditingAField_ReSerializesUnparsedTextAndRoundTrips()
    {
        var ip = new HostsEntry("1.2.3.4 localhost # note");
        ip.IpAddress = "5.6.7.8";
        ip.UnparsedText.ShouldContain("5.6.7.8");
        new HostsEntry(ip.UnparsedText).IpAddress.ShouldBe("5.6.7.8");

        var host = new HostsEntry("1.2.3.4 localhost");
        host.HostNames = "renamed.example.com";
        host.UnparsedText.ShouldContain("renamed.example.com");
        new HostsEntry(host.UnparsedText).HostNames.ShouldBe("renamed.example.com");

        var disabled = new HostsEntry("1.2.3.4 localhost");
        disabled.Enabled = false;
        disabled.UnparsedText.TrimStart().ShouldStartWith("#");
        new HostsEntry(disabled.UnparsedText).Enabled.ShouldBeFalse();
    }
}
