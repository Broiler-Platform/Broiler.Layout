using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// A table with a width of its own shares it out over its columns as browsers do: from their
/// minimums, first to the columns with a width of their own, up to it, then to the others, up to
/// their maximums, and past those to the others in proportion to their maximums.
/// </summary>
/// <remarks>
/// <para>
/// CSS Tables 3 shares a table's width out over its columns in that order. The engine gave the
/// columns without a width of their own the same share each, and took each that needed less than its
/// share to its maximum; what that left was split evenly over them all. In a 1024px page, a table
/// with <c>width: 100%</c> gave "Some longer text here" and "short" 572.93px and 451.07px, where
/// browsers give them 835.22px and 188.78px. A table narrower than its columns' own widths took
/// theirs.
/// </para>
/// <para>
/// Each table here is in a 1000px block and has no border spacing unless given; no cell is padded.
/// Words are 16px tall and 8px wide a letter, and a space is 4px wide.
/// </para>
/// </remarks>
public sealed class TableWidthShareOutTests
{
    private static readonly Uri BaseUrl = new("file:///table-width-share-out.html");

    /// <summary>
    /// In a table with <c>width: 100px</c>, "xxxxxx" and "y" take the 44px past their maximums in
    /// proportion to them: 85.71px and 14.29px. They were 70px and 30px.
    /// </summary>
    [Fact]
    public void Past_Their_Maximums_The_Columns_Share_It_In_Proportion_To_Them()
    {
        var tree = Build("100px");
        var cells = AddRow(tree, "xxxxxx", "y");
        Layout(tree);

        Assert.Equal(100, tree.Table.Size.Width, 1);
        Assert.Equal(85.71, cells[0].Size.Width, 1);
        Assert.Equal(14.29, cells[1].Size.Width, 1);
    }

    /// <summary>
    /// In a table with <c>width: 200px</c>, an empty column beside a y takes none of it, and the
    /// y's column all 200px. They were 96px and 104px.
    /// </summary>
    [Fact]
    public void An_Empty_Column_Beside_Another_Takes_None_Of_It()
    {
        var tree = Build("200px");
        var cells = AddRow(tree, "", "y");
        Layout(tree);

        Assert.Equal(0, cells[0].Size.Width, 1);
        Assert.Equal(200, cells[1].Size.Width, 1);
    }

    /// <summary>
    /// In a table with <c>width: 50px</c>, "xx xx xx xx" and "yy yy", 16px at least and 76px and
    /// 36px at most, grow from their minimums in proportion to what they have yet to take: to
    /// 29.5px and 20.5px. They were 25px each.
    /// </summary>
    [Fact]
    public void Short_Of_Their_Maximums_The_Columns_Grow_Toward_Them()
    {
        var tree = Build("50px");
        var cells = AddRow(tree, "xx xx xx xx", "yy yy");
        Layout(tree);

        Assert.Equal(29.5, cells[0].Size.Width, 1);
        Assert.Equal(20.5, cells[1].Size.Width, 1);
    }

    /// <summary>
    /// In a table with <c>width: 300px</c>, beside a column with <c>width: 30px</c>, "xx xx xx xx"
    /// and "yy yy" take the rest in proportion to their maximums: 183.21px and 86.79px. The column
    /// with a width keeps its 30px. They were 155px, 115px and 30px.
    /// </summary>
    [Fact]
    public void The_Columns_Without_A_Width_Share_What_The_Others_Leave()
    {
        var tree = Build("300px");
        var cells = AddRow(tree, "xx xx xx xx", "yy yy", "z");
        cells[2].Width = "30px";
        Layout(tree);

        Assert.Equal(183.21, cells[0].Size.Width, 1);
        Assert.Equal(86.79, cells[1].Size.Width, 1);
        Assert.Equal(30, cells[2].Size.Width, 1);
    }

    /// <summary>
    /// A table with <c>width: 60px</c> around two columns with <c>width: 50px</c> holding an x and
    /// a y stays 60px wide, and the columns shrink toward their minimums evenly, to 30px each. The
    /// table was 100px wide, the columns 50px.
    /// </summary>
    [Fact]
    public void A_Table_Narrower_Than_Its_Columns_Widths_Narrows_Them()
    {
        var tree = Build("60px");
        var cells = AddRow(tree, "x", "y");
        cells[0].Width = cells[1].Width = "50px";
        Layout(tree);

        Assert.Equal(60, tree.Table.Size.Width, 1);
        Assert.Equal(30, cells[0].Size.Width, 1);
        Assert.Equal(30, cells[1].Size.Width, 1);
    }

    /// <summary>
    /// In a table with <c>width: 300px</c>, a 2px border, 3px of padding and
    /// <c>border-spacing: 4px</c>, "xxxxxx" and "y" share the 278px those leave in proportion to
    /// their maximums: 238.29px and 39.71px. They were 159px and 119px.
    /// </summary>
    [Fact]
    public void The_Border_Padding_And_Spacing_Come_Off_First()
    {
        var tree = Build("300px", "4px");
        tree.Table.BorderLeftWidth = tree.Table.BorderTopWidth = tree.Table.BorderRightWidth = tree.Table.BorderBottomWidth = "2px";
        tree.Table.BorderLeftStyle = tree.Table.BorderTopStyle = tree.Table.BorderRightStyle = tree.Table.BorderBottomStyle = "solid";
        tree.Table.PaddingLeft = tree.Table.PaddingTop = tree.Table.PaddingRight = tree.Table.PaddingBottom = "3px";
        var cells = AddRow(tree, "xxxxxx", "y");
        Layout(tree);

        Assert.Equal(300, tree.Table.Size.Width, 1);
        Assert.Equal(238.29, cells[0].Size.Width, 1);
        Assert.Equal(39.71, cells[1].Size.Width, 1);
    }

    /// <summary>
    /// In a table with <c>width: 200px</c>, a cell spanning both columns with a 20-letter word
    /// over "x" and "yyy" gives them maximums of 40px and 120px, and they share the table in
    /// proportion to those: 50px and 150px. They were 60px and 140px.
    /// </summary>
    [Fact]
    public void A_Spanning_Cell_Counts_In_The_Maximums()
    {
        var tree = Build("200px");
        var spanning = AddRow(tree, (2, new string('x', 20)));
        var cells = AddRow(tree, "x", "yyy");
        Layout(tree);

        Assert.Equal(200, spanning[0].Size.Width, 1);
        Assert.Equal(50, cells[0].Size.Width, 1);
        Assert.Equal(150, cells[1].Size.Width, 1);
    }

    /// <summary>
    /// Laid out a second time, "xxxxxx" and "y" are 85.71px and 14.29px still.
    /// </summary>
    [Fact]
    public void A_Second_Layout_Gives_The_Same_Columns()
    {
        var tree = Build("100px");
        var cells = AddRow(tree, "xxxxxx", "y");
        Layout(tree);
        Layout(tree);

        Assert.Equal(85.71, cells[0].Size.Width, 1);
        Assert.Equal(14.29, cells[1].Size.Width, 1);
    }

    /// <summary>
    /// Controls, which pass before and after, in a table with <c>width: 200px</c>: an x in a
    /// column with <c>width: 20px</c> keeps it, and a y beside it takes the other 180px; columns
    /// with <c>width: 20px</c> and <c>width: 40px</c> share the table in proportion to those,
    /// 66.67px and 133.33px; two empty columns share it evenly; and a column with
    /// <c>width: 25%</c> takes 50px, the other 150px.
    /// </summary>
    [Theory]
    [InlineData("20px", null, "x", "y", 20, 180)]
    [InlineData("20px", "40px", "x", "y", 66.67, 133.33)]
    [InlineData(null, null, "", "", 100, 100)]
    [InlineData("25%", null, "x", "y", 50, 150)]
    public void Control_The_Columns_Already_Shared_As_Browsers_Do(string? first, string? second, string a, string b, double aWidth, double bWidth)
    {
        var tree = Build("200px");
        var cells = AddRow(tree, a, b);
        if (first != null)
            cells[0].Width = first;
        if (second != null)
            cells[1].Width = second;
        Layout(tree);

        Assert.Equal(aWidth, cells[0].Size.Width, 1);
        Assert.Equal(bWidth, cells[1].Size.Width, 1);
    }

    /// <summary>The root, the table, and its row group.</summary>
    private sealed record Tree(CssBox Root, CssBox Table, CssBox Rows);

    /// <summary>
    /// In a 1000px block in the root, a table with the given width and border spacing, and no rows
    /// yet.
    /// </summary>
    private static Tree Build(string width, string spacing = "0")
    {
        var root = new CssBox(null, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = "block",
            Location = new PointF(0, 0),
            Size = new SizeF(1024, 768),
            LayoutEnvironment = new FakeLayoutEnvironment(),
        };

        var body = new CssBox(root, new HtmlTag("div", false, null), BaseUrl) { Display = "block" };
        var container = new CssBox(body, new HtmlTag("div", false, null), BaseUrl) { Display = "block", Width = "1000px" };
        var table = new CssBox(container, new HtmlTag("table", false, null), BaseUrl) { Display = "table", BorderSpacing = spacing, Width = width };
        var rows = new CssBox(table, new HtmlTag("tbody", false, null), BaseUrl) { Display = "table-row-group" };

        return new Tree(root, table, rows);
    }

    /// <summary>Adds to the table a row of cells, one column each, holding the given texts.</summary>
    private static CssBox[] AddRow(Tree tree, params string[] texts) =>
        AddRow(tree, texts.Select(t => (1, t)).ToArray());

    /// <summary>
    /// Adds to the table a row of cells, none of them padded, each spanning the given number of
    /// columns and holding the given text, or empty where it is empty.
    /// </summary>
    private static CssBox[] AddRow(Tree tree, params (int Span, string Text)[] cells)
    {
        var row = new CssBox(tree.Rows, new HtmlTag("tr", false, null), BaseUrl) { Display = "table-row" };

        return cells
            .Select(c =>
            {
                var attributes = new Dictionary<string, string> { ["colspan"] = c.Span.ToString(CultureInfo.InvariantCulture) };
                var cell = new CssBox(row, new HtmlTag("td", false, attributes), BaseUrl) { Display = "table-cell" };

                if (c.Text.Length > 0)
                    Word(cell, c.Text);

                return cell;
            })
            .ToArray();
    }

    private static void Word(CssBox parent, string text)
    {
        var word = new CssBox(parent, null, BaseUrl) { Display = "inline", Text = text.AsMemory() };
        word.ParseToWords();
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
