using System;
using System.Drawing;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// CSS Flexbox §9.9.1: a row flex container's items sit side by side, so its max-content width is
/// the sum of their max-content contributions and gaps. Its min-content width is the same sum over
/// their min-content contributions when it is single-line, and the largest of them when it wraps.
/// </summary>
/// <remarks>
/// Every intrinsic measurement took a flex container for a block container. Its items are
/// blockified, so each started a line of its own, and a row measured as wide as its widest item.
/// An auto-width inline-flex row holding items 8, 20 and 32px wide came out 32px wide, and its
/// items were squeezed into that and wrapped, or ran out of it. The same held for a floated or
/// absolutely positioned flex row, one sized by an intrinsic keyword, and a table cell or a flex
/// row holding one. Each test here has those three items, of 1, 2 and 3 words, 8 px each and 4 px
/// apart.
/// </remarks>
public sealed class FlexRowIntrinsicWidthTests
{
    private static readonly Uri BaseUrl = new("file:///flex-row-intrinsic.html");

    private static CssBox Root() =>
        new(null, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = "block",
            Location = new PointF(0, 0),
            Size = new SizeF(1024, 768),
            LayoutEnvironment = new FakeLayoutEnvironment(),
        };

    private static CssBox Box(CssBox parent, string display, string tag = "div") =>
        new(parent, new HtmlTag(tag, false, null), BaseUrl) { Display = display };

    /// <summary>A block item holding <paramref name="words"/> words.</summary>
    private static CssBox Item(CssBox parent, int words)
    {
        var item = Box(parent, "block", "section");
        var text = Box(item, "inline", "span");
        for (int i = 0; i < words; i++)
            text.Words.Add(new CssRectWord(text, "X", i > 0, false));
        return item;
    }

    /// <summary>
    /// A container of <paramref name="display"/> on a line of its own, styled by
    /// <paramref name="style"/>, holding items 8, 20 and 32 px wide at max-content.
    /// </summary>
    private static (CssBox Container, CssBox A, CssBox B, CssBox C) Lay(
        string display = "inline-flex", Action<CssBox>? style = null)
    {
        var root = Root();
        var line = Box(root, "block");
        var container = Box(line, display, display.StartsWith("inline", StringComparison.Ordinal) ? "span" : "div");
        style?.Invoke(container);
        var items = (A: Item(container, 1), B: Item(container, 2), C: Item(container, 3));

        root.PerformLayout(root.LayoutEnvironment);
        return (container, items.A, items.B, items.C);
    }

    private static void AssertOnOneLine(CssBox a, CssBox b, CssBox c)
    {
        Assert.Equal(a.Location.X + a.Size.Width, b.Location.X, 1);
        Assert.Equal(b.Location.X + b.Size.Width, c.Location.X, 1);
        Assert.Equal(a.Location.Y, c.Location.Y, 1);
        Assert.Equal(16, c.Size.Height, 1);
    }

    /// <summary>The row is 60px, and its items sit at their own widths on one line.</summary>
    [Fact(Timeout = 600000)]
    public void An_Auto_Width_Inline_Flex_Row_Is_As_Wide_As_Its_Items()
    {
        var (row, a, b, c) = Lay();

        Assert.Equal(60, row.Size.Width, 1);
        Assert.Equal(8, a.Size.Width, 1);
        Assert.Equal(20, b.Size.Width, 1);
        Assert.Equal(32, c.Size.Width, 1);
        AssertOnOneLine(a, b, c);
    }

    /// <summary>
    /// Gaps, the items' margins and the row's own padding add to it: a 10px column gap twice, 5px
    /// of margin on each side of the middle item, or 10px of padding on each side of the row.
    /// </summary>
    [Theory]
    [InlineData("gap", 80)]
    [InlineData("margin", 70)]
    [InlineData("padding", 80)]
    public void Gaps_Margins_And_Padding_Add_To_Its_Width(string variant, float width)
    {
        var root = Root();
        var line = Box(root, "block");
        var row = Box(line, "inline-flex", "span");
        if (variant == "gap")
            row.ColumnGap = "10px";
        if (variant == "padding")
            row.PaddingLeft = row.PaddingRight = "10px";
        Item(row, 1);
        var b = Item(row, 2);
        if (variant == "margin")
            b.MarginLeft = b.MarginRight = "5px";
        var c = Item(row, 3);

        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(width, row.Size.Width, 1);
        Assert.Equal(16, c.Size.Height, 1);
    }

    /// <summary>
    /// Min-content is the sum of the items' min-content widths, 8 px each, for a single-line row,
    /// and the largest of them for a wrapping one. Max-content is 60 px either way.
    /// </summary>
    [Theory]
    [InlineData("nowrap", 24)]
    [InlineData("wrap", 8)]
    public void Min_Content_Is_Summed_Unless_The_Row_Wraps(string wrap, float minContent)
    {
        var (row, _, _, _) = Lay(style: row => row.FlexWrap = wrap);

        row.GetMinMaxWidth(out double min, out double max);

        Assert.Equal(minContent, min, 1);
        Assert.Equal(60, max, 1);
    }

    /// <summary>A floated or absolutely positioned flex row shrinks to fit its items: 60px.</summary>
    [Theory]
    [InlineData("float")]
    [InlineData("absolute")]
    public void A_Floated_Or_Absolutely_Positioned_Flex_Row_Is_As_Wide_As_Its_Items(string placement)
    {
        var (row, a, b, c) = Lay("flex", row =>
        {
            if (placement == "float")
                row.Float = "left";
            else
                row.Position = "absolute";
        });

        Assert.Equal(60, row.Size.Width, 1);
        AssertOnOneLine(a, b, c);
    }

    /// <summary>
    /// A flex row sized by an intrinsic keyword takes it from its items: 60px at max-content and
    /// fit-content, 24px at min-content, and 8px at min-content when it wraps.
    /// </summary>
    [Theory]
    [InlineData("max-content", "nowrap", 60)]
    [InlineData("fit-content", "nowrap", 60)]
    [InlineData("min-content", "nowrap", 24)]
    [InlineData("min-content", "wrap", 8)]
    public void An_Intrinsic_Keyword_Width_Comes_From_Its_Items(string keyword, string wrap, float width)
    {
        var (row, _, _, _) = Lay("flex", row =>
        {
            row.Width = keyword;
            row.FlexWrap = wrap;
        });

        Assert.Equal(width, row.Size.Width, 1);
    }

    /// <summary>A table cell holding a flex row is as wide as the row: 60px.</summary>
    [Fact(Timeout = 600000)]
    public void A_Table_Cell_Holding_A_Flex_Row_Is_As_Wide_As_The_Row()
    {
        var root = Root();
        var table = Box(root, "table", "table");
        var body = Box(table, "table-row-group", "tbody");
        var tableRow = Box(body, "table-row", "tr");
        var cell = Box(tableRow, "table-cell", "td");
        var row = Box(cell, "flex");
        Item(row, 1);
        Item(row, 2);
        Item(row, 3);

        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(60, cell.Size.Width, 1);
        Assert.Equal(60, row.Size.Width, 1);
    }

    /// <summary>
    /// A flex row that is an item of another counts as one item of its own width: the inner row's
    /// two items make 28px, and with the 32px item beside it the outer row is 60px.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Nested_Flex_Row_Is_One_Item_As_Wide_As_Its_Own_Items()
    {
        var root = Root();
        var line = Box(root, "block");
        var outer = Box(line, "inline-flex", "span");
        var inner = Box(outer, "flex");
        Item(inner, 1);
        Item(inner, 2);
        Item(outer, 3);

        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(28, inner.Size.Width, 1);
        Assert.Equal(60, outer.Size.Width, 1);
    }

    /// <summary>
    /// Controls: the items of an inline-flex column, or blocks in an inline-block, stack one per
    /// line, so the box is as wide as the widest, 32px, as it was.
    /// </summary>
    [Theory]
    [InlineData("inline-flex column")]
    [InlineData("inline-block")]
    public void Boxes_That_Stack_Their_Children_Were_Already_As_Wide_As_The_Widest(string kind)
    {
        var (box, a, _, c) = Lay(kind == "inline-block" ? "inline-block" : "inline-flex", box =>
        {
            if (kind == "inline-flex column")
                box.FlexDirection = "column";
        });

        Assert.Equal(32, box.Size.Width, 1);
        Assert.Equal(a.Location.Y + 32, c.Location.Y, 1);
    }

    // Minimal ILayoutEnvironment: every word 8px wide and 16px tall, a space 4px, a 1024×768 viewport.
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
