using System;
using System.Drawing;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// An inline-block lays its content out on lines of its own and is not part of them: its padding
/// and border are around those lines, and the inline boxes it sits in wrap it on the line it sits
/// on.
/// </summary>
/// <remarks>
/// <para>
/// <c>CssLineBox.UpdateRectangle</c> gives each inline box a rectangle around its words on a line
/// and bubbles it into the inline boxes it sits in. It bubbled past the block the line belongs to
/// whenever that block was inline-level too, an inline-block or an absolutely positioned inline box
/// laid out as a block, and so gave the block a rectangle on its own line, reaching out to its
/// border edge. Its lines then reached above its content box by its top padding and border, and
/// <c>CreateLineBoxes</c> moved them down by as much, the box with them: a
/// <c>display: inline-block; padding: 40px</c> box holding a word came out 40px low and 139.2px
/// tall, where it is 96px tall at the top of its line.
/// </para>
/// <para>
/// The inline boxes around the inline-block, a link wrapping it, got their only rectangle the same
/// way, on the inline-block's lines, where the box's content was before its line was aligned. They
/// now get it on the line the inline-block sits on, around its border box, once that line is
/// settled.
/// </para>
/// <para>
/// Each tree is built the way the parser builds text: a word is in an anonymous inline box, and is
/// 8px wide and 16px tall. The block holding everything is 320px wide.
/// </para>
/// </remarks>
public sealed class InlineBlockOwnLinesTests
{
    private static readonly Uri BaseUrl = new("file:///inline-block-own-lines.html");

    /// <summary>
    /// A padded inline-block holding a word is its padding and border around its one line, at the
    /// top of the line it sits on, with the word inside its padding: 96px tall with
    /// <c>padding: 40px</c>, 56px with 10px above and 30px below, and 46px with 10px of padding
    /// inside a 5px border. They came out 139.2px, 69.2px and 64.2px tall, as low as their top
    /// padding and border.
    /// </summary>
    [Theory]
    [InlineData(40, 40, 0, 96)]
    [InlineData(10, 30, 0, 56)]
    [InlineData(10, 10, 5, 46)]
    public void A_Padded_Inline_Block_Is_Its_Padding_Around_Its_Line(
        int paddingTop, int paddingBottom, int border, float height)
    {
        var (root, block) = Block();
        var box = InlineBlock(block, $"{paddingTop}px");
        box.PaddingBottom = $"{paddingBottom}px";
        if (border > 0)
        {
            box.BorderTopWidth = box.BorderBottomWidth = box.BorderLeftWidth = box.BorderRightWidth = $"{border}px";
            box.BorderTopStyle = box.BorderBottomStyle = box.BorderLeftStyle = box.BorderRightStyle = "solid";
        }

        var word = Word(box);

        Layout(root);

        Assert.Equal(block.Location.Y, box.Location.Y, 1);
        Assert.Equal(height, box.ActualBottom - box.Location.Y, 1);
        Assert.Equal(box.Location.Y + border + paddingTop, word.Top, 1);
    }

    /// <summary>
    /// An unpadded inline-block holding a word is as tall as its line, 16px, alone and inside a
    /// link. It was 19.2px: <c>CreateLineBoxes</c> gives the lines of a box whose kind the parser
    /// leaves unset, as it leaves a span's, the strut's descent below each inline-block on them,
    /// and the inline-block was on its own.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void An_Unpadded_Inline_Block_Is_As_Tall_As_Its_Line(bool inLink)
    {
        var (root, block) = Block();
        var box = InlineBlock(inLink ? Link(block) : block, "0");
        var word = Word(box);

        Layout(root);

        Assert.Equal(16, box.ActualBottom - box.Location.Y, 1);
        Assert.Equal(box.Location.Y, word.Top, 1);
    }

    /// <summary>
    /// A padded inline-block that <c>vertical-align: middle</c> raises above its line is still its
    /// 96px, with its word 40px down it. It was 139.2px tall.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Raised_Padded_Inline_Block_Keeps_Its_Word_Inside_Its_Padding()
    {
        var (root, block) = Block();
        var box = InlineBlock(block, "40px");
        box.VerticalAlign = "middle";
        var word = Word(box);

        Layout(root);

        Assert.Equal(96, box.ActualBottom - box.Location.Y, 1);
        Assert.Equal(box.Location.Y + 40, word.Top, 1);
    }

    /// <summary>
    /// An absolutely positioned inline box, laid out as a block, lays its word out on a line of its
    /// own too: with 10px of padding it is 36px tall, with its word 10px down it. It was 46px, with
    /// the word 20px down.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void An_Absolutely_Positioned_Inline_Box_Is_Its_Padding_Around_Its_Line()
    {
        var (root, block) = Block();
        Word(block);
        var link = Link(block, "10px");
        link.Position = "absolute";
        var word = Word(link);

        Layout(root);

        Assert.Equal(36, link.Bounds.Height, 1);
        Assert.Equal(link.Bounds.Y + 10, word.Top, 1);
    }

    /// <summary>
    /// A link holding only an inline-block, after a word, has one rectangle, on the line the
    /// inline-block sits on, exactly around its border box: in a left-aligned line, a centred one,
    /// and a right-to-left one. It had one only on the inline-block's own line, where the
    /// inline-block was before its line was aligned: at x=8 in the centred line, which had moved
    /// the inline-block to 150.
    /// </summary>
    [Theory]
    [InlineData(null, 8)]
    [InlineData("center", 150)]
    [InlineData("rtl", 292)]
    public void A_Link_Around_An_Inline_Block_Has_Its_Rectangle_On_The_Line(string? alignment, float x)
    {
        var (root, block) = Block();
        if (alignment == "rtl")
            block.Direction = "rtl";
        else if (alignment != null)
            block.TextAlign = alignment;

        Word(block);
        var link = Link(block);
        var box = InlineBlock(link, "10px");
        Word(box);

        Layout(root);

        var linkRectangle = OnLineOf(block, link);
        AssertRectangle(x, 0, 28, 36, OnLineOf(block, box));
        AssertRectangle(x, 0, 28, 36, linkRectangle);
        Assert.Equal(block.Location.Y, box.Location.Y, 1);
    }

    /// <summary>
    /// A link holding a word, an inline-block and another word has one rectangle on the line, around
    /// all three: from x=8 to 52, and as tall as the inline-block. It had one around the words
    /// only, 16px tall, and another on the inline-block's own line.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Link_Holding_Words_And_An_Inline_Block_Has_One_Rectangle_Around_Them()
    {
        var (root, block) = Block();
        Word(block);
        var link = Link(block);
        Word(link);
        var box = InlineBlock(link, "10px");
        Word(box);
        Word(link);

        Layout(root);

        AssertRectangle(8, 0, 44, 36, OnLineOf(block, link));
        Assert.Equal(36, box.ActualBottom - box.Location.Y, 1);
    }

    /// <summary>
    /// A link's own 5px of padding around an inline-block is around the inline-block's border box,
    /// and does not move the line: the inline-block is at the top of the block. An inline box's
    /// padding is left out of the line (CSS 2.1 §10.6.1). It moved the inline-block 15px down and
    /// made it 49.2px tall.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Padded_Link_Around_An_Inline_Block_Does_Not_Move_It()
    {
        var (root, block) = Block();
        Word(block);
        var link = Link(block, "5px");
        var box = InlineBlock(link, "10px");
        Word(box);

        Layout(root);

        Assert.Equal(block.Location.Y, box.Location.Y, 1);
        Assert.Equal(36, box.ActualBottom - box.Location.Y, 1);
        AssertRectangle(8, -5, 38, 46, OnLineOf(block, link));
    }

    /// <summary>
    /// A relative offset moves an inline-block and not the link around it (CSS 2.1 §9.4.3): the link
    /// wraps it where the flow put it, at x=8, while it is drawn 3px right and 5px down. The link had
    /// no rectangle on the line.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Link_Wraps_A_Relatively_Positioned_Inline_Block_Where_The_Flow_Put_It()
    {
        var (root, block) = Block();
        Word(block);
        var link = Link(block);
        var box = InlineBlock(link, "10px");
        box.Position = "relative";
        box.Left = "3px";
        box.Top = "5px";
        Word(box);

        Layout(root);

        AssertRectangle(8, 0, 28, 36, OnLineOf(block, link));
        Assert.Equal(11, box.Location.X, 1);
        Assert.Equal(block.Location.Y + 5, box.Location.Y, 1);
    }

    /// <summary>
    /// A relatively positioned link holding an inline-block is the containing block of an absolutely
    /// positioned box in it: <c>top: 0; left: 0</c> puts that box at the link's top left corner,
    /// which is the inline-block's, at x=8 and the top of the line. It was 10px lower, where the
    /// link's rectangle on the inline-block's own line was.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Link_Around_An_Inline_Block_Contains_An_Absolutely_Positioned_Box()
    {
        var (root, block) = Block();
        Word(block);
        var link = Link(block);
        link.Position = "relative";
        var box = InlineBlock(link, "10px");
        Word(box);
        var marker = new CssBox(link, new HtmlTag("i", false, null), BaseUrl)
        {
            Display = "block",
            Position = "absolute",
            Top = "0",
            Left = "0",
            Width = "4px",
            Height = "4px",
        };

        Layout(root);

        Assert.Equal(8, marker.Location.X, 1);
        Assert.Equal(block.Location.Y, marker.Location.Y, 1);
    }

    /// <summary>
    /// Control, which passes before and after: a block with <c>padding: 40px</c> holding a word is
    /// 96px tall, with the word 40px down it.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Control_A_Padded_Block_Is_Its_Padding_Around_Its_Line()
    {
        var (root, block) = Block();
        var box = new CssBox(block, new HtmlTag("div", false, null), BaseUrl) { Display = "block" };
        box.PaddingTop = box.PaddingBottom = box.PaddingLeft = box.PaddingRight = "40px";
        var word = Word(box);

        Layout(root);

        Assert.Equal(96, box.ActualBottom - box.Location.Y, 1);
        Assert.Equal(box.Location.Y + 40, word.Top, 1);
    }

    /// <summary>
    /// Control, which passes before and after: an inline flex container with 10px of padding
    /// holding a word, whose item is a block of its own, is 36px tall at the top of the line.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Control_A_Padded_Inline_Flex_Container_Holding_A_Word()
    {
        var (root, block) = Block();
        var box = InlineBlock(block, "10px");
        box.Display = "inline-flex";
        var word = Word(box);

        Layout(root);

        Assert.Equal(block.Location.Y, box.Location.Y, 1);
        Assert.Equal(36, box.ActualBottom - box.Location.Y, 1);
        Assert.Equal(box.Location.Y + 10, word.Top, 1);
    }

    /// <summary>
    /// Control, which passes before and after: a link with 10px of padding holding a word has a
    /// rectangle on the line around the word and its padding.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Control_A_Padded_Link_Holding_A_Word_Has_Its_Rectangle_Around_It()
    {
        var (root, block) = Block();
        Word(block);
        var link = Link(block, "10px");
        var word = Word(link);

        Layout(root);

        AssertRectangle(word.Left - 10, (float)word.Top - 10, 28, 36, OnLineOf(block, link));
    }

    /// <summary>The rectangle <paramref name="box"/> has on a line of <paramref name="block"/>.</summary>
    private static RectangleF OnLineOf(CssBox block, CssBox box) =>
        Assert.Single(box.Rectangles, entry => entry.Key.OwnerBox == block).Value;

    private static void AssertRectangle(double x, double y, float width, float height, RectangleF actual)
    {
        Assert.Equal(x, actual.X, 1);
        Assert.Equal(y, actual.Y, 1);
        Assert.Equal(width, actual.Width, 1);
        Assert.Equal(height, actual.Height, 1);
    }

    /// <summary>A 320px block, the only box in the root.</summary>
    private static (CssBox Root, CssBox Block) Block()
    {
        var root = new CssBox(null, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = "block",
            Location = new PointF(0, 0),
            Size = new SizeF(1024, 768),
            LayoutEnvironment = new FakeLayoutEnvironment(),
        };

        return (root, new CssBox(root, new HtmlTag("div", false, null), BaseUrl) { Display = "block", Width = "320px" });
    }

    /// <summary>An inline-block in <paramref name="parent"/> with <paramref name="padding"/> on every side.</summary>
    private static CssBox InlineBlock(CssBox parent, string padding)
    {
        var box = new CssBox(parent, new HtmlTag("span", false, null), BaseUrl) { Display = "inline-block" };
        box.PaddingTop = box.PaddingBottom = box.PaddingLeft = box.PaddingRight = padding;
        return box;
    }

    /// <summary>A link in <paramref name="parent"/> with <paramref name="padding"/> on every side.</summary>
    private static CssBox Link(CssBox parent, string padding = "0")
    {
        var link = new CssBox(parent, new HtmlTag("a", false, null), BaseUrl) { Display = "inline" };
        link.PaddingTop = link.PaddingBottom = link.PaddingLeft = link.PaddingRight = padding;
        return link;
    }

    /// <summary>
    /// An anonymous inline box in <paramref name="parent"/> holding one word, as the parser holds
    /// text, and the word.
    /// </summary>
    private static CssRect Word(CssBox parent)
    {
        var text = new CssBox(parent, null, BaseUrl) { Display = "inline", Text = "X".AsMemory() };
        text.ParseToWords();
        return Assert.Single(text.Words);
    }

    private static void Layout(CssBox root)
    {
        FlexGridItemBlockification.Generate(root);
        root.PerformLayout(root.LayoutEnvironment);
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
