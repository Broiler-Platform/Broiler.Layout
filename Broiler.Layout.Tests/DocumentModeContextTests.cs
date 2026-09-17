using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// <see cref="DocumentModeContext.IsQuirksHtml"/> against the HTML Standard's DOCTYPE conditions
/// ("The initial insertion mode"). The public-identifier cases are the point: a doctype whose *name*
/// is <c>html</c> can still select quirks mode, and every legacy page that relies on quirks-mode
/// behaviour is written that way.
/// </summary>
/// <remarks>
/// Where Broiler.Dom.Html's tokenizer gives a different answer from the Standard, the expected answer is
/// that tokenizer's, so that the quirks flag <see cref="DocumentModeContext.IsQuirksHtml"/> computes from
/// the source agrees with the DocumentType that parser builds. Those tests are grouped last and named
/// <c>…_As_In_Broiler_Dom_Html</c>; each says what the Standard answers instead, and they change together
/// with that tokenizer: the one in the Broiler.Dom.Html release after 0.1.0-preview.2.
/// </remarks>
public sealed class DocumentModeContextTests
{
    [Theory]
    // No doctype at all, and a doctype that is not html.
    [InlineData("<html><body>x</body></html>", true)]
    [InlineData("<!DOCTYPE foo><html></html>", true)]
    // The standards-mode doctype, in the forms a page actually writes it.
    [InlineData("<!DOCTYPE html><html></html>", false)]
    [InlineData("<!doctype html>\n<html></html>", false)]
    [InlineData("<!DOCTYPE HTML>", false)]
    // about:legacy-compat is standards mode: the name is html and neither identifier is listed.
    [InlineData("""<!DOCTYPE html SYSTEM "about:legacy-compat">""", false)]
    // www.7-zip.org's doctype, and more legacy public identifiers matched by prefix: HTML 4.0, 2.0 and 3.2.
    [InlineData("""<!DOCTYPE HTML PUBLIC "-//W3C//DTD HTML 4.0 Transitional//EN">""", true)]
    [InlineData("""<!DOCTYPE HTML PUBLIC "-//W3C//DTD HTML 4.0 Frameset//EN">""", true)]
    [InlineData("""<!DOCTYPE html PUBLIC "-//IETF//DTD HTML 2.0//EN">""", true)]
    [InlineData("""<!DOCTYPE html PUBLIC "-//W3C//DTD HTML 3.2 Final//EN">""", true)]
    // Exact-match public identifiers.
    [InlineData("""<!DOCTYPE html PUBLIC "HTML">""", true)]
    [InlineData("""<!DOCTYPE html PUBLIC "-/W3C/DTD HTML 4.0 Transitional/EN">""", true)]
    // The listed system identifier selects quirks whatever the public identifier says.
    [InlineData("""<!DOCTYPE html PUBLIC "" "http://www.ibm.com/data/dtd/v11/ibmxhtml1-transitional.dtd">""", true)]
    // HTML 4.01 Transitional/Frameset: full quirks without a system identifier, limited-quirks with
    // one — and limited-quirks is not what this predicate reports.
    [InlineData("""<!DOCTYPE HTML PUBLIC "-//W3C//DTD HTML 4.01 Transitional//EN">""", true)]
    [InlineData("""<!DOCTYPE HTML PUBLIC "-//W3C//DTD HTML 4.01 Transitional//EN" "http://www.w3.org/TR/html4/loose.dtd">""", false)]
    [InlineData("""<!DOCTYPE HTML PUBLIC "-//W3C//DTD HTML 4.01 Frameset//EN" "http://www.w3.org/TR/html4/frameset.dtd">""", false)]
    // HTML 4.01 Strict is standards mode either way — it is on neither list.
    [InlineData("""<!DOCTYPE HTML PUBLIC "-//W3C//DTD HTML 4.01//EN" "http://www.w3.org/TR/html4/strict.dtd">""", false)]
    // XHTML 1.0 Transitional is limited-quirks, so not full quirks; XHTML 1.0 Strict is standards mode.
    [InlineData("""<!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.0 Transitional//EN" "http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd">""", false)]
    [InlineData("""<!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.0 Strict//EN" "http://www.w3.org/TR/xhtml1/DTD/xhtml1-strict.dtd">""", false)]
    public void Classifies(string html, bool quirks) =>
        Assert.Equal(quirks, DocumentModeContext.IsQuirksHtml(html));

    // The spec compares both identifiers ASCII case-insensitively, and the keyword and quoting style
    // vary across real documents.
    [Theory]
    [InlineData("""<!doctype html public "-//w3c//dtd html 4.0 transitional//en">""")]
    [InlineData("""<!DOCTYPE HTML PUBLIC '-//W3C//DTD HTML 4.0 Transitional//EN'>""")]
    [InlineData("""<!DoCtYpE   hTmL   pUbLiC   "-//W3C//DTD HTML 4.0 Transitional//EN"  >""")]
    public void Identifier_Matching_Is_Case_Insensitive(string html) =>
        Assert.True(DocumentModeContext.IsQuirksHtml(html));

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Empty_Input_Is_Quirks(string? html) =>
        Assert.True(DocumentModeContext.IsQuirksHtml(html!));

    [Theory]
    // Whitespace the Standard expects but recovers without: before the name, after PUBLIC or SYSTEM, and
    // between the two identifiers. The name and the identifiers are read all the same, and they decide.
    [InlineData("<!DOCTYPEhtml>", false)]
    [InlineData("<!doctypehtml><html><head></head><body>x</body></html>", false)]
    [InlineData("""<!DOCTYPEhtml PUBLIC "-//W3C//DTD HTML 4.0 Transitional//EN">""", true)]
    [InlineData("""<!DOCTYPE html PUBLIC"-//W3C//DTD HTML 4.0 Transitional//EN">""", true)]
    [InlineData("""<!DOCTYPE html SYSTEM"http://www.ibm.com/data/dtd/v11/ibmxhtml1-transitional.dtd">""", true)]
    [InlineData("""<!DOCTYPE HTML PUBLIC "-//W3C//DTD HTML 4.01 Transitional//EN""http://www.w3.org/TR/html4/loose.dtd">""", false)]
    // An identifier cut off by '>' ends there, whether or not a quote comes later in the document. The
    // Standard also sets force-quirks then (abrupt-doctype-public-identifier), which these identifiers
    // select anyway.
    [InlineData("""<!DOCTYPE html PUBLIC "HTML>""", true)]
    [InlineData("""<!DOCTYPE HTML PUBLIC "-//W3C//DTD HTML 3.2 Final//EN><p>x</p>""", true)]
    [InlineData("""<!DOCTYPE html PUBLIC "HTML><p class="x">y</p>""", true)]
    public void A_Doctype_Missing_Whitespace_Or_A_Closing_Quote_Is_Still_Read(string html, bool quirks) =>
        Assert.Equal(quirks, DocumentModeContext.IsQuirksHtml(html));

    // ────────────────────────────── the initial insertion mode ──────────────────────────────

    // Comments and ASCII whitespace keep the parser in the "initial" insertion mode (HTML §13.2.6.4.1),
    // so a DOCTYPE after them is still the document's DOCTYPE. A character reference is decoded before
    // the tree builder sees it, so &#32; is whitespace. <!x>, <?…?> and </> keep the mode too: each is a
    // bogus comment or, in one parser or the other, no token at all.
    [Theory]
    [InlineData("<!DOCTYPE html><p>x</p>")]
    [InlineData("<!-- a comment --><!DOCTYPE html>")]
    [InlineData("<!--><!DOCTYPE html>")]
    [InlineData("<!---><!DOCTYPE html>")]
    [InlineData("<!----><!DOCTYPE html>")]
    [InlineData(" \t\n\f\r<!DOCTYPE html>")]
    [InlineData("\r\n<!-- one -->\n<!-- two -->\n<!DOCTYPE html>")]
    [InlineData("""<?xml version="1.0" encoding="utf-8"?><!DOCTYPE html>""")]
    [InlineData("<!x><!DOCTYPE html>")]
    [InlineData("</><!DOCTYPE html>")]
    [InlineData("&#32;&#x9;&#10;&#X0C;&#13;<!DOCTYPE html>")]
    [InlineData("<!-- <p>not a tag</p> --><!DOCTYPE html>")]
    public void A_Doctype_After_Comments_And_Ascii_Whitespace_Selects_Standards_Mode(string html) =>
        Assert.False(DocumentModeContext.IsQuirksHtml(html));

    // Any other token sets quirks mode and ends the initial insertion mode; a DOCTYPE after it is ignored.
    [Theory]
    // Text: any character that is not ASCII whitespace, including a '<' that starts no tag, a no-break
    // space written as the character or as a reference, U+000B, and a U+FEFF left in a decoded string.
    [InlineData("x<!DOCTYPE html>")]
    [InlineData("<!-- c -->x<!DOCTYPE html>")]
    [InlineData("< <!DOCTYPE html>")]
    [InlineData("\u00A0<!DOCTYPE html>")]
    [InlineData("&nbsp;<!DOCTYPE html>")]
    [InlineData("&#160;<!DOCTYPE html>")]
    [InlineData("\uFEFF<!DOCTYPE html>")]
    [InlineData("\v<!DOCTYPE html>")]
    // A reference to a character outside the BMP is text, even when the low 16 bits of its code point
    // spell SPACE (U+10020) or TAB (U+10009).
    [InlineData("&#x10020;<!DOCTYPE html>")]
    [InlineData("&#65545;<!DOCTYPE html>")]
    // A start or end tag.
    [InlineData("""<meta charset="utf-8"><!DOCTYPE html>""")]
    [InlineData("<html><!DOCTYPE html>")]
    [InlineData("</p><!DOCTYPE html>")]
    public void A_Doctype_After_Any_Other_Token_Is_Ignored(string html) =>
        Assert.True(DocumentModeContext.IsQuirksHtml(html));

    // A DOCTYPE spelled inside a comment, or after a comment that has not ended, is comment text and
    // declares nothing.
    [Theory]
    [InlineData("<!-- <!DOCTYPE html> --><html></html>")]
    [InlineData("<!-- unterminated <!DOCTYPE html>")]
    public void A_Doctype_Inside_A_Comment_Declares_Nothing(string html) =>
        Assert.True(DocumentModeContext.IsQuirksHtml(html));

    [Theory]
    // After comments and whitespace the DOCTYPE's name and identifiers still decide: a name other than
    // html and a legacy public identifier are quirks mode, HTML 4.01 Transitional with a system
    // identifier limited-quirks.
    [InlineData("<!-- c --><!DOCTYPE svg>", true)]
    [InlineData("\n<!DOCTYPE math PUBLIC \"-//W3C//DTD MathML 2.0//EN\">", true)]
    [InlineData("""<!-- c --><!DOCTYPE HTML PUBLIC "-//W3C//DTD HTML 4.0 Transitional//EN">""", true)]
    [InlineData("""<?xml?>  <!DOCTYPE HTML PUBLIC "-//W3C//DTD HTML 4.01 Transitional//EN" "http://www.w3.org/TR/html4/loose.dtd">""", false)]
    // A DOCTYPE right after another is ignored, whichever way it would decide; one with no name decides
    // quirks mode like any name other than html.
    [InlineData("<!DOCTYPE html><!DOCTYPE foo>", false)]
    [InlineData("<!DOCTYPE foo><!DOCTYPE html>", true)]
    [InlineData("<!DOCTYPE><!DOCTYPE html>", true)]
    [InlineData("<!DOCTYPE ><!DOCTYPE html>", true)]
    public void The_Doctype_That_Ends_The_Initial_Insertion_Mode_Keeps_Its_Own_Conditions(string html, bool quirks) =>
        Assert.Equal(quirks, DocumentModeContext.IsQuirksHtml(html));

    // ───────────── where Broiler.Dom.Html's tokenizer departs from the Standard ─────────────

    // The Standard closes a comment at "--!>" (comment end bang state, incorrectly-closed-comment), so
    // the DOCTYPE after it would count. Broiler.Dom.Html's tokenizer has no comment end bang state and
    // reads the rest of this input as comment text.
    [Fact(Timeout = 600000)]
    public void A_Comment_Closed_With_Dash_Dash_Bang_Is_Still_Open_As_In_Broiler_Dom_Html() =>
        Assert.True(DocumentModeContext.IsQuirksHtml("<!-- x --!><!DOCTYPE html>"));

    // The Standard's end tag open state wants an ASCII alpha, so "</é>" is a bogus comment and the DOCTYPE
    // after it would count. Broiler.Dom.Html's tokenizer takes any letter (char.IsLetter) and reads an end
    // tag, which ends the initial insertion mode.
    [Fact(Timeout = 600000)]
    public void A_Non_Ascii_Letter_Starts_An_End_Tag_As_In_Broiler_Dom_Html() =>
        Assert.True(DocumentModeContext.IsQuirksHtml("</\u00E9><!DOCTYPE html>"));

    // The Standard decodes each of these to whitespace, so the DOCTYPE after it would count: a numeric
    // reference with no semicolon (missing-semicolon-after-character-reference), &Tab; and &NewLine;.
    // Broiler.Dom.Html's tokenizer decodes only references WebUtility.HtmlDecode knows, terminated by ';',
    // and leaves these as text.
    [Theory]
    [InlineData("&#32<!DOCTYPE html>")]
    [InlineData("&Tab;<!DOCTYPE html>")]
    [InlineData("&NewLine;<!DOCTYPE html>")]
    public void A_Whitespace_Reference_Left_Undecoded_Is_Text_As_In_Broiler_Dom_Html(string html) =>
        Assert.True(DocumentModeContext.IsQuirksHtml(html));

    // Here the Standard is stricter: its numeric reference is the digits right after "&#" or "&#x", so
    // each of these leaves text and the DOCTYPE after it is ignored. WebUtility.HtmlDecode, which
    // Broiler.Dom.Html's tokenizer decodes with, parses a decimal reference as a number, accepting a sign,
    // whitespace around the digits and NULs after them (NULs after hex digits too), so each is a space.
    [Theory]
    [InlineData("&#+32;<!DOCTYPE html>")]
    [InlineData("&# 32;<!DOCTYPE html>")]
    [InlineData("&#\t32\n;<!DOCTYPE html>")]
    [InlineData("&#32\0;<!DOCTYPE html>")]
    [InlineData("&#x20\0;<!DOCTYPE html>")]
    public void A_Numeric_Reference_Decodes_As_Leniently_As_In_Broiler_Dom_Html(string html) =>
        Assert.False(DocumentModeContext.IsQuirksHtml(html));

    // U+00A0 is not whitespace to the Standard's DOCTYPE states, so the name there is U+00A0 followed by
    // html, which is quirks mode. Broiler.Dom.Html's tokenizer skips char.IsWhiteSpace in its DOCTYPE
    // states and reads the name html.
    [Fact(Timeout = 600000)]
    public void A_No_Break_Space_Before_The_Doctype_Name_Is_Whitespace_As_In_Broiler_Dom_Html() =>
        Assert.False(DocumentModeContext.IsQuirksHtml("<!DOCTYPE\u00A0html>"));

    // The Standard sets the force-quirks flag on each of these DOCTYPEs, which puts them in quirks mode:
    // end of input inside it, PUBLIC with no identifier, an identifier cut off by '>', other text after
    // the name. Broiler.Dom.Html's tokenizer has no force-quirks flag, so each is a DOCTYPE named html
    // with no quirks identifier.
    [Theory]
    [InlineData("<!DOCTYPE html")]
    [InlineData("<!DOCTYPE html PUBLIC>")]
    [InlineData("""<!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.0 Strict//EN>""")]
    [InlineData("<!DOCTYPE html bogus>")]
    public void A_Doctype_Has_No_Force_Quirks_Flag_As_In_Broiler_Dom_Html(string html) =>
        Assert.False(DocumentModeContext.IsQuirksHtml(html));
}
