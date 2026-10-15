using System;
using System.Collections.Generic;
using System.Drawing;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// A cell's <c>colspan</c> and <c>rowspan</c> are read as HTML reads them (§4.9.11): as non-negative
/// integers (§2.3.4.2), then clamped to 1..1000 and 1..65534 as the <c>colSpan</c> and <c>rowSpan</c> IDL
/// attributes clamp them.
/// </summary>
/// <remarks>
/// The table layout used <c>int.TryParse</c>, so <c>colspan="2px"</c> spanned one column, a negative
/// <c>rowspan</c> came back negative, and no span was bounded: one cell with <c>colspan="3000000"</c>
/// made the layout allocate 137 MiB of column state.
/// </remarks>
public sealed class HtmlTableSpansTests
{
    private static readonly Uri BaseUrl = new("file:///html-table-spans.html");

    [Theory]
    [InlineData("3", 3)]
    [InlineData("  3", 3)]
    [InlineData("\t\n3", 3)]
    [InlineData("+3", 3)]
    [InlineData("3px", 3)]
    [InlineData("3.9", 3)]
    [InlineData("0", 0)]
    [InlineData("-0", 0)]
    [InlineData("007", 7)]
    [InlineData("99999999999999999999", int.MaxValue)]
    public void A_Non_Negative_Integer_Is_Read_As_HTML_Reads_It(string value, int expected) =>
        Assert.Equal(expected, HtmlTableSpans.ParseNonNegativeInteger(value));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("-3")]
    [InlineData("+")]
    [InlineData("x3")]
    [InlineData("\u00A03")]
    public void Anything_Else_Is_An_Error(string? value) =>
        Assert.Null(HtmlTableSpans.ParseNonNegativeInteger(value));

    [Theory]
    [InlineData(null, 1)]
    [InlineData("0", 1)]
    [InlineData("-2", 1)]
    [InlineData("2px", 2)]
    [InlineData("1000", 1000)]
    [InlineData("1001", 1000)]
    [InlineData("3000000", 1000)]
    public void A_Colspan_Is_Clamped_To_1_Through_1000(string? value, int expected) =>
        Assert.Equal(expected, HtmlTableSpans.ParseColSpan(value));

    [Theory]
    [InlineData(null, 1)]
    [InlineData("0", 1)]
    [InlineData("-2", 1)]
    [InlineData("2px", 2)]
    [InlineData("65534", 65534)]
    [InlineData("70000", 65534)]
    public void A_Rowspan_Is_Clamped_To_1_Through_65534(string? value, int expected) =>
        Assert.Equal(expected, HtmlTableSpans.ParseRowSpan(value));

    /// <summary>A cell with <c>colspan="2px"</c> spans both columns of the row below it.</summary>
    [Fact]
    public void A_Colspan_With_Trailing_Characters_Spans_Its_Columns()
    {
        var (root, rows) = Build();
        var spanning = Cell(Row(rows), "2px");
        Block(spanning, "200px");
        var second = Row(rows);
        var left = Cell(second, null);
        var right = Cell(second, null);
        Block(left, "50px");
        Block(right, "50px");

        Layout(root);

        Assert.Equal(left.Size.Width + right.Size.Width, spanning.Size.Width, 0.5);
    }

    /// <summary>A huge <c>colspan</c> lays out as 1000 columns, with bounded work.</summary>
    [Fact]
    public void A_Huge_Colspan_Is_Bounded()
    {
        var (root, rows) = Build();
        Block(Cell(Row(rows), "3000000"), "10px");
        Block(Cell(Row(rows), null), "10px");

        var before = GC.GetAllocatedBytesForCurrentThread();
        Layout(root);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(allocated < 32L * 1024 * 1024, $"Layout allocated {allocated / (1024 * 1024)} MiB.");
    }

    private static (CssBox Root, CssBox Rows) Build()
    {
        var root = new CssBox(null, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = "block",
            Location = new PointF(0, 0),
            Size = new SizeF(1024, 768),
            LayoutEnvironment = new FakeLayoutEnvironment(),
        };
        var body = new CssBox(root, new HtmlTag("div", false, null), BaseUrl) { Display = "block" };
        var table = new CssBox(body, new HtmlTag("table", false, null), BaseUrl) { Display = "table", BorderSpacing = "0" };
        var rows = new CssBox(table, new HtmlTag("tbody", false, null), BaseUrl) { Display = "table-row-group" };
        return (root, rows);
    }

    private static CssBox Row(CssBox rows) =>
        new(rows, new HtmlTag("tr", false, null), BaseUrl) { Display = "table-row" };

    private static CssBox Cell(CssBox row, string? colspan)
    {
        var attributes = colspan is null ? null : new Dictionary<string, string> { ["colspan"] = colspan };
        return new CssBox(row, new HtmlTag("td", false, attributes), BaseUrl) { Display = "table-cell" };
    }

    private static CssBox Block(CssBox parent, string width) =>
        new(parent, new HtmlTag("div", false, null), BaseUrl) { Display = "block", Width = width, Height = "20px" };

    private static void Layout(CssBox root)
    {
        FlexGridItemBlockification.Generate(root);
        root.PerformLayout(root.LayoutEnvironment);
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
        public double Size => 16;
        public double Height => 16;
        public double UnderlineOffset => 0;
        public double LeftPadding => 0;
        public string? FontFeatures => null;
    }
}
