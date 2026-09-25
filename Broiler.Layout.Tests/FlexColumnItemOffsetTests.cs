using System;
using System.Drawing;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// CSS Flexbox §9.4 and §9.5: each item of a single-line column flex container starts at the
/// container's inline-start content edge, plus its own start margin, whatever the item before it
/// has on its right.
/// </summary>
/// <remarks>
/// A column container flows its items through line layout, one item per line. After each item the
/// line break reset the inline cursor to the content edge, and then the loop added the item's right
/// margin, border and padding to it, as it does for a box that stays on its line. So every item
/// after one with any of those started that far to the right. A stretched item kept that start and
/// its full width, so it overflowed the container by the same amount. An item as wide as the
/// container no longer fit on its line and was wrapped a line's descent further down. The page
/// that showed it had a hero section with 24px of padding, and the section after it sat 24px
/// right of where every browser puts it.
/// </remarks>
public sealed class FlexColumnItemOffsetTests
{
    /// <summary>The container's left padding: its inline-start content edge.</summary>
    private const float ContentLeft = 10;

    private static readonly Uri BaseUrl = new("file:///flex-column-offset.html");

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

    /// <summary>An inline box holding one 8×16 word.</summary>
    private static void Word(CssBox parent)
    {
        var text = Box(parent, "inline", "span");
        text.Words.Add(new CssRectWord(text, "X", false, false));
    }

    /// <summary>24px of right padding, border or margin.</summary>
    private static void RightEdge(CssBox box, string edge)
    {
        switch (edge)
        {
            case "padding":
                box.PaddingRight = "24px";
                break;
            case "border":
                box.BorderRightWidth = "24px";
                box.BorderRightStyle = "solid";
                break;
            case "margin":
                box.MarginRight = "24px";
                break;
        }
    }

    /// <summary>
    /// A column flex container with 10px of padding on both sides, holding three one-word
    /// sections: the first with 24px of <paramref name="edge"/> on its right.
    /// </summary>
    private static (CssBox Container, CssBox First, CssBox Next, CssBox Third) Column(
        string edge = "padding", string display = "flex", string direction = "column",
        string? alignItems = null, string? containerWidth = null, Action<CssBox>? styleNext = null)
    {
        var root = Root();
        var container = Box(root, display, "main");
        container.FlexDirection = direction;
        container.PaddingLeft = "10px";
        container.PaddingRight = "10px";
        if (alignItems != null)
            container.AlignItems = alignItems;
        if (containerWidth != null)
            container.Width = containerWidth;

        var first = Box(container, "block", "section");
        RightEdge(first, edge);
        Word(first);

        var next = Box(container, "block", "section");
        styleNext?.Invoke(next);
        Word(next);

        var third = Box(container, "block", "section");
        Word(third);

        root.PerformLayout(root.LayoutEnvironment);
        return (container, first, next, third);
    }

    /// <summary>
    /// The item after one with a right edge starts at the content edge and, stretched, ends at the
    /// other one; it started 24px right and overflowed the container by 24px.
    /// </summary>
    [Theory]
    [InlineData("padding")]
    [InlineData("border")]
    [InlineData("margin")]
    public void The_Item_After_One_With_A_Right_Edge_Starts_At_The_Content_Edge(string edge)
    {
        var (container, _, next, third) = Column(edge);
        double contentRight = container.Location.X + container.Size.Width - 10;

        Assert.Equal(ContentLeft, next.Location.X, 1);
        Assert.Equal(contentRight, next.Location.X + next.Size.Width, 1);
        Assert.Equal(ContentLeft, third.Location.X, 1);
    }

    /// <summary><c>align-items: flex-start</c> keeps the item's width, and its start.</summary>
    [Fact(Timeout = 600000)]
    public void A_Start_Aligned_Item_Starts_At_The_Content_Edge()
    {
        var (_, _, next, _) = Column(alignItems: "flex-start");

        Assert.Equal(ContentLeft, next.Location.X, 1);
        Assert.Equal(8, next.Size.Width, 1);
    }

    /// <summary>The item's own start margin still counts: it starts 5px in from the content edge.</summary>
    [Fact(Timeout = 600000)]
    public void The_Item_Keeps_Its_Own_Start_Margin()
    {
        var (_, _, next, _) = Column(styleNext: next => next.MarginLeft = "5px");

        Assert.Equal(ContentLeft + 5, next.Location.X, 1);
    }

    /// <summary>
    /// An item as wide as the container's content fits on its line: it follows the first item
    /// directly, and so does the item after it. It was wrapped 3.2px, a line's descent, lower.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void An_Item_As_Wide_As_The_Container_Stays_On_Its_Line()
    {
        var (container, first, next, third) = Column(styleNext: next => next.Width = "100%");

        Assert.Equal(ContentLeft, next.Location.X, 1);
        Assert.Equal(first.Location.Y + first.Size.Height, next.Location.Y, 1);
        Assert.Equal(next.Location.Y + next.Size.Height, third.Location.Y, 1);
        Assert.Equal(3 * 16, container.Size.Height, 1);
    }

    /// <summary>An inline-level column flex container lays its items out the same way.</summary>
    [Fact(Timeout = 600000)]
    public void An_Inline_Flex_Column_Starts_Its_Items_At_The_Content_Edge()
    {
        var (container, _, next, third) = Column(display: "inline-flex", containerWidth: "600px");

        Assert.Equal(container.Location.X + ContentLeft, next.Location.X, 1);
        Assert.Equal(container.Location.X + ContentLeft, third.Location.X, 1);
    }

    /// <summary>
    /// Controls: a centred item is placed from the content edge again afterwards, and a
    /// <c>column-reverse</c> container places its items itself. Both were already right.
    /// </summary>
    [Theory]
    [InlineData("column", "center")]
    [InlineData("column-reverse", null)]
    public void Items_Placed_From_The_Content_Edge_Afterwards_Were_Already_Right(string direction, string? alignItems)
    {
        var (container, _, next, _) = Column(direction: direction, alignItems: alignItems);
        double contentWidth = container.Size.Width - 2 * ContentLeft;
        double expected = alignItems == "center"
            ? ContentLeft + (contentWidth - next.Size.Width) / 2
            : ContentLeft;

        Assert.Equal(expected, next.Location.X, 1);
    }

    /// <summary>
    /// Control: boxes that share a line still follow each other's right edges. An inline-block
    /// with 24px of right padding and 6px of right margin is 32px wide, so the next one starts at 38.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Boxes_Sharing_A_Line_Still_Start_After_The_Right_Edge_Before_Them()
    {
        var root = Root();
        var line = Box(root, "block");
        var first = Box(line, "inline-block", "span");
        first.PaddingRight = "24px";
        first.MarginRight = "6px";
        Word(first);
        var next = Box(line, "inline-block", "span");
        Word(next);

        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(32, first.Size.Width, 1);
        Assert.Equal(first.Location.X + 32 + 6, next.Location.X, 1);
        Assert.Equal(first.Location.Y, next.Location.Y, 1);
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
