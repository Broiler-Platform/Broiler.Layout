using System;
using System.Drawing;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// A flex container is as tall as its items, however short they are: its items are blockified
/// (CSS Display 3 §2.7) and lie in no line box, so no strut (CSS2.1 §10.8) holds a column to a
/// line-height.
/// </summary>
/// <remarks>
/// A column's items are placed one per line here, and each line was held to at least the
/// container's line-height. Each item starts where the one before ends, so that was invisible
/// between them, but it left the column a line below a last item shorter than a line: three 10px
/// items made a 36px column where browsers make it 30. Words are 8px wide and 16px tall, so a line
/// is 16px.
/// </remarks>
public sealed class FlexLineStrutTests
{
    private static readonly Uri BaseUrl = new("file:///flex-line-strut.html");

    /// <summary>
    /// A column of three 10px items is 30px tall, one of one 10px item 10px, and one of two with
    /// <c>line-height: 40px</c> 20px. They were 36, 16 and 50.
    /// </summary>
    [Theory]
    [InlineData(3, null, 30)]
    [InlineData(1, null, 10)]
    [InlineData(2, "40px", 20)]
    public void A_Column_Ends_At_Its_Last_Item(int count, string? lineHeight, float height)
    {
        var (container, _) = Lay("flex", count, 10, lineHeight);

        Assert.Equal(height, Height(container), 1);
    }

    /// <summary>An <c>inline-flex</c> column of two 10px items is 20px tall. It was 26.</summary>
    [Fact(Timeout = 600000)]
    public void An_Inline_Flex_Column_Ends_At_Its_Last_Item()
    {
        var (container, _) = Lay("inline-flex", 2, 10, null);

        Assert.Equal(20, Height(container), 1);
    }

    /// <summary>
    /// Controls, which pass before and after: a column whose item is taller than a line is as tall
    /// as the item, 30px; a grid of three 10px items is 30px; and a block holding a word keeps its
    /// line, 40px with <c>line-height: 40px</c>.
    /// </summary>
    [Theory]
    [InlineData("flex", 1, 30, null, 30)]
    [InlineData("grid", 3, 10, null, 30)]
    [InlineData("block", 0, 0, "40px", 40)]
    public void Control_Heights_That_Were_Already_Right(
        string display, int count, int itemHeight, string? lineHeight, float height)
    {
        var (container, _) = Lay(display, count, itemHeight, lineHeight);

        Assert.Equal(height, Height(container), 1);
    }

    /// <summary>
    /// A container of <paramref name="display"/>, a column when it is flex, holding
    /// <paramref name="count"/> blocks <paramref name="itemHeight"/>px tall, or a word when there
    /// are none, inside a 320px block.
    /// </summary>
    private static (CssBox Container, CssBox[] Items) Lay(
        string display, int count, int itemHeight, string? lineHeight)
    {
        var root = new CssBox(null, Div(), BaseUrl)
        {
            Display = CssConstants.Block,
            Location = new PointF(0, 0),
            Size = new SizeF(1024, 768),
            LayoutEnvironment = new FakeLayoutEnvironment(),
        };

        var page = new CssBox(root, Div(), BaseUrl) { Display = CssConstants.Block, Width = "320px" };

        var container = new CssBox(page, Div(), BaseUrl) { Display = display, Width = "320px" };
        if (display is "flex" or "inline-flex")
            container.FlexDirection = "column";
        if (lineHeight != null)
            container.LineHeight = lineHeight;

        var items = new CssBox[count];
        for (int i = 0; i < count; i++)
        {
            items[i] = new CssBox(container, Div(), BaseUrl)
            {
                Display = CssConstants.Block,
                Height = $"{itemHeight}px",
            };
        }

        if (count == 0)
        {
            var text = new CssBox(container, new HtmlTag("span", false, null), BaseUrl) { Display = CssConstants.Inline };
            text.Words.Add(new CssRectWord(text, "X", false, false));
        }

        root.PerformLayout(root.LayoutEnvironment);
        return (container, items);
    }

    private static HtmlTag Div() => new("div", false, null);

    private static double Height(CssBox box) => box.ActualBottom - box.Location.Y;

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
