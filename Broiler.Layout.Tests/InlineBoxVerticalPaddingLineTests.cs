using System;
using System.Drawing;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// An inline box's vertical padding and border leave its line where it is: they reach above and
/// below its words without moving them or anything else on the block.
/// </summary>
/// <remarks>
/// <para>
/// CSS2.1 §10.6.1: the vertical padding, border and margin of an inline, non-replaced box "start at
/// the top and bottom of the content area, and [have] nothing to do with the 'line-height'". Line
/// layout measured a line by the rectangles of the inline boxes on it, which reach above and below
/// their words by their padding and border. A link with 10px of padding on a block's first line
/// reached above the block, and the block moved every line 10px down to bring it back inside,
/// growing 10px taller. A table cell measured what it centres the same way.
/// </para>
/// <para>
/// Words here are 8×16px, one to each inline box, in a 320px block; no line-height is set.
/// </para>
/// </remarks>
public sealed class InlineBoxVerticalPaddingLineTests
{
    private static readonly Uri BaseUrl = new("file:///inline-box-vertical-padding.html");

    /// <summary>
    /// A word and then a link holding a word, with 10px of top padding, a 10px top border, or both:
    /// the block is one line, 16px tall, with both words at its top and the link's rectangle
    /// reaching above it. The block was 26px or 36px tall, with the words that far down.
    /// </summary>
    [Theory(Timeout = 600000)]
    [InlineData(10, 0)]
    [InlineData(0, 10)]
    [InlineData(10, 10)]
    public void A_Padded_Link_Leaves_Its_Line_Where_It_Is(int paddingTop, int borderTop)
    {
        var (root, block) = Block();
        var first = Word(block);
        var link = Inline(block, link =>
        {
            link.PaddingTop = $"{paddingTop}px";
            if (borderTop > 0)
            {
                link.BorderTopWidth = $"{borderTop}px";
                link.BorderTopStyle = "solid";
            }
        });
        var word = Word(link);

        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(16, block.Size.Height, 1);
        Assert.Equal(block.Location.Y, first.Top, 1);
        Assert.Equal(block.Location.Y, word.Top, 1);
        Assert.Equal(word.Top - paddingTop - borderTop, Assert.Single(link.Rectangles).Value.Top, 1);
    }

    /// <summary>
    /// A link with 5px of padding holding one with 10px: the block is 16px tall with the word at its
    /// top. The two paddings added up, and it was 31px tall with the word 15px down.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Nested_Padded_Links_Leave_Their_Line_Where_It_Is()
    {
        var (root, block) = Block();
        var outer = Inline(block, outer => outer.PaddingTop = outer.PaddingBottom = "5px");
        var inner = Inline(outer, inner => inner.PaddingTop = inner.PaddingBottom = "10px");
        var word = Word(inner);

        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(16, block.Size.Height, 1);
        Assert.Equal(block.Location.Y, word.Top, 1);
    }

    /// <summary>
    /// In an 8px block, one word to a line, a link with 40px of top padding on the third line
    /// reaches 8px above the block. The block is three lines, 48px tall, with its first word at its
    /// top; every line was 8px lower, and the block 56px tall.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Padded_Link_On_A_Later_Line_Moves_No_Line()
    {
        var (root, block) = Block(width: "8px");
        var first = Word(block);
        var second = Word(block);
        var link = Inline(block, link => link.PaddingTop = "40px");
        var third = Word(link);

        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(48, block.Size.Height, 1);
        Assert.Equal(block.Location.Y, first.Top, 1);
        Assert.Equal(block.Location.Y + 16, second.Top, 1);
        Assert.Equal(block.Location.Y + 32, third.Top, 1);
    }

    /// <summary>
    /// In an 8px block with a 30px line height, one word to a line, a link with 10px of top padding
    /// on the second line: the block is two lines, 60px tall. The second line was measured from the
    /// top of the link's padding and ended 10px short, at 50px.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Padded_Link_On_A_Later_Line_Keeps_That_Line_Its_Height()
    {
        var (root, block) = Block(width: "8px");
        block.LineHeight = "30px";
        Word(block);
        var link = Inline(block, link => link.PaddingTop = "10px");
        var second = Word(link);

        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(block.Location.Y + 30, second.Top, 1);
        Assert.Equal(60, block.Size.Height, 1);
    }

    /// <summary>
    /// An 8px block clamped to two lines, one word to a line, with a link with 10px of top padding
    /// on the second: the block ends at that line's word, 32px tall. The line was measured to the
    /// bottom of the link's rectangle, which reached 10px below the word, and the block was 42px.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Clamped_Block_Ends_At_The_Words_Of_Its_Last_Line()
    {
        var (root, block) = Block(width: "8px");
        block.LineClamp = "2";
        Word(block);
        var link = Inline(block, link => link.PaddingTop = "10px");
        var second = Word(link);
        Word(block);

        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(block.Location.Y + 16, second.Top, 1);
        Assert.Equal(32, block.Size.Height, 1);
    }

    /// <summary>
    /// A 40px inline-block aligned to the top of a line that also holds a link with 10px of top
    /// padding sits at the top of the line, with the words, and the block is as tall as it is with
    /// no padding on the link. The inline-block was placed at the top of the link's padding, and the
    /// line then moved 10px down to hold it.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Top_Aligned_Box_Aligns_With_The_Words_Not_The_Padding()
    {
        var (root, block, first, box) = TopAligned("10px");
        var (unpaddedRoot, unpadded, _, _) = TopAligned("0px");

        root.PerformLayout(root.LayoutEnvironment);
        unpaddedRoot.PerformLayout(unpaddedRoot.LayoutEnvironment);

        Assert.Equal(block.Location.Y, first.Top, 1);
        Assert.Equal(block.Location.Y, box.Location.Y, 1);
        Assert.Equal(unpadded.Size.Height, block.Size.Height, 1);
    }

    /// <summary>
    /// A block holding a word, a link with the given top padding holding a word, and an 8×40px
    /// inline-block aligned to the top of the line.
    /// </summary>
    private static (CssBox Root, CssBox Block, CssRect First, CssBox Box) TopAligned(string paddingTop)
    {
        var (root, block) = Block();
        var first = Word(block);
        var link = Inline(block, link => link.PaddingTop = paddingTop);
        Word(link);
        var box = new CssBox(block, new HtmlTag("span", false, null), BaseUrl)
        {
            Display = CssConstants.InlineBlock,
            Width = "8px",
            Height = "40px",
            VerticalAlign = CssConstants.Top,
        };
        return (root, block, first, box);
    }

    /// <summary>
    /// A 100px table cell aligned to its bottom, holding a link with 20px of top padding: the word
    /// ends at the cell's bottom, 84px down. The line was moved 20px down and then measured with
    /// the padding, and the word was 64px down.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Bottom_Aligned_Cell_Ends_At_The_Words()
    {
        var (root, cell, word) = Cell(CssConstants.Bottom, paddingTop: 20, paddingBottom: 0);

        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(84, word.Top - cell.Location.Y, 1);
    }

    /// <summary>
    /// Controls, which pass before and after: in a 100px table cell aligned to its middle, the word
    /// is 42px down with 20px of padding above it, 20px below it or 10px each side, and in one
    /// aligned to its bottom, 84px down with 20px of padding below it. The middle ones passed
    /// before because the padding the line was moved down by was measured below it again.
    /// </summary>
    [Theory(Timeout = 600000)]
    [InlineData("middle", 20, 0, 42)]
    [InlineData("middle", 0, 20, 42)]
    [InlineData("middle", 10, 10, 42)]
    [InlineData("bottom", 0, 20, 84)]
    public void Control_An_Aligned_Cell_Places_The_Words(string verticalAlign, int paddingTop, int paddingBottom, float top)
    {
        var (root, cell, word) = Cell(verticalAlign, paddingTop, paddingBottom);

        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(top, word.Top - cell.Location.Y, 1);
    }

    /// <summary>
    /// Control, which passes before and after: a link with 30px of bottom padding leaves the block
    /// one line, 16px tall, with the word at its top.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Control_Bottom_Padding_Leaves_The_Line_Where_It_Is()
    {
        var (root, block) = Block();
        var link = Inline(block, link => link.PaddingBottom = "30px");
        var word = Word(link);

        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(16, block.Size.Height, 1);
        Assert.Equal(block.Location.Y, word.Top, 1);
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

    /// <summary>
    /// A table in a root with one 100px cell, unpadded and aligned by <paramref name="verticalAlign"/>,
    /// holding a link with the given vertical padding that holds a word.
    /// </summary>
    private static (CssBox Root, CssBox Cell, CssRect Word) Cell(string verticalAlign, int paddingTop, int paddingBottom)
    {
        var root = Root();
        var table = new CssBox(root, new HtmlTag("table", false, null), BaseUrl) { Display = "table", Width = "320px" };
        var body = new CssBox(table, new HtmlTag("tbody", false, null), BaseUrl) { Display = "table-row-group" };
        var row = new CssBox(body, new HtmlTag("tr", false, null), BaseUrl) { Display = "table-row" };
        var cell = new CssBox(row, new HtmlTag("td", false, null), BaseUrl)
        {
            Display = "table-cell",
            Height = "100px",
            VerticalAlign = verticalAlign,
        };
        var link = Inline(cell, link =>
        {
            link.PaddingTop = $"{paddingTop}px";
            link.PaddingBottom = $"{paddingBottom}px";
        });
        return (root, cell, Word(link));
    }

    /// <summary>An inline box in <paramref name="parent"/>, styled by <paramref name="style"/>.</summary>
    private static CssBox Inline(CssBox parent, Action<CssBox> style)
    {
        var box = new CssBox(parent, new HtmlTag("a", false, null), BaseUrl) { Display = "inline" };
        style(box);
        return box;
    }

    /// <summary>An anonymous inline box in <paramref name="parent"/> holding one word, and the word.</summary>
    private static CssRect Word(CssBox parent)
    {
        var text = new CssBox(parent, null, BaseUrl) { Display = "inline", Text = "X".AsMemory() };
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
