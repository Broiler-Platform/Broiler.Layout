using System;
using System.Drawing;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// A block's first child's top margin collapses through the block's top whatever the block has at
/// its bottom (CSS2.1 §8.3.1).
/// </summary>
/// <remarks>
/// <para>
/// Only the block's top padding and border separate its top margin from its first in-flow child's.
/// <c>MarginTopCollapse</c> asked for no bottom padding or border as well, so a block with either
/// kept its first child's top margin inside it: it began where the box before it ended, and held
/// the child that far down.
/// </para>
/// <para>
/// Each block here follows a 10px block in a wrapper, and holds a 10px block with
/// <c>margin-top: 20px</c>; the wrapper sits in a body in a root, as a page's content does.
/// </para>
/// </remarks>
public sealed class FirstChildMarginBottomPaddingTests
{
    private static readonly Uri BaseUrl = new("file:///first-child-margin-bottom-padding.html");

    /// <summary>
    /// A block with 10px of bottom padding, a 5px bottom border, or both with a 2px border: it
    /// begins 20px below the box before it, with the child at its top, and is 20px, 15px or 22px
    /// tall. It began right below that box, with the child 20px down, and was 20px taller.
    /// </summary>
    [Theory(Timeout = 600000)]
    [InlineData("10px", null, 20)]
    [InlineData("0", "5px", 15)]
    [InlineData("10px", "2px", 22)]
    public void The_Child_Margin_Collapses_Through_The_Top(string paddingBottom, string? borderBottom, float height)
    {
        var (before, block, child) = Lay(block =>
        {
            block.PaddingBottom = paddingBottom;
            if (borderBottom != null)
            {
                block.BorderBottomWidth = borderBottom;
                block.BorderBottomStyle = "solid";
            }
        });

        Assert.Equal(before.ActualBottom + 20, block.Location.Y, 1);
        Assert.Equal(block.Location.Y, child.Location.Y, 1);
        Assert.Equal(height, block.Size.Height, 1);
    }

    /// <summary>
    /// The same block with a 30px top margin of its own: the two margins collapse to 30px, so it
    /// begins 30px below the box before it with the child at its top. The child was 20px down.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void The_Collapsed_Margin_Is_The_Larger_One()
    {
        var (before, block, child) = Lay(block =>
        {
            block.MarginTop = "30px";
            block.PaddingBottom = "10px";
        });

        Assert.Equal(before.ActualBottom + 30, block.Location.Y, 1);
        Assert.Equal(block.Location.Y, child.Location.Y, 1);
    }

    /// <summary>
    /// Controls, which pass before and after: with 10px of top padding the margin stays inside the
    /// block, which begins right below the box before it with the child 30px down; with no padding
    /// the margin collapses, and the block begins 20px down with the child at its top.
    /// </summary>
    [Theory(Timeout = 600000)]
    [InlineData("10px", 0, 30)]
    [InlineData("0", 20, 0)]
    public void Control_Top_Padding_Or_None(string paddingTop, float blockDown, float childDown)
    {
        var (before, block, child) = Lay(block => block.PaddingTop = paddingTop);

        Assert.Equal(before.ActualBottom + blockDown, block.Location.Y, 1);
        Assert.Equal(block.Location.Y + childDown, child.Location.Y, 1);
    }

    /// <summary>
    /// A root holding a body holding a wrapper, which holds a 10px block and then a block styled by
    /// <paramref name="style"/> that holds a 10px block with a 20px top margin, laid out. Returns
    /// the 10px block before, the styled block and its child.
    /// </summary>
    private static (CssBox Before, CssBox Block, CssBox Child) Lay(Action<CssBox> style)
    {
        var root = new CssBox(null, new HtmlTag("html", false, null), BaseUrl)
        {
            Display = "block",
            Location = new PointF(0, 0),
            Size = new SizeF(1024, 768),
            LayoutEnvironment = new FakeLayoutEnvironment(),
        };
        var body = Block(root);
        var wrapper = Block(body);
        var before = Block(wrapper);
        before.Height = "10px";
        var block = Block(wrapper);
        style(block);
        var child = Block(block);
        child.Height = "10px";
        child.MarginTop = "20px";

        root.PerformLayout(root.LayoutEnvironment);
        return (before, block, child);
    }

    private static CssBox Block(CssBox parent) =>
        new(parent, new HtmlTag("div", false, null), BaseUrl) { Display = CssConstants.Block };

    private sealed class FakeLayoutEnvironment : ILayoutEnvironment
    {
        private static readonly Broiler.Graphics.Text.ILayoutFont TheFont = new FakeFont();
        public Broiler.Graphics.Text.ILayoutFont GetFont(string family, double size, LayoutFontStyle style, string? fontFeatures = null) => TheFont;
        public SizeF MeasureText(Broiler.Graphics.Text.ILayoutFont font, string text) => new(8, 16);
        public void MeasureText(Broiler.Graphics.Text.ILayoutFont font, string text, double maxWidth, out int charFit, out double charFitWidth) { charFit = text.Length; charFitWidth = 8; }
        public double GetWhitespaceWidth(Broiler.Graphics.Text.ILayoutFont font) => 4;
        public ImageIntrinsics GetImageIntrinsics(object imageHandle) => default;
        public Broiler.Graphics.Color.BColor ParseColor(string value) => default;
        public void RequestRefresh(bool relayout) { }
        public SizeF ViewportSize => new(1024, 768);
        public PointF RootLocation => PointF.Empty;
        public SizeF ActualSize { get; set; }
        public bool AvoidGeometryAntialias => false;
        public SizeF PageSize => new(1024, 768);
        public int MarginTop => 0;
        public void ReportLayoutError(string message, Exception? exception = null) { }
        public bool AvoidAsyncImagesLoading => true;
        public bool AvoidImagesLateLoading => true;
        public ILayoutImageLoader CreateImageLoader(Action<object?, RectangleF, bool> onComplete) => null!;
        public string FormatListMarker(int number, string style) => string.Empty;
    }

    private sealed class FakeFont : Broiler.Graphics.Text.ILayoutFont
    {
        public double Size => 16;
        public double Height => 16;
        public double UnderlineOffset => 0;
        public double LeftPadding => 0;
        public string? FontFeatures => null;
    }
}
