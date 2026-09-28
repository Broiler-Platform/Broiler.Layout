using System;
using System.Drawing;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// A word that does not fit on a line after a box placed on it whole, an inline-block or an
/// inline-flex container, goes to the next line.
/// </summary>
/// <remarks>
/// <para>
/// CSS Text 3 §5: there is a soft wrap opportunity after an atomic inline. <c>FlowBox</c> wraps a
/// word only when something is on the line before it, and took the line's words for all it held,
/// so a line holding an inline-block and no word counted as empty: a word after the inline-block
/// that did not fit beside it stayed on the line and ran on past the block's edge.
/// </para>
/// <para>
/// Each block here is 100px wide, in a block in the root. Words are 8px wide a letter and 16px
/// tall, a line is 16px tall, and inline-blocks are empty and 10px tall.
/// </para>
/// </remarks>
public sealed class WrapAfterAtomicInlineTests
{
    private static readonly Uri BaseUrl = new("file:///wrap-after-atomic-inline.html");

    /// <summary>
    /// After a 90px inline-block or inline-flex container, "abcdefgh", 64px, goes to the next line:
    /// at the block's left edge, 16px down. It stayed 90px in on the first line.
    /// </summary>
    [Theory]
    [InlineData(CssConstants.InlineBlock)]
    [InlineData("inline-flex")]
    public void A_Word_That_Does_Not_Fit_After_An_Atomic_Inline_Wraps(string display)
    {
        var (root, block, word) = Build(display, 90, "abcdefgh");
        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(0, word.Words[0].Left - block.Location.X, 1);
        Assert.Equal(16, word.Words[0].Top - block.Location.Y, 1);
    }

    /// <summary>
    /// With its words in a span, the word goes to the next line as well, and the block is two
    /// lines tall, 32px. It stayed on the first line, and the block was 16px tall.
    /// </summary>
    [Fact]
    public void A_Word_In_A_Span_Wraps_Too()
    {
        var (root, block, word) = Build(CssConstants.InlineBlock, 90, "abcdefgh", inSpan: true);
        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(16, word.Words[0].Top - block.Location.Y, 1);
        Assert.Equal(32, block.Size.Height, 1);
    }

    /// <summary>
    /// Laid out a second time, the word is on the second line still.
    /// </summary>
    [Fact]
    public void A_Second_Layout_Gives_The_Same_Lines()
    {
        var (root, block, word) = Build(CssConstants.InlineBlock, 90, "abcdefgh");
        root.PerformLayout(root.LayoutEnvironment);
        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(0, word.Words[0].Left - block.Location.X, 1);
        Assert.Equal(16, word.Words[0].Top - block.Location.Y, 1);
    }

    /// <summary>
    /// Control, which passes before and after: after a 50px inline-block, "ab", 16px, fits and
    /// stays on the line, 50px in.
    /// </summary>
    [Fact]
    public void Control_A_Word_That_Fits_Stays_On_The_Line()
    {
        var (root, block, word) = Build(CssConstants.InlineBlock, 50, "ab");
        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(50, word.Words[0].Left - block.Location.X, 1);
        Assert.Equal(0, word.Words[0].Top - block.Location.Y, 1);
    }

    /// <summary>
    /// Control, which passes before and after: a word wider than the block, first on its line,
    /// stays there and overflows it rather than leaving an empty line above it.
    /// </summary>
    [Fact]
    public void Control_A_Wide_Word_First_On_Its_Line_Stays()
    {
        var (root, block, word) = Build(null, 0, "abcdefghijklmnop");
        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(0, word.Words[0].Left - block.Location.X, 1);
        Assert.Equal(0, word.Words[0].Top - block.Location.Y, 1);
    }

    /// <summary>
    /// A 100px block in the root holding an empty box of the given display and width, if any, then
    /// <paramref name="text"/>, in a span if <paramref name="inSpan"/>. Returns the root, the block
    /// and the box holding the words.
    /// </summary>
    private static (CssBox Root, CssBox Block, CssBox Word) Build(string? display, int width, string text, bool inSpan = false)
    {
        var root = new CssBox(null, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = "block",
            Location = new PointF(0, 0),
            Size = new SizeF(1024, 768),
            LayoutEnvironment = new FakeLayoutEnvironment(),
        };

        var body = new CssBox(root, new HtmlTag("div", false, null), BaseUrl) { Display = "block" };
        var block = new CssBox(body, new HtmlTag("div", false, null), BaseUrl) { Display = "block", Width = "100px" };

        if (display != null)
        {
            _ = new CssBox(block, new HtmlTag("span", false, null), BaseUrl)
            {
                Display = display,
                Width = width + "px",
                Height = "10px",
            };
        }

        var parent = inSpan ? new CssBox(block, new HtmlTag("span", false, null), BaseUrl) { Display = "inline" } : block;
        var word = new CssBox(parent, null, BaseUrl) { Display = "inline", Text = text.AsMemory() };
        word.ParseToWords();

        return (root, block, word);
    }

    private sealed class FakeLayoutEnvironment : ILayoutEnvironment
    {
        private static readonly Broiler.Graphics.Text.ILayoutFont TheFont = new FakeFont();
        public Broiler.Graphics.Text.ILayoutFont GetFont(string family, double size, LayoutFontStyle style, string? fontFeatures = null) => TheFont;
        public SizeF MeasureText(Broiler.Graphics.Text.ILayoutFont font, string text) => new(8 * text.Length, 16);
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

    private sealed class FakeFont : Broiler.Graphics.Text.ILayoutFont
    {
        public double Size => 12;
        public double Height => 16;
        public double UnderlineOffset => 0;
        public double LeftPadding => 0;
        public string? FontFeatures => null;
    }
}
