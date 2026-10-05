using System;
using System.Collections.Generic;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// An <c>&lt;a&gt;</c> is clickable when it is a link, which its <c>href</c> makes it, whatever other
/// attributes it has.
/// </summary>
/// <remarks>
/// <para>
/// HTML §4.6.1: an <c>a</c> element with an <c>href</c> attribute is a hyperlink; without one it is a
/// placeholder for where a link might be. <see cref="CssBox.IsClickable"/> asked for the opposite of an
/// <c>id</c> instead, a rule for the named anchors of old pages (<c>&lt;a id="top"&gt;</c>), so every
/// link with an <c>id</c> was not clickable -- a click on one in the window followed nothing -- and
/// every placeholder without one was, its empty target followed as a link to the page itself.
/// </para>
/// </remarks>
public sealed class ClickableLinkTests
{
    private static readonly Uri BaseUrl = new("file:///clickable-link.html");

    /// <summary>A link with an <c>id</c> is clickable. It was not.</summary>
    [Fact]
    public void A_Link_With_An_Id_Is_Clickable()
    {
        Assert.True(Box("a", ("id", "next"), ("href", "/next")).IsClickable);
    }

    /// <summary>A link with an empty <c>href</c>, a link to its own page, is clickable whatever else it carries.</summary>
    [Fact]
    public void A_Link_With_An_Empty_Href_And_An_Id_Is_Clickable()
    {
        Assert.True(Box("a", ("href", ""), ("id", "self")).IsClickable);
    }

    /// <summary>An <c>a</c> without an <c>href</c> is not a link, and not clickable. It was, unless it had an <c>id</c>.</summary>
    [Fact]
    public void A_Placeholder_Is_Not_Clickable()
    {
        Assert.False(Box("a").IsClickable);
        Assert.False(Box("a", ("name", "top")).IsClickable);
        Assert.False(Box("a", ("onclick", "go()")).IsClickable);
    }

    /// <summary>
    /// Controls, which pass before and after: a link without an <c>id</c>, a button and a submit
    /// button are clickable; a named anchor with an <c>id</c>, a text field and a <c>span</c> are not.
    /// </summary>
    [Fact]
    public void Control_Other_Elements()
    {
        Assert.True(Box("a", ("href", "/next")).IsClickable);
        Assert.True(Box("button").IsClickable);
        Assert.True(Box("input", ("type", "submit")).IsClickable);
        Assert.False(Box("a", ("id", "top")).IsClickable);
        Assert.False(Box("input", ("type", "text")).IsClickable);
        Assert.False(Box("span", ("id", "x")).IsClickable);
    }

    private static CssBox Box(string name, params (string Name, string Value)[] attributes)
    {
        var bag = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (attribute, value) in attributes)
            bag[attribute] = value;

        return new CssBox(null, new HtmlTag(name, false, bag), BaseUrl);
    }
}
