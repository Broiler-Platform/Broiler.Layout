using System;
using System.Drawing;
using System.Linq;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// An inline box's rectangle on a line goes where its words go when the line's baseline moves them.
/// </summary>
/// <remarks>
/// <para>
/// An inline box's geometry, what <c>getBoundingClientRect</c> reports, is the union of its
/// rectangles on its lines. <c>CssLineBox.SetBaseLine</c> moved a box's words down to the baseline
/// and its rectangle with them only where the box was inside a taller inline box, so a span in the
/// block itself stayed at the top of its line: beside an empty 30px inline-block, the span holding
/// "a" was 0px down its line, where its "a" was drawn 15.15px down and browsers report 15.
/// </para>
/// <para>
/// Each line here is in a 320px block. Words are as tall as their font, 16px unless it says
/// otherwise, and 8px wide a letter, and stand on a baseline four fifths of the way down them.
/// </para>
/// </remarks>
public sealed class InlineBoxRectangleFollowsWordsTests
{
    private static readonly Uri BaseUrl = new("file:///inline-box-rectangle.html");

    private const double Ascent = 12.8;

    /// <summary>
    /// Beside an empty 8 × 30px inline-block, which stands on the baseline with its bottom, the
    /// span holding "a" is where its "a" is, 17.2px down. It was at the top.
    /// </summary>
    [Fact]
    public void A_Span_Is_Where_Its_Words_Are()
    {
        var tree = Build(30);
        var span = Span(tree.Block, "a");
        Layout(tree);

        Assert.Equal(30 - Ascent, tree.Top(span.Boxes[0].Words[0]), 1);
        Assert.Equal(30 - Ascent, tree.RectangleTop(span), 1);
    }

    /// <summary>
    /// A bold box inside the span goes with the span and its words, 27.2px down beside a 40px
    /// inline-block. Both were at the top.
    /// </summary>
    [Fact]
    public void A_Box_Inside_The_Span_Goes_With_It()
    {
        var tree = Build(40);
        var span = Span(tree.Block, "abc ");
        var bold = Span(span, "bold");
        Layout(tree);

        Assert.Equal(40 - Ascent, tree.RectangleTop(span), 1);
        Assert.Equal(40 - Ascent, tree.RectangleTop(bold), 1);
    }

    /// <summary>
    /// Laid out a second time, the span is 17.2px down still.
    /// </summary>
    [Fact]
    public void A_Second_Layout_Gives_The_Same_Rectangle()
    {
        var tree = Build(30);
        var span = Span(tree.Block, "a");
        Layout(tree);
        Layout(tree);

        Assert.Equal(30 - Ascent, tree.RectangleTop(span), 1);
    }

    /// <summary>
    /// An inline-flex container has no words on its line, and keeps its rectangle there where the
    /// box is, at the top of the line, and the block is 27.2px tall, the box's 24px and the strut's
    /// descent. In a 10px font, the rectangle was moved 4.8px down, by what the font's ascent is
    /// short of the block's, and the block grew by as much.
    /// </summary>
    [Fact]
    public void An_Inline_Flex_Container_Keeps_Its_Rectangle()
    {
        var tree = Build(0);
        var flex = new CssBox(tree.Block, new HtmlTag("span", false, null), BaseUrl)
        {
            Display = "inline-flex",
            FontSize = "10px",
            PaddingTop = "4px",
            PaddingBottom = "4px",
        };
        Span(flex, "a");
        Layout(tree);

        Assert.Equal(flex.Location.Y, flex.Rectangles.Values.First().Top, 1);
        Assert.Equal(27.2, tree.Block.Size.Height, 1);
    }

    /// <summary>
    /// Control, which passes before and after: on a line of text alone, the span is at the top,
    /// where its words are.
    /// </summary>
    [Fact]
    public void Control_A_Line_Of_Text_Alone()
    {
        var tree = Build(0);
        var span = Span(tree.Block, "a");
        Layout(tree);

        Assert.Equal(0, tree.Top(span.Boxes[0].Words[0]), 1);
        Assert.Equal(0, tree.RectangleTop(span), 1);
    }

    /// <summary>The root and the 320px block.</summary>
    private sealed record Tree(CssBox Root, CssBox Block)
    {
        public double Top(CssRect word) => word.Top - Block.Location.Y;

        /// <summary>How far the box's rectangle on its first line stands below the block's top.</summary>
        public double RectangleTop(CssBox box) => box.Rectangles.Values.First().Top - Block.Location.Y;
    }

    /// <summary>
    /// In a 320px block in the root, an empty 8px wide inline-block of the given height, if any.
    /// </summary>
    private static Tree Build(int inlineBlockHeight)
    {
        var root = new CssBox(null, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = "block",
            Location = new PointF(0, 0),
            Size = new SizeF(1024, 768),
            LayoutEnvironment = new FakeLayoutEnvironment(),
        };

        var body = new CssBox(root, new HtmlTag("div", false, null), BaseUrl) { Display = "block" };
        var block = new CssBox(body, new HtmlTag("div", false, null), BaseUrl) { Display = "block", Width = "320px" };

        if (inlineBlockHeight > 0)
        {
            _ = new CssBox(block, new HtmlTag("span", false, null), BaseUrl)
            {
                Display = CssConstants.InlineBlock,
                Width = "8px",
                Height = inlineBlockHeight + "px",
            };
        }

        return new Tree(root, block);
    }

    /// <summary>Adds to <paramref name="parent"/> a span holding <paramref name="text"/>.</summary>
    private static CssBox Span(CssBox parent, string text)
    {
        var span = new CssBox(parent, new HtmlTag("span", false, null), BaseUrl) { Display = "inline" };
        var word = new CssBox(span, null, BaseUrl) { Display = "inline", Text = text.AsMemory() };
        word.ParseToWords();
        return span;
    }

    private static void Layout(Tree tree)
    {
        FlexGridItemBlockification.Generate(tree.Root);
        tree.Root.PerformLayout(tree.Root.LayoutEnvironment);
    }

    private sealed class FakeLayoutEnvironment : ILayoutEnvironment
    {
        public Broiler.Graphics.Text.ILayoutFont GetFont(string family, double size, LayoutFontStyle style, string? fontFeatures = null) => new FakeFont(size);
        public SizeF MeasureText(Broiler.Graphics.Text.ILayoutFont font, string text) => new(8 * text.Length, (float)font.Height);
        public void MeasureText(Broiler.Graphics.Text.ILayoutFont font, string text, double maxWidth, out int charFit, out double charFitWidth) { charFit = text.Length; charFitWidth = 8 * text.Length; }
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

    /// <summary>A font of the given size in points, as tall in pixels as the size is.</summary>
    private sealed class FakeFont(double size) : Broiler.Graphics.Text.ILayoutFont
    {
        public double Size => size;
        public double Height => size * 4 / 3;
        public double UnderlineOffset => 0;
        public double LeftPadding => 0;
        public string? FontFeatures => null;
    }
}
