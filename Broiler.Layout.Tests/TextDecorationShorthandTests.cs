using System;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// CSS Text Decoration 3 §2.4: <c>text-decoration</c> sets <c>text-decoration-line</c>,
/// <c>text-decoration-style</c> and <c>text-decoration-color</c> in any order, and resets the ones
/// the declaration omits.
/// </summary>
/// <remarks>
/// The value was split on every space, so a functional colour came apart and its last piece was
/// taken for the colour: <c>underline rgb(255, 0, 0)</c> had the colour <c>0)</c>, which is none,
/// and the underline was drawn in the text's colour. Only the last line keyword was kept, so
/// <c>underline overline</c> drew an overline alone.
/// </remarks>
public sealed class TextDecorationShorthandTests
{
    private static readonly Uri BaseUrl = new("file:///decoration.html");

    private static CssBox Apply(string value)
    {
        var box = new CssBox(null, null, BaseUrl);
        CssUtils.SetPropertyValue(box, "text-decoration", value);
        return box;
    }

    [Theory(Timeout = 600000)]
    [InlineData("underline rgb(255, 0, 0)", "rgb(255, 0, 0)")]
    [InlineData("rgb(255, 0, 0) underline", "rgb(255, 0, 0)")]
    [InlineData("underline dashed hsl(0 100% 50% / 0.5)", "hsl(0 100% 50% / 0.5)")]
    [InlineData("underline red", "red")]
    public void A_Functional_Colour_Stays_In_One_Piece(string value, string color)
    {
        var box = Apply(value);

        Assert.Equal("underline", box.TextDecoration);
        Assert.Equal(color, box.TextDecorationColor);
    }

    [Fact(Timeout = 600000)]
    public void The_Style_Is_Read_Beside_A_Functional_Colour()
    {
        var box = Apply("underline dotted rgb(0, 0, 255)");

        Assert.Equal("dotted", box.TextDecorationStyle);
        Assert.Equal("rgb(0, 0, 255)", box.TextDecorationColor);
    }

    [Fact(Timeout = 600000)]
    public void Several_Lines_Are_All_Kept()
    {
        var box = Apply("underline overline");

        Assert.Equal("underline overline", box.TextDecoration);
        Assert.Equal("solid", box.TextDecorationStyle);
        Assert.Equal("currentcolor", box.TextDecorationColor);
    }

    [Fact(Timeout = 600000)]
    public void None_Draws_No_Line()
    {
        var box = Apply("none");

        Assert.Equal("none", box.TextDecoration);
    }
}
