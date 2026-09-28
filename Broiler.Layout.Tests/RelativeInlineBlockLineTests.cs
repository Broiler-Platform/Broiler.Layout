using System;
using System.Drawing;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// A relative offset moves an inline-block, an inline flex container or a flex item the flow
/// places whole, and nothing around it: the line is laid out as if the box were where the flow put
/// it.
/// </summary>
/// <remarks>
/// <para>
/// CSS2.1 §9.4.3. The offset was applied as the flow placed the box, and the line was then
/// measured and aligned with the box where the offset had put it: <c>top: 5px</c> made the line
/// 5px taller and stood an inline-block beside the box 5px lower, and <c>top: -5px</c> moved the
/// block's lines 5px down. Vertical alignment, which places an inline-block afresh, and a
/// right-to-left line dropped the offset.
/// </para>
/// <para>
/// Each case is laid out twice, with the offset and without it, and compared: words here are
/// 8×16px, in a 320px block, and the boxes are 10×20px.
/// </para>
/// </remarks>
public sealed class RelativeInlineBlockLineTests
{
    private static readonly Uri BaseUrl = new("file:///relative-inline-block-line.html");

    /// <summary>
    /// After a word, an inline-block offset 5px or 30px down, or 5px up, is that far from where it
    /// is with no offset, and the block and the word are where they are with no offset. The block
    /// was 5px and 30px taller, and 5px up moved the word 5px down.
    /// </summary>
    [Theory]
    [InlineData("5px", 5)]
    [InlineData("30px", 30)]
    [InlineData("-5px", -5)]
    public void An_Offset_Inline_Block_Leaves_Its_Line_As_It_Was(string top, float dy)
    {
        var offset = Line(CssConstants.InlineBlock, box => box.Top = top);
        var still = Line(CssConstants.InlineBlock, _ => { });

        Assert.Equal(still.Block.Size.Height, offset.Block.Size.Height, 1);
        Assert.Equal(still.Word.Top, offset.Word.Top, 1);
        Assert.Equal(still.Box.Location.Y + dy, offset.Box.Location.Y, 1);
        Assert.Equal(20, offset.Box.Size.Height, 1);
    }

    /// <summary>
    /// An inline flex container offset 5px down leaves the block as tall as with no offset. It was
    /// 5px taller.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void An_Offset_Inline_Flex_Container_Leaves_Its_Line_As_It_Was()
    {
        var offset = Line("inline-flex", box => box.Top = "5px");
        var still = Line("inline-flex", _ => { });

        Assert.Equal(still.Block.Size.Height, offset.Block.Size.Height, 1);
        Assert.Equal(still.Box.Location.Y + 5, offset.Box.Location.Y, 1);
    }

    /// <summary>
    /// An inline-block beside one offset 5px down stays where it is with no offset, and the offset
    /// one is 5px below that. It stood on the offset one's bottom, 5px lower.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void An_Inline_Block_Beside_An_Offset_One_Stays_Where_It_Was()
    {
        var offset = Line(CssConstants.InlineBlock, box => box.Top = "5px", sibling: true);
        var still = Line(CssConstants.InlineBlock, _ => { }, sibling: true);

        Assert.Equal(still.Sibling!.Location.Y, offset.Sibling!.Location.Y, 1);
        Assert.Equal(still.Box.Location.Y + 5, offset.Box.Location.Y, 1);
        Assert.Equal(still.Block.Size.Height, offset.Block.Size.Height, 1);
    }

    /// <summary>
    /// A 40px inline-block aligned to the middle of the line and offset 5px down is 5px below where
    /// the alignment puts it with no offset. The alignment placed it afresh and dropped the offset.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Middle_Aligned_Inline_Block_Keeps_Its_Offset()
    {
        var offset = Line(CssConstants.InlineBlock, box => { box.VerticalAlign = "middle"; box.Height = "40px"; box.Top = "5px"; });
        var still = Line(CssConstants.InlineBlock, box => { box.VerticalAlign = "middle"; box.Height = "40px"; });

        Assert.Equal(still.Box.Location.Y + 5, offset.Box.Location.Y, 1);
        Assert.Equal(still.Block.Size.Height, offset.Block.Size.Height, 1);
    }

    /// <summary>
    /// On a right-to-left line, an inline-block offset by <c>left: 10px</c> is 10px right of where
    /// it is with no offset. The line dropped the offset.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Right_To_Left_Line_Keeps_The_Offset()
    {
        var offset = Line(CssConstants.InlineBlock, box => box.Left = "10px", direction: CssConstants.Rtl);
        var still = Line(CssConstants.InlineBlock, _ => { }, direction: CssConstants.Rtl);

        Assert.Equal(still.Box.Location.X + 10, offset.Box.Location.X, 1);
    }

    /// <summary>
    /// An inline-block holding a word, offset 5px down, keeps its height and is 5px below where it is
    /// with no offset, and the block is as tall as with no offset. The block was 5px taller.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void An_Offset_Inline_Block_Holding_A_Word_Keeps_Its_Height()
    {
        var offset = Line(CssConstants.InlineBlock, box => box.Top = "5px", holdsWord: true);
        var still = Line(CssConstants.InlineBlock, _ => { }, holdsWord: true);

        Assert.Equal(still.Box.Size.Height, offset.Box.Size.Height, 1);
        Assert.Equal(still.Box.Location.Y + 5, offset.Box.Location.Y, 1);
        Assert.Equal(still.Block.Size.Height, offset.Block.Size.Height, 1);
    }

    /// <summary>
    /// In a column flex container, the second of two 20px items offset 5px down is 5px below where
    /// it is with no offset, and the container is 40px tall, as with no offset. It was 45px.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void An_Offset_Column_Item_Leaves_Its_Container_As_Tall_As_It_Was()
    {
        var (offsetContainer, offsetItem) = Column(item => item.Top = "5px");
        var (stillContainer, stillItem) = Column(_ => { });

        Assert.Equal(40, stillContainer.Size.Height, 1);
        Assert.Equal(40, offsetContainer.Size.Height, 1);
        Assert.Equal(stillItem.Location.Y + 5, offsetItem.Location.Y, 1);
    }

    /// <summary>
    /// Control, which passes before and after: <c>left: 10px</c> moves an inline-block 10px right
    /// on a left-to-right line and leaves the block as tall as with no offset.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Control_A_Horizontal_Offset_On_A_Left_To_Right_Line()
    {
        var offset = Line(CssConstants.InlineBlock, box => box.Left = "10px");
        var still = Line(CssConstants.InlineBlock, _ => { });

        Assert.Equal(still.Box.Location.X + 10, offset.Box.Location.X, 1);
        Assert.Equal(still.Block.Size.Height, offset.Block.Size.Height, 1);
    }

    /// <summary>
    /// Control, which passes before and after: an absolutely positioned box at the top of an
    /// inline-block offset 5px down is at the inline-block's top, its containing block, which moves
    /// with it.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Control_An_Absolutely_Positioned_Box_Moves_With_Its_Offset_Container()
    {
        var offset = Line(CssConstants.InlineBlock, box => box.Top = "5px", holdsAbsolute: true);

        Assert.Equal(offset.Box.Location.Y, offset.Absolute!.Location.Y, 1);
    }

    private sealed record Laid(CssBox Block, CssRect Word, CssBox Box, CssBox? Sibling, CssBox? Absolute);

    /// <summary>
    /// A 320px block holding a word, a 10×20px inline-block when <paramref name="sibling"/>, and a
    /// 10×20px box of <paramref name="display"/>, relatively positioned and styled by
    /// <paramref name="style"/>, empty, or holding a word when <paramref name="holdsWord"/>, or an
    /// absolutely positioned 2×2px box at its top left when <paramref name="holdsAbsolute"/>.
    /// </summary>
    private static Laid Line(
        string display,
        Action<CssBox> style,
        bool sibling = false,
        bool holdsWord = false,
        bool holdsAbsolute = false,
        string? direction = null)
    {
        var root = Root();
        var block = new CssBox(root, new HtmlTag("div", false, null), BaseUrl) { Display = "block", Width = "320px" };
        if (direction != null)
            block.Direction = direction;

        var word = Word(block);

        CssBox? siblingBox = null;
        if (sibling)
            siblingBox = new CssBox(block, new HtmlTag("span", false, null), BaseUrl) { Display = CssConstants.InlineBlock, Width = "10px", Height = "20px" };

        var box = new CssBox(block, new HtmlTag("span", false, null), BaseUrl)
        {
            Display = display,
            Position = CssConstants.Relative,
            Width = "10px",
            Height = "20px",
        };

        if (holdsWord)
        {
            box.Height = "auto";
            Word(box);
        }

        CssBox? absolute = null;
        if (holdsAbsolute)
        {
            absolute = new CssBox(box, new HtmlTag("i", false, null), BaseUrl)
            {
                Display = "block",
                Position = CssConstants.Absolute,
                Top = "0",
                Left = "0",
                Width = "2px",
                Height = "2px",
            };
        }

        style(box);

        FlexGridItemBlockification.Generate(root);
        root.PerformLayout(root.LayoutEnvironment);
        return new Laid(block, word, box, siblingBox, absolute);
    }

    /// <summary>
    /// A 320px column flex container, aligning its items to its start, holding two 10×20px items,
    /// the second relatively positioned and styled by <paramref name="style"/>.
    /// </summary>
    private static (CssBox Container, CssBox Item) Column(Action<CssBox> style)
    {
        var root = Root();
        var container = new CssBox(root, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = "flex",
            FlexDirection = "column",
            AlignItems = "flex-start",
            Width = "320px",
        };

        _ = new CssBox(container, new HtmlTag("span", false, null), BaseUrl) { Display = "block", Width = "10px", Height = "20px" };
        var item = new CssBox(container, new HtmlTag("span", false, null), BaseUrl)
        {
            Display = "block",
            Position = CssConstants.Relative,
            Width = "10px",
            Height = "20px",
        };
        style(item);

        FlexGridItemBlockification.Generate(root);
        root.PerformLayout(root.LayoutEnvironment);
        return (container, item);
    }

    private static CssBox Root() => new(null, new HtmlTag("div", false, null), BaseUrl)
    {
        Display = "block",
        Location = new PointF(0, 0),
        Size = new SizeF(1024, 768),
        LayoutEnvironment = new FakeLayoutEnvironment(),
    };

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
