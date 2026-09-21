using System.Drawing;
using Broiler.Layout.IR;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// <see cref="SvgViewBox.Resolve"/> — SVG 1.1 §7.8, the scale and translation a
/// <c>preserveAspectRatio</c> value produces for a view box on a viewport.
/// </summary>
/// <remarks>
/// The renderer has always applied this mapping; it is public so that a consumer measuring the same
/// document reaches the same rectangles. The cases worth pinning are therefore the ones a caller
/// approximating the default gets wrong: <c>slice</c>, the corner alignments, and <c>none</c>. Every
/// viewport below is 200×100 against a square view box, so meet and slice pick visibly different
/// scales and leave slack on opposite axes.
/// </remarks>
public sealed class SvgViewBoxTests
{
    private static readonly RectangleF Wide = new(0, 0, 200, 100);
    private static readonly RectangleF UnitBox = new(0, 0, 10, 10);

    [Fact(Timeout = 600000)]
    public void The_Default_Is_Uniform_Scale_To_Fit_Centred()
    {
        // meet takes the smaller scale (100/10), so the 10-unit box paints 100 wide in a 200-wide
        // viewport and the 100 units of slack are split evenly.
        var mapping = SvgViewBox.Resolve(null, Wide, UnitBox);

        Assert.Equal(10f, mapping.ScaleX, 4);
        Assert.Equal(10f, mapping.ScaleY, 4);
        Assert.Equal(50f, mapping.TranslateX, 4);
        Assert.Equal(0f, mapping.TranslateY, 4);
    }

    [Theory]
    // An absent, empty or unparseable value all take the initial xMidYMid meet.
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("nonsense")]
    [InlineData("xMidYMid")]
    [InlineData("xMidYMid meet")]
    // "defer" is a legacy prefix that applies only to <image>; the alignment after it still counts.
    [InlineData("defer xMidYMid meet")]
    public void An_Unreadable_Or_Absent_Value_Takes_The_Initial_Mapping(string? preserveAspectRatio)
    {
        var mapping = SvgViewBox.Resolve(preserveAspectRatio, Wide, UnitBox);

        Assert.Equal(SvgViewBox.Resolve(null, Wide, UnitBox), mapping);
    }

    [Fact(Timeout = 600000)]
    public void Defer_Is_Skipped_Rather_Than_Read_As_The_Alignment()
    {
        // Were "defer" taken for the alignment, it would match neither xMin nor xMax and centre —
        // which is the same answer as the default, so the slice keyword after it is what tells the
        // two apart: it is only reached when "defer" has been stepped over.
        var mapping = SvgViewBox.Resolve("defer xMinYMin slice", Wide, UnitBox);

        Assert.Equal(20f, mapping.ScaleX, 4);
        Assert.Equal(0f, mapping.TranslateX, 4);
        Assert.Equal(0f, mapping.TranslateY, 4);
    }

    [Fact(Timeout = 600000)]
    public void None_Scales_The_Axes_Independently()
    {
        var mapping = SvgViewBox.Resolve("none", Wide, UnitBox);

        Assert.Equal(20f, mapping.ScaleX, 4);
        Assert.Equal(10f, mapping.ScaleY, 4);
        Assert.Equal(0f, mapping.TranslateX, 4);
        Assert.Equal(0f, mapping.TranslateY, 4);
    }

    // meet fits the view box inside the viewport, so the scale is the smaller of the two and the
    // slack lies on the wider axis — here 100px of it, placed by the x half of the alignment.
    [Theory]
    [InlineData("xMinYMin meet", 0f)]
    [InlineData("xMidYMin meet", 50f)]
    [InlineData("xMaxYMin meet", 100f)]
    public void Meet_Places_The_Slack_By_The_Alignment(string preserveAspectRatio, float translateX)
    {
        var mapping = SvgViewBox.Resolve(preserveAspectRatio, Wide, UnitBox);

        Assert.Equal(10f, mapping.ScaleX, 4);
        Assert.Equal(10f, mapping.ScaleY, 4);
        Assert.Equal(translateX, mapping.TranslateX, 4);
        Assert.Equal(0f, mapping.TranslateY, 4);
    }

    // slice covers the viewport instead, so the scale is the larger of the two and the overflow —
    // negative slack — is distributed the same way. This is the family a caller hardcoding the
    // default reports at both the wrong scale and the wrong offset.
    [Theory]
    [InlineData("xMinYMin slice", 0f)]
    [InlineData("xMinYMid slice", -50f)]
    [InlineData("xMinYMax slice", -100f)]
    public void Slice_Covers_The_Viewport_And_Overflows_By_The_Alignment(
        string preserveAspectRatio, float translateY)
    {
        var mapping = SvgViewBox.Resolve(preserveAspectRatio, Wide, UnitBox);

        Assert.Equal(20f, mapping.ScaleX, 4);
        Assert.Equal(20f, mapping.ScaleY, 4);
        Assert.Equal(0f, mapping.TranslateX, 4);
        Assert.Equal(translateY, mapping.TranslateY, 4);
    }

    [Fact(Timeout = 600000)]
    public void All_Nine_Alignments_Differ_In_Both_Axes()
    {
        // A square viewport against a 2:1 view box leaves slack on the block axis and none on the
        // inline one; a 1:2 view box does the reverse. Between the two every alignment keyword has
        // an axis it can move on, so nine distinct mappings means no pair of keywords collapses.
        var square = new RectangleF(0, 0, 100, 100);
        var mappings = new HashSet<(SvgViewBox.Mapping Wide, SvgViewBox.Mapping Tall)>();

        foreach (var x in new[] { "xMin", "xMid", "xMax" })
        {
            foreach (var y in new[] { "YMin", "YMid", "YMax" })
            {
                mappings.Add((
                    SvgViewBox.Resolve(x + y + " meet", square, new RectangleF(0, 0, 20, 10)),
                    SvgViewBox.Resolve(x + y + " meet", square, new RectangleF(0, 0, 10, 20))));
            }
        }

        Assert.Equal(9, mappings.Count);
    }

    [Fact(Timeout = 600000)]
    public void The_View_Box_Origin_Is_Folded_Into_The_Translation()
    {
        // viewBox="5 5 10 10" puts user-unit (5,5) at the viewport origin, so the translation has to
        // carry -5 scaled on each axis. A caller that offsets by the origin itself would double it.
        var mapping = SvgViewBox.Resolve("none", new RectangleF(0, 0, 10, 10), new RectangleF(5, 5, 10, 10));

        Assert.Equal(1f, mapping.ScaleX, 4);
        Assert.Equal(1f, mapping.ScaleY, 4);
        Assert.Equal(-5f, mapping.TranslateX, 4);
        Assert.Equal(-5f, mapping.TranslateY, 4);
    }

    [Fact(Timeout = 600000)]
    public void The_Viewport_Origin_Is_Not_Folded_In()
    {
        // The mapping is into the viewport's own coordinate space, so a viewport placed away from
        // the origin does not shift it; the renderer paints into item-local coordinates and adds the
        // box offset itself. A consumer composing this into document space adds it the same way.
        var atOrigin = SvgViewBox.Resolve("xMidYMid meet", new RectangleF(0, 0, 200, 100), UnitBox);
        var moved = SvgViewBox.Resolve("xMidYMid meet", new RectangleF(37, 91, 200, 100), UnitBox);

        Assert.Equal(atOrigin, moved);
    }

    [Theory]
    // §7.7 disables rendering of an element whose view box has a zero extent, and a negative one is
    // an error; a zero scale is the mapping that paints nothing.
    [InlineData(0f, 10f)]
    [InlineData(10f, 0f)]
    [InlineData(-10f, 10f)]
    [InlineData(10f, -10f)]
    public void A_Degenerate_View_Box_Maps_To_Nothing(float width, float height)
    {
        var mapping = SvgViewBox.Resolve("xMidYMid meet", Wide, new RectangleF(0, 0, width, height));

        Assert.Equal(default, mapping);
    }

    // The renderer matched the x half case-insensitively and the Y half case-sensitively, and lifting
    // the method out changed nothing about that. The spec's alignment values are case-sensitive
    // keywords, so the Y half is the strict reading; this pins the asymmetry rather than endorsing
    // it, so that closing it later is a visible change and not a silent one.
    [Fact(Timeout = 600000)]
    public void Alignment_Matching_Is_Case_Insensitive_On_X_Only()
    {
        var lowercase = SvgViewBox.Resolve("xminymin meet", Wide, UnitBox);

        Assert.Equal(0f, lowercase.TranslateX, 4);
        Assert.Equal(SvgViewBox.Resolve("xMinYMid meet", Wide, UnitBox), lowercase);
    }
}
