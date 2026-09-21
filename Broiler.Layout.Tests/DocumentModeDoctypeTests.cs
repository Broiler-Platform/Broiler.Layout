using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// <see cref="DocumentModeContext.IsQuirksDoctype"/> — the same HTML Standard conditions
/// <see cref="DocumentModeContext.IsQuirksHtml"/> applies, asked about a DOCTYPE that has already been
/// parsed into a name and two identifiers rather than about markup.
/// </summary>
/// <remarks>
/// A host reads the document mode twice: once from the source it parses, and again from the tree when
/// it serialises it back. The two have to agree, so the cases here are the ones where a name-only test
/// disagrees — a DOCTYPE named <c>html</c> that still selects quirks mode by its public identifier —
/// and the limited-quirks carve-out, where the system identifier alone flips the answer.
/// </remarks>
public sealed class DocumentModeDoctypeTests
{
    [Theory]
    // A name other than html is quirks mode whatever the identifiers say.
    [InlineData("foo", "", "", true)]
    [InlineData("HTML PUBLIC", "", "", true)]
    [InlineData("", "", "", true)]
    // The bare standards-mode doctype, in either case, and about:legacy-compat.
    [InlineData("html", "", "", false)]
    [InlineData("HTML", "", "", false)]
    [InlineData("html", "", "about:legacy-compat", false)]
    // www.7-zip.org's doctype: the name is html and the public identifier selects quirks mode. This is
    // the case a host testing doctype.Name alone gets wrong, flipping the mode on a serialisation
    // round trip.
    [InlineData("html", "-//W3C//DTD HTML 4.0 Transitional//EN", "", true)]
    // More prefix-matched legacy identifiers: HTML 4.0 Frameset, 2.0, 3.2.
    [InlineData("html", "-//W3C//DTD HTML 4.0 Frameset//EN", "", true)]
    [InlineData("html", "-//IETF//DTD HTML 2.0//EN", "", true)]
    [InlineData("html", "-//W3C//DTD HTML 3.2 Final//EN", "", true)]
    // Exact-match public identifiers.
    [InlineData("html", "HTML", "", true)]
    [InlineData("html", "-/W3C/DTD HTML 4.0 Transitional/EN", "", true)]
    [InlineData("html", "-//W3O//DTD W3 HTML Strict 3.0//EN//", "", true)]
    // The listed system identifier selects quirks mode whatever the public identifier says.
    [InlineData("html", "", "http://www.ibm.com/data/dtd/v11/ibmxhtml1-transitional.dtd", true)]
    // HTML 4.01 Strict is on neither list, and XHTML 1.0 Transitional is limited-quirks, which this
    // predicate does not report.
    [InlineData("html", "-//W3C//DTD HTML 4.01//EN", "http://www.w3.org/TR/html4/strict.dtd", false)]
    [InlineData("html", "-//W3C//DTD XHTML 1.0 Transitional//EN",
        "http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd", false)]
    public void Classifies(string name, string publicId, string systemId, bool quirks) =>
        Assert.Equal(quirks, DocumentModeContext.IsQuirksDoctype(name, publicId, systemId));

    // HTML 4.01 Transitional and Frameset are the one pair whose answer the system identifier alone
    // decides: full quirks without one, limited-quirks with one. Getting this backwards is how a
    // conforming HTML 4.01 Transitional page ends up in quirks mode.
    [Theory]
    [InlineData("-//W3C//DTD HTML 4.01 Transitional//EN")]
    [InlineData("-//W3C//DTD HTML 4.01 Frameset//EN")]
    public void Html4_01_Transitional_And_Frameset_Are_Quirks_Only_Without_A_System_Identifier(string publicId)
    {
        Assert.True(DocumentModeContext.IsQuirksDoctype("html", publicId, ""));
        Assert.True(DocumentModeContext.IsQuirksDoctype("html", publicId, null));
        Assert.False(DocumentModeContext.IsQuirksDoctype("html", publicId, "http://www.w3.org/TR/html4/loose.dtd"));
        // Any system identifier, not only the matching one: the condition is presence, not value.
        Assert.False(DocumentModeContext.IsQuirksDoctype("html", publicId, "x"));
    }

    // A missing identifier reaches this method as null from a nullable-oblivious caller just as easily
    // as it does as an empty string, and both mean the same thing to the spec conditions. A null name
    // is not a name of html, so it is quirks mode.
    [Fact(Timeout = 600000)]
    public void A_Null_Identifier_Counts_As_Missing()
    {
        Assert.False(DocumentModeContext.IsQuirksDoctype("html", null, null));
        Assert.True(DocumentModeContext.IsQuirksDoctype("html", "-//W3C//DTD HTML 4.0 Transitional//EN", null));
        Assert.True(DocumentModeContext.IsQuirksDoctype(null, null, null));
    }

    // Both identifiers and the name are compared ASCII case-insensitively, so a parser that keeps the
    // source case of <!DOCTYPE HTML PUBLIC "…"> answers the same as one that lowercases the name.
    [Theory]
    [InlineData("html", "-//w3c//dtd html 4.0 transitional//en")]
    [InlineData("HTML", "-//W3C//DTD HTML 4.0 Transitional//EN")]
    [InlineData("hTmL", "-//W3C//dtd HTML 4.0 TRANSITIONAL//EN")]
    public void Matching_Is_Case_Insensitive(string name, string publicId) =>
        Assert.True(DocumentModeContext.IsQuirksDoctype(name, publicId, ""));

    [Fact(Timeout = 600000)]
    public void The_System_Identifier_Match_Is_Case_Insensitive_Too() =>
        Assert.True(DocumentModeContext.IsQuirksDoctype(
            "html", "", "HTTP://WWW.IBM.COM/data/dtd/v11/ibmxhtml1-transitional.dtd"));

    // The two entry points share one implementation, so the answer a host computes from the source and
    // the answer it recomputes from the parsed triple must not differ. These are the triples the
    // markup on the left parses to.
    [Theory]
    [InlineData("<!DOCTYPE html>", "html", "", "")]
    [InlineData("<!DOCTYPE foo>", "foo", "", "")]
    [InlineData("""<!DOCTYPE HTML PUBLIC "-//W3C//DTD HTML 4.0 Transitional//EN">""",
        "HTML", "-//W3C//DTD HTML 4.0 Transitional//EN", "")]
    [InlineData("""<!DOCTYPE HTML PUBLIC "-//W3C//DTD HTML 4.01 Transitional//EN" "http://www.w3.org/TR/html4/loose.dtd">""",
        "HTML", "-//W3C//DTD HTML 4.01 Transitional//EN", "http://www.w3.org/TR/html4/loose.dtd")]
    [InlineData("""<!DOCTYPE html SYSTEM "about:legacy-compat">""", "html", "", "about:legacy-compat")]
    [InlineData("""<!DOCTYPE html PUBLIC "" "http://www.ibm.com/data/dtd/v11/ibmxhtml1-transitional.dtd">""",
        "html", "", "http://www.ibm.com/data/dtd/v11/ibmxhtml1-transitional.dtd")]
    public void Agrees_With_The_Markup_Predicate(string html, string name, string publicId, string systemId) =>
        Assert.Equal(
            DocumentModeContext.IsQuirksHtml(html),
            DocumentModeContext.IsQuirksDoctype(name, publicId, systemId));
}
