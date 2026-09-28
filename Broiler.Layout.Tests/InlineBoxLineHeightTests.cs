using System;
using System.Drawing;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// An inline box's <c>line-height</c> makes its line as tall as it, where that is taller than the
/// block's (CSS2.1 §10.8.1), and only the line its words are on.
/// </summary>
/// <remarks>
/// <para>
/// A line was measured by the block's own line height and by its words, and a word counted its
/// inline box's line height only where that was shorter than the word. A link with
/// <c>line-height: 60px</c> in a block of normal line height left the block one word tall, where
/// browsers make it 60px. The flow did count it, before a word could wrap, so a line after it
/// moved down, and a line before a link whose first word wrapped became that tall instead.
/// </para>
/// <para>
/// Words here are 8×16px, each in an anonymous inline box that inherits its parent's style, in a
/// 320px block; the glyphs stand at the top of their line, where this engine puts them for the
/// block's own line height too.
/// </para>
/// </remarks>
public sealed class InlineBoxLineHeightTests
{
    private static readonly Uri BaseUrl = new("file:///inline-box-line-height.html");

    /// <summary>
    /// A link with <c>line-height: 60px</c> holding a word, alone or after a word of the block's,
    /// makes the block 60px tall, with the words at its top. The block was 16px tall.
    /// </summary>
    [Theory(Timeout = 600000)]
    [InlineData(false)]
    [InlineData(true)]
    public void A_Link_With_A_Taller_Line_Height_Makes_Its_Line_That_Tall(bool wordBefore)
    {
        var (root, block) = Block();
        var first = wordBefore ? Word(block) : null;
        var link = Inline(block, "60px");
        var word = Word(link);

        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(60, block.Size.Height, 1);
        Assert.Equal(block.Location.Y, word.Top, 1);
        if (first != null)
            Assert.Equal(block.Location.Y, first.Top, 1);
    }

    /// <summary>
    /// In an 8px block, a link with <c>line-height: 60px</c> holding three words puts them on three
    /// lines 60px apart, and the block is 180px tall. It was 136px: the last line was 16px.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Every_Line_Of_A_Wrapped_Link_Is_That_Tall()
    {
        var (root, block) = Block(width: "8px");
        var link = Inline(block, "60px");
        var words = new[] { Word(link), Word(link), Word(link) };

        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(block.Location.Y + 120, words[2].Top, 1);
        Assert.Equal(180, block.Size.Height, 1);
    }

    /// <summary>
    /// In an 8px block, one word to a line, a word of the block's and then a link with
    /// <c>line-height: 60px</c>: the link's word goes to the second line, 16px down, below a first
    /// line as tall as the block's word, and the block is 76px tall. The first line was 60px, as
    /// tall as the link, and the link's word 60px down.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Wrapped_Link_Leaves_The_Line_Before_It_Alone()
    {
        var (root, block) = Block(width: "8px");
        var first = Word(block);
        var link = Inline(block, "60px");
        var word = Word(link);

        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(block.Location.Y, first.Top, 1);
        Assert.Equal(block.Location.Y + 16, word.Top, 1);
        Assert.Equal(76, block.Size.Height, 1);
    }

    /// <summary>
    /// A link with <c>line-height: 40px</c> in a block with <c>line-height: 10px</c> makes the
    /// block 40px tall. The block was 16px, as tall as the word.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Link_Taller_Than_A_Short_Block_Line_Height()
    {
        var (root, block) = Block();
        block.LineHeight = "10px";
        var link = Inline(block, "40px");
        Word(link);

        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(40, block.Size.Height, 1);
    }

    /// <summary>
    /// In an 8px block, one word to a line, a span with <c>line-height: 60px</c> that holds its word
    /// itself, between two words of the block's: the span's line is 60px tall, the word after it
    /// 76px down, and the block 92px tall. The span's line was 16px, and the block 48px: the flow
    /// counted the line height of the box the span is in, not the span's.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Span_Holding_Its_Word_Itself_Makes_Its_Line_That_Tall()
    {
        var (root, block) = Block(width: "8px");
        Word(block);
        var span = new CssBox(block, new HtmlTag("span", false, null), BaseUrl)
        {
            Display = "inline",
            LineHeight = "60px",
            Text = "G".AsMemory(),
        };
        span.ParseToWords();
        var spanWord = Assert.Single(span.Words);
        var after = Word(block);

        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(block.Location.Y + 16, spanWord.Top, 1);
        Assert.Equal(block.Location.Y + 76, after.Top, 1);
        Assert.Equal(92, block.Size.Height, 1);
    }

    /// <summary>
    /// An 8px block clamped to two lines, one word to a line, with a link with
    /// <c>line-height: 60px</c> on the second: the block keeps that line at 60px, 76px tall. The
    /// clamp measured the line by its word, and the block was 32px.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Clamped_Block_Keeps_Its_Last_Line_That_Tall()
    {
        var (root, block) = Block(width: "8px");
        block.LineClamp = "2";
        Word(block);
        var link = Inline(block, "60px");
        var second = Word(link);
        Word(block);

        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(block.Location.Y + 16, second.Top, 1);
        Assert.Equal(76, block.Size.Height, 1);
    }

    /// <summary>
    /// Controls, which pass before and after: a block with <c>line-height: 60px</c> is 60px tall
    /// holding a word, or a link with <c>line-height: 20px</c>; and a block and a link both with
    /// <c>line-height: 10px</c> make a 10px line, with the word overflowing it.
    /// </summary>
    [Theory(Timeout = 600000)]
    [InlineData("60px", null, 60)]
    [InlineData("60px", "20px", 60)]
    [InlineData("10px", "10px", 10)]
    public void Control_The_Block_Line_Height_Or_A_Shorter_One(string blockLineHeight, string? linkLineHeight, float height)
    {
        var (root, block) = Block();
        block.LineHeight = blockLineHeight;
        Word(linkLineHeight == null ? block : Inline(block, linkLineHeight));

        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(height, block.Size.Height, 1);
    }

    /// <summary>
    /// Control, which passes before and after: a block with <c>line-height: 24px</c> holding an
    /// 8×40px inline-block and a word, which stands on the inline-block's baseline, keeps the
    /// 44.8px it had. The word's line height is the block's, so the line is measured as before.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Control_A_Word_Beside_A_Taller_Inline_Block()
    {
        var (root, block) = Block();
        block.LineHeight = "24px";
        _ = new CssBox(block, new HtmlTag("span", false, null), BaseUrl)
        {
            Display = CssConstants.InlineBlock,
            Width = "8px",
            Height = "40px",
        };
        Word(block);

        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(44.8, block.Size.Height, 1);
    }

    private static CssBox Root() => new(null, new HtmlTag("div", false, null), BaseUrl)
    {
        Display = "block",
        Location = new PointF(0, 0),
        Size = new SizeF(1024, 768),
        LayoutEnvironment = new FakeLayoutEnvironment(),
    };

    /// <summary>A block of the given width in a root.</summary>
    private static (CssBox Root, CssBox Block) Block(string width = "320px")
    {
        var root = Root();
        var block = new CssBox(root, new HtmlTag("div", false, null), BaseUrl) { Display = "block", Width = width };
        return (root, block);
    }

    /// <summary>A link in <paramref name="parent"/> with the given line height.</summary>
    private static CssBox Inline(CssBox parent, string lineHeight) =>
        new(parent, new HtmlTag("a", false, null), BaseUrl) { Display = "inline", LineHeight = lineHeight };

    /// <summary>
    /// An anonymous inline box in <paramref name="parent"/> holding one word, inheriting the
    /// parent's style as the box a text node makes does, and the word.
    /// </summary>
    private static CssRect Word(CssBox parent)
    {
        var text = new CssBox(parent, null, BaseUrl);
        text.InheritStyle();
        text.Display = "inline";
        text.Text = "X".AsMemory();
        text.ParseToWords();
        return Assert.Single(text.Words);
    }

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
