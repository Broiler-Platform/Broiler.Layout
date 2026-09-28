using System;
using System.Drawing;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// A table with a width of its own is that wide from the outer edge of its left border to the outer
/// edge of its right one.
/// </summary>
/// <remarks>
/// <para>
/// Browsers take a table's <c>width</c> for the width of its border box, and the table algorithm
/// sizes the columns the same way: <c>GetAvailableCellWidth</c> takes the borders and the border
/// spacing off the width and gives the columns the rest. But the table's right edge was then put
/// the right border and the spacing past the width, so a table with <c>width: 320px</c> and a 10px
/// border came out 330px wide, and 332px with the default 2px border spacing, where browsers make
/// it 320px.
/// </para>
/// <para>
/// Each table here is in a 500px block and has one row, with cells holding a word; words are 16px
/// tall and 8px wide a letter.
/// </para>
/// </remarks>
public sealed class TableBorderWidthTests
{
    private static readonly Uri BaseUrl = new("file:///table-border-width.html");

    /// <summary>
    /// A table with <c>width: 320px</c> and a 10px border, with no border spacing or with 2px of
    /// it, is 320px wide, with its cell 300 or 296px wide inside the border. It was 330 or 332px
    /// wide.
    /// </summary>
    [Theory]
    [InlineData("0", 300)]
    [InlineData("2px", 296)]
    public void A_Table_With_A_Border_Is_As_Wide_As_Its_Width(string borderSpacing, float cellWidth)
    {
        var tree = Build(width: "320px", border: 10, borderSpacing: borderSpacing);
        Layout(tree);

        Assert.Equal(320, tree.Table.Size.Width, 1);
        Assert.Equal(cellWidth, tree.Cells[0].Size.Width, 1);
    }

    /// <summary>
    /// A 320px table with a 30px left border, a 5px right one and two cells is 320px wide, its cells
    /// 142.5px wide each. It was 325px wide.
    /// </summary>
    [Fact]
    public void Borders_Of_Different_Widths_Are_Both_Inside_The_Width()
    {
        var tree = Build(width: "320px", border: 0, borderSpacing: "0", cells: 2);
        tree.Table.BorderLeftWidth = "30px";
        tree.Table.BorderLeftStyle = "solid";
        tree.Table.BorderRightWidth = "5px";
        tree.Table.BorderRightStyle = "solid";
        Layout(tree);

        Assert.Equal(320, tree.Table.Size.Width, 1);
        Assert.Equal(142.5, tree.Cells[0].Size.Width, 1);
        Assert.Equal(142.5, tree.Cells[1].Size.Width, 1);
    }

    /// <summary>
    /// With <c>border-collapse: collapse</c>, the 320px table with a 10px border is 320px wide too.
    /// It was 329px wide.
    /// </summary>
    [Fact]
    public void A_Table_With_Collapsed_Borders_Is_As_Wide_As_Its_Width()
    {
        var tree = Build(width: "320px", border: 10, borderSpacing: "0");
        tree.Table.BorderCollapse = CssConstants.Collapse;
        Layout(tree);

        Assert.Equal(320, tree.Table.Size.Width, 1);
    }

    /// <summary>
    /// A table with <c>width: 100px</c>, 2px of border spacing and no rows is 100px wide. It was
    /// 102px wide, the spacing put past its width too.
    /// </summary>
    [Fact]
    public void An_Empty_Table_Is_As_Wide_As_Its_Width()
    {
        var tree = Build(width: "100px", border: 0, borderSpacing: "2px", cells: 0);
        tree.Table.Boxes.Clear();
        Layout(tree);

        Assert.Equal(100, tree.Table.Size.Width, 1);
    }

    /// <summary>
    /// Laid out a second time, the 320px table with a 10px border is 320px wide still.
    /// </summary>
    [Fact]
    public void A_Second_Layout_Gives_The_Same_Width()
    {
        var tree = Build(width: "320px", border: 10, borderSpacing: "2px");
        Layout(tree);
        Layout(tree);

        Assert.Equal(320, tree.Table.Size.Width, 1);
    }

    /// <summary>
    /// Controls, which pass before and after: a 320px table with no border is 320px wide; and a
    /// table with <c>width: 40px</c> and a 10px border whose cell holds a 17-letter word, 136px wide,
    /// is as wide as the word and the borders, 156px.
    /// </summary>
    [Theory]
    [InlineData("no-border", 320)]
    [InlineData("wide-word", 156)]
    public void Control_A_Table_As_Wide_As_It_Was(string kind, float width)
    {
        var tree = kind == "no-border"
            ? Build(width: "320px", border: 0, borderSpacing: "0")
            : Build(width: "40px", border: 10, borderSpacing: "0", text: new string('X', 17));
        Layout(tree);

        Assert.Equal(width, tree.Table.Size.Width, 1);
    }

    /// <summary>
    /// The root, the table and its cells.
    /// </summary>
    private sealed record Tree(CssBox Root, CssBox Table, CssBox[] Cells);

    /// <summary>
    /// In a 500px block in the root, a table of the given width, with a border of the given width
    /// on every side, the given border spacing and one row of <paramref name="cells"/> cells, each
    /// holding <paramref name="text"/>.
    /// </summary>
    private static Tree Build(string width, int border, string borderSpacing, int cells = 1, string text = "X")
    {
        var root = new CssBox(null, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = "block",
            Location = new PointF(0, 0),
            Size = new SizeF(1024, 768),
            LayoutEnvironment = new FakeLayoutEnvironment(),
        };

        var body = new CssBox(root, new HtmlTag("div", false, null), BaseUrl) { Display = "block" };
        var container = new CssBox(body, new HtmlTag("div", false, null), BaseUrl) { Display = "block", Width = "500px" };
        var table = new CssBox(container, new HtmlTag("table", false, null), BaseUrl)
        {
            Display = "table",
            Width = width,
            BorderSpacing = borderSpacing,
        };

        if (border > 0)
        {
            table.BorderLeftWidth = table.BorderTopWidth = table.BorderRightWidth = table.BorderBottomWidth = border + "px";
            table.BorderLeftStyle = table.BorderTopStyle = table.BorderRightStyle = table.BorderBottomStyle = "solid";
        }

        var rows = new CssBox(table, new HtmlTag("tbody", false, null), BaseUrl) { Display = "table-row-group" };
        var row = new CssBox(rows, new HtmlTag("tr", false, null), BaseUrl) { Display = "table-row" };
        var cellBoxes = new CssBox[cells];

        for (int i = 0; i < cells; i++)
        {
            cellBoxes[i] = new CssBox(row, new HtmlTag("td", false, null), BaseUrl) { Display = "table-cell" };
            var word = new CssBox(cellBoxes[i], null, BaseUrl) { Display = "inline", Text = text.AsMemory() };
            word.ParseToWords();
        }

        return new Tree(root, table, cellBoxes);
    }

    private static void Layout(Tree tree)
    {
        FlexGridItemBlockification.Generate(tree.Root);
        tree.Root.PerformLayout(tree.Root.LayoutEnvironment);
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
