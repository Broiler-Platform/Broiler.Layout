using System;
using System.Drawing;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// Padding or a border keeps a box's margin from collapsing with its children's however thin it
/// is (CSS2.1 §8.3.1).
/// </summary>
/// <remarks>
/// <para>
/// Margins are adjoining only where "no line boxes, no clearance, no padding and no border
/// separate them". The engine took padding and borders under 0.1px for none, so a thin padding
/// let the margins collapse. www.mediawiki.org's <c>.mw-page-container</c> has
/// <c>padding-top: 0.05px</c> to keep its site notice's 24px top margin inside it, and began 24px
/// below the header, where browsers begin it right below.
/// </para>
/// <para>
/// Each block here follows a 10px block in a wrapper, and holds a 10px block with a 20px margin;
/// the wrapper sits in a body in a root, as a page's content does.
/// </para>
/// </remarks>
public sealed class ThinPaddingMarginTests
{
    private static readonly Uri BaseUrl = new("file:///thin-padding-margin.html");

    /// <summary>
    /// A block with <c>padding-top: 0.05px</c> keeps its child's top margin inside it: it begins
    /// right below the box before it, holds the child 20.05px down and is 30.05px tall. It began
    /// 20px down, with the child at its top, and was 10.05px tall.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Thin_Top_Padding_Keeps_The_First_Child_Margin_Inside()
    {
        var (before, block, child, _) = Lay(block => block.PaddingTop = "0.05px", child => child.MarginTop = "20px");

        Assert.Equal(before.ActualBottom, block.Location.Y, 2);
        Assert.Equal(block.Location.Y + 20.05, child.Location.Y, 2);
        Assert.Equal(30.05, block.Size.Height, 2);
    }

    /// <summary>
    /// A 0.05px top border does the same: the block begins right below the box before it. It
    /// began 20px down.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Thin_Top_Border_Keeps_The_First_Child_Margin_Inside()
    {
        var (before, block, child, _) = Lay(block =>
        {
            block.BorderTopWidth = "0.05px";
            block.BorderTopStyle = "solid";
        }, child => child.MarginTop = "20px");

        Assert.Equal(before.ActualBottom, block.Location.Y, 2);
        Assert.True(child.Location.Y - block.Location.Y >= 20, $"child {child.Location.Y - block.Location.Y}px down");
    }

    /// <summary>
    /// A block with <c>padding-bottom: 0.05px</c> keeps its last child's bottom margin inside it:
    /// it is 30.05px tall, and the block after it begins right below it. It was 10.05px tall, and
    /// the block after it began 20px below it.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Thin_Bottom_Padding_Keeps_The_Last_Child_Margin_Inside()
    {
        var (_, block, _, after) = Lay(block => block.PaddingBottom = "0.05px", child => child.MarginBottom = "20px");

        Assert.Equal(30.05, block.Size.Height, 2);
        Assert.Equal(block.ActualBottom, after.Location.Y, 2);
    }

    /// <summary>
    /// An empty block with <c>padding-top: 0.05px</c> and 10px margins does not collapse its
    /// margins through itself: the block after it begins 20.05px below the box before it. It
    /// began 10px below.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Thin_Padding_Keeps_An_Empty_Block_From_Collapsing_Through()
    {
        var (before, block, _, after) = Lay(block =>
        {
            block.PaddingTop = "0.05px";
            block.MarginTop = "10px";
            block.MarginBottom = "10px";
        }, child => child.Display = CssConstants.None);

        Assert.Equal(before.ActualBottom + 20.05, after.Location.Y, 2);
    }

    /// <summary>
    /// Controls, which pass before and after: with no padding the child's top margin collapses
    /// through the block, which begins 20px down with the child at its top; with 1px of padding it
    /// stays inside, and the block begins right below the box before it with the child 21px down.
    /// </summary>
    [Theory(Timeout = 600000)]
    [InlineData("0", 20, 0)]
    [InlineData("1px", 0, 21)]
    public void Control_No_Padding_Or_1px(string paddingTop, float blockDown, float childDown)
    {
        var (before, block, child, _) = Lay(block => block.PaddingTop = paddingTop, child => child.MarginTop = "20px");

        Assert.Equal(before.ActualBottom + blockDown, block.Location.Y, 2);
        Assert.Equal(block.Location.Y + childDown, child.Location.Y, 2);
    }

    /// <summary>
    /// A root holding a body holding a wrapper, which holds a 10px block, a block styled by
    /// <paramref name="styleBlock"/> that holds a 10px block styled by <paramref name="styleChild"/>,
    /// and a 10px block after, laid out. Returns the block before, the styled block, its child and
    /// the block after.
    /// </summary>
    private static (CssBox Before, CssBox Block, CssBox Child, CssBox After) Lay(Action<CssBox> styleBlock, Action<CssBox> styleChild)
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
        styleBlock(block);
        var child = Block(block);
        child.Height = "10px";
        styleChild(child);
        var after = Block(wrapper);
        after.Height = "10px";

        root.PerformLayout(root.LayoutEnvironment);
        return (before, block, child, after);
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
