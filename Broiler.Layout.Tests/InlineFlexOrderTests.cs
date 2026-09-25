using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// CSS Flexbox §5.4: <c>order</c> places a flex item, and paints it, in order-modified document
/// order, and a grid container places its items in that order too. That holds for an
/// <c>inline-flex</c> or <c>inline-grid</c> container as much as for a block-level one.
/// </summary>
/// <remarks>
/// <c>LayoutBlockChildren</c> sorts a block-level flex or grid container's children into
/// order-modified document order before laying them out. A container laid out as an atomic
/// inline-level box goes through <c>CssLayoutEngine.FlowInlineBlock</c> instead, which never sorted
/// them. So <c>order</c> did nothing in an inline-flex row or column or in an inline-grid, nor in a
/// flex container laid out as an item of a column flex container when that container does not lay
/// it out again. Each test has three items, a, b and c, with <c>order: -1</c> on b; every browser
/// puts b first.
/// </remarks>
public sealed class InlineFlexOrderTests
{
    private static readonly Uri BaseUrl = new("file:///inline-flex-order.html");

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

    /// <summary>A block item with an id, holding <paramref name="words"/> 8×16 words 4px apart.</summary>
    private static CssBox Item(CssBox parent, string id, int words, string? order = null)
    {
        var item = new CssBox(parent, new HtmlTag("section", false, new Dictionary<string, string> { ["id"] = id }), BaseUrl)
        {
            Display = "block",
        };
        if (order != null)
            item.Order = order;

        var text = Box(item, "inline", "span");
        for (int i = 0; i < words; i++)
            text.Words.Add(new CssRectWord(text, "X", i > 0, false));
        return item;
    }

    /// <summary>
    /// a (8px wide), b (20px, <c>order: -1</c> unless <paramref name="bOrder"/> says otherwise) and c
    /// (32px) in <paramref name="container"/>.
    /// </summary>
    private static (CssBox A, CssBox B, CssBox C) Items(CssBox container, string? bOrder = "-1") =>
        (Item(container, "a", 1), Item(container, "b", 2, bOrder), Item(container, "c", 3));

    /// <summary>The ids of the container's children, in the order layout and painting walk them.</summary>
    private static string Ids(CssBox container) =>
        string.Join(",", container.Boxes.Select(box => box.HtmlTag?.TryGetAttribute("id")));

    /// <summary>A container of <paramref name="display"/> on a line of its own.</summary>
    private static CssBox OnALine(CssBox root, string display, string? direction = null, string? width = null)
    {
        var line = Box(root, "block");
        var container = Box(line, display, "span");
        if (direction != null)
            container.FlexDirection = direction;
        if (width != null)
            container.Width = width;
        return container;
    }

    /// <summary>An inline-flex row puts b first, then a and c after it; b was second.</summary>
    [Fact(Timeout = 600000)]
    public void An_Inline_Flex_Row_Lays_Its_Items_Out_In_Order()
    {
        var root = Root();
        var row = OnALine(root, "inline-flex", "row", "600px");
        var (a, b, c) = Items(row);

        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal("b,a,c", Ids(row));
        Assert.Equal(row.Location.X, b.Location.X, 1);
        Assert.Equal(b.Location.X + 20, a.Location.X, 1);
        Assert.Equal(a.Location.X + 8, c.Location.X, 1);
    }

    /// <summary>An inline-flex column stacks b on top.</summary>
    [Fact(Timeout = 600000)]
    public void An_Inline_Flex_Column_Stacks_Its_Items_In_Order()
    {
        var root = Root();
        var column = OnALine(root, "inline-flex", "column");
        var (a, b, c) = Items(column);

        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal("b,a,c", Ids(column));
        Assert.Equal(column.Location.Y, b.Location.Y, 1);
        Assert.Equal(b.Location.Y + 16, a.Location.Y, 1);
        Assert.Equal(a.Location.Y + 16, c.Location.Y, 1);
    }

    /// <summary>An inline-grid auto-places b in its first row.</summary>
    [Fact(Timeout = 600000)]
    public void An_Inline_Grid_Places_Its_Items_In_Order()
    {
        var root = Root();
        var grid = OnALine(root, "inline-grid");
        var (a, b, c) = Items(grid);

        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal("b,a,c", Ids(grid));
        Assert.Equal(grid.Location.Y, b.Location.Y, 1);
        Assert.Equal(b.Location.Y + 16, a.Location.Y, 1);
        Assert.Equal(a.Location.Y + 16, c.Location.Y, 1);
    }

    /// <summary>
    /// A 300px <c>display: flex</c> row that is an item of a column flex container is laid out as
    /// an atomic inline-level box, and its definite width means the column never lays it out
    /// again to stretch it. It puts b first too.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Flex_Container_Laid_Out_As_An_Item_Orders_Its_Own_Items()
    {
        var root = Root();
        var column = Box(root, "flex", "main");
        column.FlexDirection = "column";
        var row = Box(column, "flex");
        row.Width = "300px";
        var (a, b, _) = Items(row);

        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal("b,a,c", Ids(row));
        Assert.Equal(row.Location.X, b.Location.X, 1);
        Assert.Equal(b.Location.X + 20, a.Location.X, 1);
    }

    /// <summary>
    /// Items with equal <c>order</c> keep their document order: a and b share <c>order: 1</c>, so
    /// c, at the initial 0, comes first and a stays before b.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Equal_Orders_Keep_Document_Order()
    {
        var root = Root();
        var row = OnALine(root, "inline-flex", "row", "600px");
        var (a, _, _) = Items(row, bOrder: "1");
        a.Order = "1";

        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal("c,a,b", Ids(row));
    }

    /// <summary>Control: a block-level flex row already put b first.</summary>
    [Fact(Timeout = 600000)]
    public void A_Block_Level_Flex_Row_Already_Laid_Its_Items_Out_In_Order()
    {
        var root = Root();
        var row = Box(root, "flex");
        var (a, b, _) = Items(row);

        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal("b,a,c", Ids(row));
        Assert.Equal(row.Location.X, b.Location.X, 1);
        Assert.Equal(b.Location.X + 20, a.Location.X, 1);
    }

    /// <summary>Control: an inline-flex row whose items declare no <c>order</c> keeps document order.</summary>
    [Fact(Timeout = 600000)]
    public void Without_Order_An_Inline_Flex_Row_Keeps_Document_Order()
    {
        var root = Root();
        var row = OnALine(root, "inline-flex", "row", "600px");
        var (a, b, _) = Items(row, bOrder: null);

        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal("a,b,c", Ids(row));
        Assert.Equal(row.Location.X, a.Location.X, 1);
        Assert.Equal(a.Location.X + 8, b.Location.X, 1);
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
