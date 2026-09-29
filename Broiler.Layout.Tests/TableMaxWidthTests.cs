using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// <c>max-width</c> narrows a table, but never below its columns' minimums, and the columns share
/// out the width it leaves them from their minimums, as they share out any width a table is given.
/// </summary>
/// <remarks>
/// <para>
/// CSS Tables 3: a table is never narrower than its columns' minimums, whatever its <c>width</c> and
/// <c>max-width</c>, and its columns share its width out from their minimums: first to the columns
/// with a width of their own, up to it, then to the others, up to their maximums, and past those in
/// proportion to them. The engine narrowed the columns to their minimums, and past them, the table
/// clipping them, where the <c>max-width</c> was narrower; otherwise it gave them what that left
/// evenly. <c>max-width: 20px</c> made a table around "xxxxxx" 20px wide, the word running out of
/// it, where browsers make it 48px wide. A table with a <c>width</c> kept it, wider than its
/// <c>max-width</c> and its columns.
/// </para>
/// <para>
/// Each table here is in a 1000px block and has no border spacing unless given; no cell is padded.
/// Words are 16px tall and 8px wide a letter, and a space is 4px wide.
/// </para>
/// </remarks>
public sealed class TableMaxWidthTests
{
    private static readonly Uri BaseUrl = new("file:///table-max-width.html");

    /// <summary>
    /// <c>max-width: 20px</c> leaves a table around "xxxxxx" 48px wide, as wide as the word. It was
    /// 20px wide.
    /// </summary>
    [Fact]
    public void A_Table_Is_Never_Narrower_Than_Its_Columns_Minimums()
    {
        var tree = Build(maxWidth: "20px");
        var cells = AddRow(tree, "xxxxxx");
        Layout(tree);

        Assert.Equal(48, tree.Table.Size.Width, 1);
        Assert.Equal(48, cells[0].Size.Width, 1);
    }

    /// <summary>
    /// <c>max-width: 40px</c> leaves a table around "xxx xxx" and "yyy yyy" 48px wide, their
    /// minimums together, 24px each. It was 40px wide, 20px each.
    /// </summary>
    [Fact]
    public void Each_Column_Keeps_Its_Minimum()
    {
        var tree = Build(maxWidth: "40px");
        var cells = AddRow(tree, "xxx xxx", "yyy yyy");
        Layout(tree);

        Assert.Equal(48, tree.Table.Size.Width, 1);
        Assert.Equal(24, cells[0].Size.Width, 1);
        Assert.Equal(24, cells[1].Size.Width, 1);
    }

    /// <summary>
    /// <c>max-width: 100px</c>: "xx xx xx xx" and "yy yy", 16px at least and 76px and 36px at
    /// most, grow from their minimums in proportion to what they have yet to take, to 67px and
    /// 33px. They were 64px and 36px.
    /// </summary>
    [Fact]
    public void The_Columns_Grow_From_Their_Minimums_Toward_Their_Maximums()
    {
        var tree = Build(maxWidth: "100px");
        var cells = AddRow(tree, "xx xx xx xx", "yy yy");
        Layout(tree);

        Assert.Equal(100, tree.Table.Size.Width, 1);
        Assert.Equal(67, cells[0].Size.Width, 1);
        Assert.Equal(33, cells[1].Size.Width, 1);
    }

    /// <summary>
    /// With <c>border-spacing: 2px</c>, a 3px border and <c>max-width: 60px</c>, the same columns
    /// share the 48px left, 28px and 20px. They were 24px each.
    /// </summary>
    [Fact]
    public void The_Border_And_Spacing_Come_Off_First()
    {
        var tree = Build(maxWidth: "60px", spacing: "2px");
        tree.Table.BorderLeftWidth = tree.Table.BorderTopWidth = tree.Table.BorderRightWidth = tree.Table.BorderBottomWidth = "3px";
        tree.Table.BorderLeftStyle = tree.Table.BorderTopStyle = tree.Table.BorderRightStyle = tree.Table.BorderBottomStyle = "solid";
        var cells = AddRow(tree, "xx xx xx xx", "yy yy");
        Layout(tree);

        Assert.Equal(60, tree.Table.Size.Width, 1);
        Assert.Equal(28, cells[0].Size.Width, 1);
        Assert.Equal(20, cells[1].Size.Width, 1);
    }

    /// <summary>
    /// <c>max-width: 100px</c>: a column with <c>width: 80px</c> keeps it, and "yy yy yy" beside it
    /// takes the other 20px. They were 46px and 54px.
    /// </summary>
    [Fact]
    public void A_Column_With_A_Width_Of_Its_Own_Keeps_It()
    {
        var tree = Build(maxWidth: "100px");
        var cells = AddRow(tree, "x", "yy yy yy");
        cells[0].Width = "80px";
        Layout(tree);

        Assert.Equal(80, cells[0].Size.Width, 1);
        Assert.Equal(20, cells[1].Size.Width, 1);
    }

    /// <summary>
    /// <c>width: 300px</c> and <c>max-width: 150px</c> make the table 150px wide, "xxxxxx" and "y"
    /// sharing it in proportion to their maximums, 128.57px and 21.43px. The table was 300px wide,
    /// around columns of 95px and 55px.
    /// </summary>
    [Fact]
    public void The_Max_Width_Wins_Over_The_Width()
    {
        var tree = Build(width: "300px", maxWidth: "150px");
        var cells = AddRow(tree, "xxxxxx", "y");
        Layout(tree);

        Assert.Equal(150, tree.Table.Size.Width, 1);
        Assert.Equal(128.57, cells[0].Size.Width, 1);
        Assert.Equal(21.43, cells[1].Size.Width, 1);
    }

    /// <summary>
    /// <c>width: 100%</c> and <c>max-width: 300px</c> make the table 300px wide, "xxxxxx" and "y"
    /// 257.14px and 42.86px. They were 170px and 130px.
    /// </summary>
    [Fact]
    public void A_Percentage_Width_Gives_Way_To_The_Max_Width()
    {
        var tree = Build(width: "100%", maxWidth: "300px");
        var cells = AddRow(tree, "xxxxxx", "y");
        Layout(tree);

        Assert.Equal(300, tree.Table.Size.Width, 1);
        Assert.Equal(257.14, cells[0].Size.Width, 1);
        Assert.Equal(42.86, cells[1].Size.Width, 1);
    }

    /// <summary>
    /// A table with <c>width: 12000px</c> and no <c>max-width</c> has a 12000px column. Its column
    /// was 9999px wide.
    /// </summary>
    [Fact]
    public void A_Table_With_No_Max_Width_Is_Not_Narrowed()
    {
        var tree = Build(width: "12000px");
        var cells = AddRow(tree, "x");
        Layout(tree);

        Assert.Equal(12000, tree.Table.Size.Width, 1);
        Assert.Equal(12000, cells[0].Size.Width, 1);
    }

    /// <summary>
    /// Laid out a second time, the table around "xxxxxx" is 48px wide still.
    /// </summary>
    [Fact]
    public void A_Second_Layout_Gives_The_Same_Table()
    {
        var tree = Build(maxWidth: "20px");
        var cells = AddRow(tree, "xxxxxx");
        Layout(tree);
        Layout(tree);

        Assert.Equal(48, tree.Table.Size.Width, 1);
        Assert.Equal(48, cells[0].Size.Width, 1);
    }

    /// <summary>
    /// Controls, which pass before and after: <c>max-width: 10%</c>, 100px, leaves eight "xx" and a
    /// y 92px and 8px wide; <c>max-width: 500px</c> leaves a table around an x 8px wide.
    /// </summary>
    [Theory]
    [InlineData("10%", "xx xx xx xx xx xx xx xx", 100, 92)]
    [InlineData("500px", "x", 8, 8)]
    public void Control_A_Max_Width_The_Columns_Already_Keep_To(string maxWidth, string text, double table, double first)
    {
        var tree = Build(maxWidth: maxWidth);
        var cells = AddRow(tree, maxWidth == "10%" ? [text, "y"] : [text]);
        Layout(tree);

        Assert.Equal(table, tree.Table.Size.Width, 1);
        Assert.Equal(first, cells[0].Size.Width, 1);
    }

    /// <summary>The root, the table, and its row group.</summary>
    private sealed record Tree(CssBox Root, CssBox Table, CssBox Rows);

    /// <summary>
    /// In a 1000px block in the root, a table with the given width, max-width and border spacing,
    /// and no rows yet.
    /// </summary>
    private static Tree Build(string? width = null, string? maxWidth = null, string spacing = "0")
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
        var table = new CssBox(container, new HtmlTag("table", false, null), BaseUrl) { Display = "table", BorderSpacing = spacing };

        if (width != null)
            table.Width = width;

        if (maxWidth != null)
            table.MaxWidth = maxWidth;

        var rows = new CssBox(table, new HtmlTag("tbody", false, null), BaseUrl) { Display = "table-row-group" };

        return new Tree(root, table, rows);
    }

    /// <summary>Adds to the table a row of cells, none of them padded, holding the given texts.</summary>
    private static CssBox[] AddRow(Tree tree, params string[] texts)
    {
        var row = new CssBox(tree.Rows, new HtmlTag("tr", false, null), BaseUrl) { Display = "table-row" };

        return texts
            .Select(text =>
            {
                var attributes = new Dictionary<string, string> { ["colspan"] = 1.ToString(CultureInfo.InvariantCulture) };
                var cell = new CssBox(row, new HtmlTag("td", false, attributes), BaseUrl) { Display = "table-cell" };
                Word(cell, text);
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
