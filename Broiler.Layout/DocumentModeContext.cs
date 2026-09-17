using System;
using System.Net;

namespace Broiler.Layout;

/// <summary>
/// Per-render (thread-local) document-mode state, mirroring the way
/// <c>Broiler.CSS.CssLengthParser</c> exposes the current viewport size for the
/// duration of a render. The host publishes the document's quirks-mode flag here
/// when it parses the page, so layout — which on the HTML-string parse path holds
/// no reference back to the source document — can consult it while sizing the
/// root and body boxes (the quirks-mode body/html fill-viewport behaviour,
/// https://quirks.spec.whatwg.org/).
///
/// Layout runs on the same thread immediately after the parse that set this, and
/// each parse overwrites it, so a stale <c>true</c> never leaks into a later
/// standards-mode render. <c>CssBox</c> additionally caches the value on the tree
/// root the first time it reads it, so it survives a re-layout pass that may run
/// on a different thread.
/// </summary>
public static class DocumentModeContext
{
    [ThreadStatic]
    private static bool _quirksMode;

    /// <summary>The quirks-mode flag of the document currently being laid out.</summary>
    /// <remarks>
    /// Both render paths publish this on every parse: the HtmlBridge DOM path at
    /// <c>DomBridge.HtmlParsing.cs</c>, and — since Phase 2 item #9 — the HTML-string path at
    /// <c>HtmlContainerInt.SetHtmlWithStyleSet</c>. Until the second of those existed the flag was
    /// simply never written on the string path, which was harmless only because one thread rendered
    /// one document at a time; a pooled thread arriving from a quirks-mode DOM render would have
    /// carried that <c>true</c> into a standards-mode string render. <see cref="AmbientRenderState"/>
    /// is the check that would have caught it, and now has nothing to catch here.
    /// </remarks>
    public static bool CurrentQuirksMode
    {
        get
        {
            AmbientRenderState.AssertEstablished(
                AmbientRenderState.Slots.DocumentMode, nameof(CurrentQuirksMode));
            return _quirksMode;
        }

        set
        {
            _quirksMode = value;
            AmbientRenderState.MarkEstablished(AmbientRenderState.Slots.DocumentMode);
        }
    }

    /// <summary>
    /// Quirks-mode determination from raw HTML, following the HTML Standard's DOCTYPE conditions.
    /// A document with no doctype, or one whose name is not <c>html</c>, is in quirks mode; a bare
    /// <c>&lt;!DOCTYPE html&gt;</c> selects standards mode; and a name of <c>html</c> carrying one
    /// of the legacy public identifiers selects quirks mode too.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The public identifier half is not a refinement anyone can skip: the doctypes real legacy
    /// pages carry are exactly the ones it recognises. www.7-zip.org declares
    /// <c>&lt;!DOCTYPE HTML PUBLIC "-//W3C//DTD HTML 4.0 Transitional//EN"&gt;</c>, whose name *is*
    /// <c>html</c> — so on a name-only test it read as standards mode, and every quirks-mode
    /// behaviour keyed off this flag (the body/html fill, both table quirks) was silently inert on
    /// the pages that need them most.
    /// </para>
    /// <para>
    /// This reads the doctype out of the markup rather than from a parsed tree because both callers
    /// have only the string: the flag is published before the document is built.
    /// </para>
    /// <para>
    /// <b>Only a DOCTYPE the parser would keep counts.</b> The HTML Standard's "initial" insertion
    /// mode (§13.2.6.4.1) takes a DOCTYPE only while everything before it has been comments and ASCII
    /// whitespace; any other token — text, a tag, even a DOCTYPE with no name — leaves the mode in
    /// quirks, and a DOCTYPE after that is a parse error that is ignored. This used to take the first
    /// <c>&lt;!doctype</c> anywhere in the source, so a page with a stray character or a
    /// <c>&lt;meta&gt;</c> ahead of its DOCTYPE read as standards mode here.
    /// </para>
    /// <para>
    /// The rule has to match the parser that builds the tree, because a host can read the mode twice:
    /// from the source through this predicate, and again from the tree once it is serialized, where a
    /// DocumentType is written as a DOCTYPE. Broiler.Dom.Html's tree builder applies the same rule
    /// from the release after 0.1.0-preview.2 on; preview.2 itself still keeps a late DOCTYPE, so
    /// against it such a page is quirks mode here and standards mode from the serialized tree. A
    /// consumer takes this rule and that parser in the same update.
    /// </para>
    /// <para>
    /// For the same reason the answer follows Broiler.Dom.Html's tokenizer rather than the Standard
    /// wherever the two differ: <see cref="FindInitialModeDoctype"/> walks the leading tokens the way
    /// that tokenizer reads them, and <see cref="ReadDoctype"/> reads the DOCTYPE it finds the way that
    /// tokenizer does. Each lists the departures it carries over, and the tests named
    /// <c>…_As_In_Broiler_Dom_Html</c> pin them, so both sides change together.
    /// </para>
    /// </remarks>
    public static bool IsQuirksHtml(string html)
    {
        if (string.IsNullOrEmpty(html))
            return true;

        var doctypeStart = FindInitialModeDoctype(html);
        if (doctypeStart < 0)
            return true;

        ReadDoctype(html, doctypeStart + "<!DOCTYPE".Length, out var name, out var publicId, out var systemId);
        if (!name.Equals("html", StringComparison.OrdinalIgnoreCase))
            return true;

        return SelectsQuirksMode(publicId, systemId);
    }

    /// <summary>
    /// The HTML Standard's quirks-mode conditions for a DOCTYPE whose name is <c>html</c>
    /// (<see href="https://html.spec.whatwg.org/multipage/parsing.html#the-initial-insertion-mode">"The
    /// initial insertion mode"</see>). Both identifiers are compared ASCII case-insensitively, and an
    /// empty system identifier counts as missing, as the spec requires.
    /// </summary>
    /// <remarks>
    /// Limited-quirks mode is deliberately not modelled: this predicate answers "full quirks?", and
    /// the two limited-quirks families (XHTML 1.0 Transitional/Frameset, and HTML 4.01
    /// Transitional/Frameset <em>with</em> a system identifier) fall through to <c>false</c> — which
    /// is what the callers, all of them full-quirks behaviours, need.
    /// </remarks>
    private static bool SelectsQuirksMode(string publicId, string systemId)
    {
        foreach (var exact in QuirksPublicIdentifiers)
            if (publicId.Equals(exact, StringComparison.OrdinalIgnoreCase))
                return true;

        if (systemId.Equals(
                "http://www.ibm.com/data/dtd/v11/ibmxhtml1-transitional.dtd",
                StringComparison.OrdinalIgnoreCase))
            return true;

        foreach (var prefix in QuirksPublicIdentifierPrefixes)
            if (publicId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;

        // HTML 4.01 Transitional/Frameset are full quirks only without a system identifier; with one
        // they are limited-quirks, which is not this predicate's answer.
        if (systemId.Length == 0)
            foreach (var prefix in QuirksPublicIdentifierPrefixesWithoutSystemId)
                if (publicId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return true;

        return false;
    }

    /// <summary>
    /// The index of the <c>&lt;!DOCTYPE</c> that ends the "initial" insertion mode, or <c>-1</c> when
    /// some other token ends it first (or the input ends before any token does).
    /// </summary>
    /// <remarks>
    /// <para>
    /// A hand-written walk over the leading tokens only — Broiler.Layout does not reference
    /// Broiler.Dom.Html, and a page's head is never read past its first real token. What keeps the mode
    /// is what Broiler.Dom.Html's tokenizer turns into a comment or into whitespace-only text:
    /// </para>
    /// <list type="bullet">
    /// <item><description>ASCII whitespace (TAB, LF, FF, CR, SPACE), and a character reference that
    /// decodes to one of them (<c>&amp;#32;</c>): references are decoded before the tree builder sees
    /// the text, with the tokenizer's own decoder (<see cref="TrySkipWhitespaceReference"/>). Any
    /// other character, a U+00A0 or a leaked U+FEFF included, is text and ends the
    /// mode.</description></item>
    /// <item><description>A comment, closed at <c>--&gt;</c>, or abruptly at <c>&lt;!--&gt;</c> and
    /// <c>&lt;!---&gt;</c>.</description></item>
    /// <item><description>A bogus comment: <c>&lt;!</c> not followed by <c>--</c> or <c>DOCTYPE</c>,
    /// <c>&lt;/</c> not followed by a letter, each up to the next <c>&gt;</c>; and <c>&lt;?…&gt;</c>,
    /// which that tokenizer skips without a token at all.</description></item>
    /// </list>
    /// <para>
    /// A start or end tag (<c>&lt;</c> or <c>&lt;/</c> followed by a letter) ends the mode, as does a
    /// <c>&lt;</c> that starts nothing, since it is text.
    /// </para>
    /// <para>
    /// Where that tokenizer departs from the Standard, this follows it. <c>--!&gt;</c> does not close
    /// a comment (no comment end bang state). Letters are <see cref="char.IsLetter(char)"/>, as in its
    /// tag-open states, where the Standard says ASCII alpha, so a non-ASCII letter after <c>&lt;/</c>
    /// starts an end tag. A reference is decoded only when it ends in <c>;</c> and the decoder knows
    /// it, so <c>&amp;#32</c> and <c>&amp;Tab;</c> are text. And the decoder parses a decimal
    /// reference as a number, so <c>&amp;#+32;</c> and <c>&amp;# 32;</c> are a space, and so is
    /// <c>&amp;#32;</c> with NULs before its <c>;</c>. The Standard reads each of these the other way.
    /// </para>
    /// </remarks>
    private static int FindInitialModeDoctype(string html)
    {
        var pos = 0;
        while (pos < html.Length)
        {
            var c = html[pos];
            if (IsAsciiWhitespace(c))
            {
                pos++;
                continue;
            }

            if (c == '&')
            {
                if (!TrySkipWhitespaceReference(html, ref pos))
                    return -1;
                continue;
            }

            if (c != '<' || pos + 1 >= html.Length)
                return -1;

            var next = html[pos + 1];
            if (next == '!')
            {
                if (string.CompareOrdinal(html, pos + 2, "--", 0, 2) == 0)
                {
                    pos = SkipComment(html, pos + 4);
                    continue;
                }

                if (string.Compare(html, pos + 2, "DOCTYPE", 0, 7, StringComparison.OrdinalIgnoreCase) == 0)
                    return pos;

                pos = SkipToGreaterThan(html, pos + 2);
                continue;
            }

            if (next == '?')
            {
                pos = SkipToGreaterThan(html, pos + 2);
                continue;
            }

            if (next == '/')
            {
                if (pos + 2 >= html.Length || char.IsLetter(html[pos + 2]))
                    return -1;

                pos = SkipToGreaterThan(html, pos + 2);
                continue;
            }

            // A start tag, or a '<' that opens nothing and is therefore text: either ends the mode.
            return -1;
        }

        return -1;
    }

    private static bool IsAsciiWhitespace(char c) => c is '\t' or '\n' or '\f' or '\r' or ' ';

    /// <summary>
    /// Advances past the character reference at <paramref name="pos"/> when Broiler.Dom.Html's
    /// tokenizer decodes it to a single ASCII whitespace character. Anything else starting with
    /// <c>&amp;</c> is text, and the answer is <c>false</c>: a reference to any other character (one
    /// outside the BMP included), a reference the decoder leaves as it is, or an ampersand that starts
    /// no reference.
    /// </summary>
    /// <remarks>
    /// That tokenizer hands each text run, which ends at the next <c>&lt;</c>, to
    /// <see cref="WebUtility.HtmlDecode(string)"/>. The decoder takes a reference to run from the
    /// <c>&amp;</c> to the next <c>;</c>, unless another <c>&amp;</c> comes first, and leaves that text
    /// as it is when it does not decode. The same text goes to the same decoder here rather than to a
    /// hand-written parser, so that what counts as whitespace is what the decoder reads, including its
    /// departures from the Standard (see <see cref="FindInitialModeDoctype"/>).
    /// </remarks>
    private static bool TrySkipWhitespaceReference(string html, ref int pos)
    {
        var length = html.AsSpan(pos + 1).IndexOfAny(';', '&', '<');
        if (length < 0 || html[pos + 1 + length] != ';')
            return false;

        var end = pos + 1 + length + 1;
        var decoded = WebUtility.HtmlDecode(html[pos..end]);
        if (decoded.Length != 1 || !IsAsciiWhitespace(decoded[0]))
            return false;

        pos = end;
        return true;
    }

    /// <summary>
    /// The index just past a comment whose <c>&lt;!--</c> ended right before <paramref name="pos"/>:
    /// after its <c>--&gt;</c>, after the <c>&gt;</c> of an abruptly closed <c>&lt;!--&gt;</c> or
    /// <c>&lt;!---&gt;</c>, or the end of the input for a comment that never closes.
    /// </summary>
    private static int SkipComment(string html, int pos)
    {
        if (pos < html.Length && html[pos] == '>')
            return pos + 1;
        if (pos + 1 < html.Length && html[pos] == '-' && html[pos + 1] == '>')
            return pos + 2;

        var end = html.IndexOf("-->", pos, StringComparison.Ordinal);
        return end < 0 ? html.Length : end + 3;
    }

    /// <summary>The index just past the next <c>&gt;</c> at or after <paramref name="pos"/>, or the end
    /// of the input when there is none.</summary>
    private static int SkipToGreaterThan(string html, int pos)
    {
        var end = html.IndexOf('>', pos);
        return end < 0 ? html.Length : end + 1;
    }

    /// <summary>
    /// Reads the DOCTYPE whose <c>&lt;!DOCTYPE</c> ends right before <paramref name="pos"/>: its name,
    /// empty when it has none, and its public and system identifiers, each empty when missing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This mirrors Broiler.Dom.Html's tokenizer (its DOCTYPE state and <c>ReadDoctype</c>), so the
    /// name and identifiers are the ones the DocumentType gets. As in the Standard, the whitespace
    /// before the name, after <c>PUBLIC</c> or <c>SYSTEM</c> and between the two identifiers may be
    /// missing (<c>&lt;!DOCTYPEhtml&gt;</c>, <c>PUBLIC"…"</c>), a parse error that changes nothing
    /// else, and an identifier with no closing quote ends at <c>&gt;</c>.
    /// </para>
    /// <para>
    /// Where that tokenizer departs from the Standard, this follows it. Whitespace is
    /// <see cref="char.IsWhiteSpace(char)"/>, U+00A0 included. And there is no force-quirks flag, so a
    /// DOCTYPE the Standard puts in quirks mode for being malformed is decided by its name and
    /// identifiers alone: one cut off by the end of the input, <c>PUBLIC</c> or <c>SYSTEM</c> with no
    /// identifier, an identifier cut off by <c>&gt;</c>, other text after the name, and the like.
    /// </para>
    /// </remarks>
    private static void ReadDoctype(string html, int pos, out string name, out string publicId, out string systemId)
    {
        publicId = string.Empty;
        systemId = string.Empty;

        pos = SkipWhiteSpace(html, pos);
        var nameStart = pos;
        while (pos < html.Length && html[pos] != '>' && !char.IsWhiteSpace(html[pos]))
            pos++;
        name = html[nameStart..pos];

        // "PUBLIC <public id> [<system id>]" versus "SYSTEM <system id>": the first quoted string
        // means different things in the two forms.
        pos = SkipWhiteSpace(html, pos);
        if (IsKeywordAt(html, pos, "PUBLIC"))
        {
            pos = SkipWhiteSpace(html, pos + "PUBLIC".Length);
            publicId = ReadQuotedIdentifier(html, ref pos);
            pos = SkipWhiteSpace(html, pos);
            systemId = ReadQuotedIdentifier(html, ref pos);
        }
        else if (IsKeywordAt(html, pos, "SYSTEM"))
        {
            pos = SkipWhiteSpace(html, pos + "SYSTEM".Length);
            systemId = ReadQuotedIdentifier(html, ref pos);
        }
    }

    /// <summary>
    /// The quoted DOCTYPE identifier at <paramref name="pos"/> without its quotes, or an empty string
    /// when no quote starts there. It ends at its closing quote, which is consumed, or at a
    /// <c>&gt;</c> that cuts it off, which is not.
    /// </summary>
    private static string ReadQuotedIdentifier(string html, ref int pos)
    {
        if (pos >= html.Length || html[pos] is not ('"' or '\''))
            return string.Empty;

        var quote = html[pos++];
        var start = pos;
        while (pos < html.Length && html[pos] != quote && html[pos] != '>')
            pos++;

        var identifier = html[start..pos];
        if (pos < html.Length && html[pos] == quote)
            pos++;
        return identifier;
    }

    private static int SkipWhiteSpace(string html, int pos)
    {
        while (pos < html.Length && char.IsWhiteSpace(html[pos]))
            pos++;
        return pos;
    }

    private static bool IsKeywordAt(string html, int pos, string keyword) =>
        pos + keyword.Length <= html.Length &&
        string.Compare(html, pos, keyword, 0, keyword.Length, StringComparison.OrdinalIgnoreCase) == 0;

    /// <summary>Public identifiers that select quirks mode by exact match.</summary>
    private static readonly string[] QuirksPublicIdentifiers =
    [
        "-//W3O//DTD W3 HTML Strict 3.0//EN//",
        "-/W3C/DTD HTML 4.0 Transitional/EN",
        "HTML",
    ];

    /// <summary>Public-identifier prefixes that select quirks mode regardless of system identifier.</summary>
    private static readonly string[] QuirksPublicIdentifierPrefixes =
    [
        "+//Silmaril//dtd html Pro v0r11 19970101//",
        "-//AS//DTD HTML 3.0 asWedit + extensions//",
        "-//AdvaSoft Ltd//DTD HTML 3.0 asWedit + extensions//",
        "-//IETF//DTD HTML 2.0 Level 1//",
        "-//IETF//DTD HTML 2.0 Level 2//",
        "-//IETF//DTD HTML 2.0 Strict Level 1//",
        "-//IETF//DTD HTML 2.0 Strict Level 2//",
        "-//IETF//DTD HTML 2.0 Strict//",
        "-//IETF//DTD HTML 2.0//",
        "-//IETF//DTD HTML 2.1E//",
        "-//IETF//DTD HTML 3.0//",
        "-//IETF//DTD HTML 3.2 Final//",
        "-//IETF//DTD HTML 3.2//",
        "-//IETF//DTD HTML 3//",
        "-//IETF//DTD HTML Level 0//",
        "-//IETF//DTD HTML Level 1//",
        "-//IETF//DTD HTML Level 2//",
        "-//IETF//DTD HTML Level 3//",
        "-//IETF//DTD HTML Strict Level 0//",
        "-//IETF//DTD HTML Strict Level 1//",
        "-//IETF//DTD HTML Strict Level 2//",
        "-//IETF//DTD HTML Strict Level 3//",
        "-//IETF//DTD HTML Strict//",
        "-//IETF//DTD HTML//",
        "-//Metrius//DTD Metrius Presentational//",
        "-//Microsoft//DTD Internet Explorer 2.0 HTML Strict//",
        "-//Microsoft//DTD Internet Explorer 2.0 HTML//",
        "-//Microsoft//DTD Internet Explorer 2.0 Tables//",
        "-//Microsoft//DTD Internet Explorer 3.0 HTML Strict//",
        "-//Microsoft//DTD Internet Explorer 3.0 HTML//",
        "-//Microsoft//DTD Internet Explorer 3.0 Tables//",
        "-//Netscape Comm. Corp.//DTD HTML//",
        "-//Netscape Comm. Corp.//DTD Strict HTML//",
        "-//O'Reilly and Associates//DTD HTML 2.0//",
        "-//O'Reilly and Associates//DTD HTML Extended 1.0//",
        "-//O'Reilly and Associates//DTD HTML Extended Relaxed 1.0//",
        "-//SQ//DTD HTML 2.0 HoTMetaL + extensions//",
        "-//SoftQuad Software//DTD HoTMetaL PRO 6.0::19990601::extensions to HTML 4.0//",
        "-//SoftQuad//DTD HoTMetaL PRO 4.0::19971010::extensions to HTML 4.0//",
        "-//Spyglass//DTD HTML 2.0 Extended//",
        "-//Sun Microsystems Corp.//DTD HotJava HTML//",
        "-//Sun Microsystems Corp.//DTD HotJava Strict HTML//",
        "-//W3C//DTD HTML 3 1995-03-24//",
        "-//W3C//DTD HTML 3.2 Draft//",
        "-//W3C//DTD HTML 3.2 Final//",
        "-//W3C//DTD HTML 3.2//",
        "-//W3C//DTD HTML 3.2S Draft//",
        "-//W3C//DTD HTML 4.0 Frameset//",
        "-//W3C//DTD HTML 4.0 Transitional//",
        "-//W3C//DTD HTML Experimental 19960712//",
        "-//W3C//DTD HTML Experimental 970421//",
        "-//W3C//DTD W3 HTML//",
        "-//W3O//DTD W3 HTML 3.0//",
        "-//WebTechs//DTD Mozilla HTML 2.0//",
        "-//WebTechs//DTD Mozilla HTML//",
    ];

    /// <summary>
    /// Public-identifier prefixes that select quirks mode only when the system identifier is missing
    /// or empty; with one present these are limited-quirks instead.
    /// </summary>
    private static readonly string[] QuirksPublicIdentifierPrefixesWithoutSystemId =
    [
        "-//W3C//DTD HTML 4.01 Frameset//",
        "-//W3C//DTD HTML 4.01 Transitional//",
    ];
}
