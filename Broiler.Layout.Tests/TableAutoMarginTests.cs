using System;
using System.Drawing;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// CSS 2.1 §10.3.3 for a block-level table in flow: auto inline margins take the space its
/// containing block has left, once the table algorithm has sized the table.
/// </summary>
/// <remarks>
/// An in-flow table skips the block width resolution where auto margins are resolved, because its
/// width comes from the table algorithm, and it was placed before it had one. <c>margin: 0 auto</c>,
/// the way a page centres a table, left it at the left edge, and so did HTML's
/// <c>&lt;table align=center&gt;</c>, which Broiler.HTML maps to those margins.
/// </remarks>
public sealed class TableAutoMarginTests
{
    private static readonly Uri BaseUrl = new("file:///table-auto-margins.html");

    private static (CssBox Root, CssBox Table, CssBox Cell) Tree(string? marginLeft, string? marginRight, string? width = "300px")
    {
        var root = new CssBox(null, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = "block",
            Location = new PointF(0, 0),
            Size = new SizeF(800, 600),
            LayoutEnvironment = new FakeLayoutEnvironment(),
        };
        var table = new CssBox(root, new HtmlTag("table", false, null), BaseUrl) { Display = "table" };
        if (width != null)
            table.Width = width;
        if (marginLeft != null)
            table.MarginLeft = marginLeft;
        if (marginRight != null)
            table.MarginRight = marginRight;

        var body = new CssBox(table, new HtmlTag("tbody", false, null), BaseUrl) { Display = "table-row-group" };
        var row = new CssBox(body, new HtmlTag("tr", false, null), BaseUrl) { Display = "table-row" };
        var cell = new CssBox(row, new HtmlTag("td", false, null), BaseUrl) { Display = "table-cell" };
        cell.Words.Add(new CssRectWord(cell, "X", false, false));
        return (root, table, cell);
    }

    [Theory(Timeout = 600000)]
    [InlineData("auto", "auto", 250)]
    [InlineData("auto", "20px", 480)]
    [InlineData("20px", "auto", 20)]
    [InlineData(null, null, 0)]
    public void A_Table_Is_Placed_By_Its_Auto_Margins(string? marginLeft, string? marginRight, double x)
    {
        var (root, table, cell) = Tree(marginLeft, marginRight);

        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(300, table.Size.Width, 1);
        Assert.Equal(x, table.Location.X, 1);
        Assert.True(cell.Location.X >= table.Location.X, $"The cell is at {cell.Location.X}, the table at {table.Location.X}.");
    }

    /// <summary>A table with no width of its own is centred at the width the table algorithm gives it.</summary>
    [Fact(Timeout = 600000)]
    public void An_Auto_Width_Table_Is_Centred_At_Its_Own_Width()
    {
        var (root, table, _) = Tree("auto", "auto", width: null);

        root.PerformLayout(root.LayoutEnvironment);

        Assert.True(table.Size.Width < 800, $"The table is {table.Size.Width}px wide.");
        Assert.Equal((800 - table.Size.Width) / 2, table.Location.X, 1);
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
