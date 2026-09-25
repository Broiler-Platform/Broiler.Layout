using System;
using System.Drawing;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// Min- and max-content count each box's horizontal border and padding once, on the paths that go
/// through it; and a table row's minimum is the sum of its cells'.
/// </summary>
/// <remarks>
/// The padding and border of every box the intrinsic walk visited went into one running total,
/// added to both widths at the end, so boxes stacked one above another counted as if they sat side
/// by side. html5test.com's results column — an inline-block holding tables with a padded cell per
/// feature — came out 12,060px wide in a 900px page, and the page ran far past the window.
/// </remarks>
public sealed class IntrinsicWidthPaddingTests
{
    private static readonly Uri BaseUrl = new("file:///intrinsic-padding.html");

    private static CssBox Root() =>
        new(null, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = "block",
            Location = new PointF(0, 0),
            Size = new SizeF(2000, 1000),
            LayoutEnvironment = new FakeLayoutEnvironment(),
        };

    private static CssBox Box(CssBox parent, string display, string tag = "div") =>
        new(parent, new HtmlTag(tag, false, null), BaseUrl) { Display = display };

    /// <summary>An inline box holding one 8px word.</summary>
    private static void Word(CssBox parent)
    {
        var text = Box(parent, "inline", "span");
        text.Words.Add(new CssRectWord(text, "X", false, false));
    }

    private static (double Min, double Max, float Width) Measure(CssBox root, CssBox box)
    {
        root.PerformLayout(root.LayoutEnvironment);
        box.GetMinMaxWidth(out double min, out double max);
        return (min, max, box.Size.Width);
    }

    /// <summary>
    /// Ten rows stacked in an inline-block, each with 100px of left padding or border before an 8px
    /// word. They are one above another, so the inline-block is 108px wide, not 1008px.
    /// </summary>
    [Theory(Timeout = 600000)]
    [InlineData("padding")]
    [InlineData("border")]
    public void Stacked_Rows_Count_Their_Edges_Once(string edge)
    {
        var root = Root();
        var column = Box(root, "inline-block");
        for (var i = 0; i < 10; i++)
        {
            var row = Box(column, "block");
            if (edge == "padding")
            {
                row.PaddingLeft = "100px";
            }
            else
            {
                row.BorderLeftWidth = "100px";
                row.BorderLeftStyle = "solid";
            }

            Word(row);
        }

        var (min, max, width) = Measure(root, column);

        Assert.Equal(108, min, 1);
        Assert.Equal(108, max, 1);
        Assert.Equal(108, width, 1);
    }

    /// <summary>
    /// The same ten items side by side, as inline-blocks, are one line: max-content is their sum, and
    /// min-content is still one of them.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Items_Side_By_Side_Sum_On_Their_Line()
    {
        var root = Root();
        var line = Box(root, "inline-block");
        for (var i = 0; i < 10; i++)
        {
            var item = Box(line, "inline-block");
            item.PaddingLeft = "100px";
            Word(item);
        }

        var (min, max, _) = Measure(root, line);

        Assert.Equal(108, min, 1);
        Assert.Equal(1080, max, 1);
    }

    /// <summary>Nested boxes are one path, and every edge on it counts.</summary>
    [Fact(Timeout = 600000)]
    public void Nested_Boxes_Count_Every_Edge_On_Their_Path()
    {
        var root = Root();
        var outer = Box(root, "inline-block");
        var box = outer;
        for (var i = 0; i < 3; i++)
        {
            box = Box(box, "block");
            box.PaddingLeft = "5px";
            box.PaddingRight = "5px";
        }

        Word(box);

        var (min, max, _) = Measure(root, outer);

        Assert.Equal(38, min, 1);
        Assert.Equal(38, max, 1);
    }

    /// <summary>
    /// A table row's cells sit side by side with nothing to wrap between them, so its minimum is the
    /// sum of its cells', padding included: three cells of 8px and 20px padding need 84px, however
    /// many such rows there are.
    /// </summary>
    [Theory(Timeout = 600000)]
    [InlineData(1)]
    [InlineData(2)]
    public void A_Table_Rows_Minimum_Is_The_Sum_Of_Its_Cells(int rows)
    {
        var root = Root();
        var wrapper = Box(root, "inline-block");
        var table = Box(wrapper, "table", "table");
        var body = Box(table, "table-row-group", "tbody");
        for (var r = 0; r < rows; r++)
        {
            var row = Box(body, "table-row", "tr");
            for (var c = 0; c < 3; c++)
            {
                var cell = Box(row, "table-cell", "td");
                cell.PaddingLeft = "10px";
                cell.PaddingRight = "10px";
                Word(cell);
            }
        }

        var (min, _, _) = Measure(root, wrapper);

        Assert.Equal(84, min, 1);
    }

    // Minimal ILayoutEnvironment: every word 8px wide, a space 4px.
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
        public SizeF ViewportSize => new(1000, 1000);
        public PointF RootLocation => PointF.Empty;
        public SizeF ActualSize { get; set; }
        public bool AvoidGeometryAntialias => false;
        public SizeF PageSize => new(1000, 1000);
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
