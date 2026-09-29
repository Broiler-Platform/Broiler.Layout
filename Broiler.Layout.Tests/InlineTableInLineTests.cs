using System;
using System.Drawing;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// An inline table is laid out on the line it comes to, as an atomic inline box, and stands on the
/// line's baseline with its first row's.
/// </summary>
/// <remarks>
/// <para>
/// CSS 2.1 §17.2: an <c>inline-table</c> is inline-level; §10.8.1: its baseline is its first row's,
/// which is that of the row's cells aligned <c>baseline</c>; browsers give a row with no such cell
/// its bottom edge. The engine counted an inline table as block-level, so a block holding one took
/// the block path, which wrapped the text around it in anonymous blocks and put the table on a line
/// of its own: after "Text", the table went below it, where browsers keep it on the line.
/// </para>
/// <para>
/// Each line here is in a 320px block. Words are 16px tall and 8px wide a letter, a space is 4px,
/// and words stand on a baseline 12.8px below their top; the strut's descent is 3.2px. Tables have
/// no border spacing, and each cell holds an x.
/// </para>
/// </remarks>
public sealed class InlineTableInLineTests
{
    private static readonly Uri BaseUrl = new("file:///inline-table-in-line.html");

    private const double Ascent = 12.8;

    /// <summary>
    /// After "Text", the table is on the first line, 36px in, and "more" after it, 48px in. The
    /// table was on a line of its own below.
    /// </summary>
    [Fact]
    public void The_Table_Stays_On_The_Line()
    {
        var tree = Build();
        Layout(tree);

        Assert.Equal(0, tree.Top(tree.Table), 1);
        Assert.Equal(36, tree.Table.Location.X - tree.Block.Location.X, 1);
        Assert.Equal(48, tree.After.Words[0].Left - tree.Block.Location.X, 1);
        Assert.Equal(0, tree.Top(tree.After), 1);
    }

    /// <summary>
    /// With its cell aligned <c>baseline</c>, the table stands on the cell's text: the table and
    /// "Text" are both at the top, and the block is a line tall, 16px. The block was 48px tall.
    /// </summary>
    [Fact]
    public void A_Baseline_Aligned_Cell_Gives_Its_Text_Baseline()
    {
        var tree = Build();
        Layout(tree);

        Assert.Equal(0, tree.Top(tree.Before), 1);
        Assert.Equal(16, tree.Block.Size.Height, 1);
    }

    /// <summary>
    /// With its cell aligned <c>middle</c>, as a <c>&lt;td&gt;</c> is, the row has no baseline, and
    /// the table stands on the row's bottom: "Text" is 3.2px down, and the line 19.2px tall, the
    /// strut's descent below the table.
    /// </summary>
    [Fact]
    public void A_Row_With_No_Baseline_Aligned_Cell_Stands_On_Its_Bottom()
    {
        var tree = Build(cell: c => c.VerticalAlign = CssConstants.Middle);
        Layout(tree);

        Assert.Equal(0, tree.Top(tree.Table), 1);
        Assert.Equal(16 - Ascent, tree.Top(tree.Before), 1);
        Assert.Equal(19.2, tree.Block.Size.Height, 1);
    }

    /// <summary>
    /// With 10px of padding at the top of the table, the first row's bottom is 26px down it, and
    /// "Text" stands on it, 13.2px down.
    /// </summary>
    [Fact]
    public void The_Tables_Padding_Counts()
    {
        var tree = Build(table: t => t.PaddingTop = "10px", cell: c => c.VerticalAlign = CssConstants.Middle);
        Layout(tree);

        Assert.Equal(0, tree.Top(tree.Table), 1);
        Assert.Equal(26 - Ascent, tree.Top(tree.Before), 1);
    }

    /// <summary>
    /// Under a caption, the first row starts below it: "Text" stands on the row's bottom, 32px down
    /// the table, 19.2px down.
    /// </summary>
    [Fact]
    public void The_First_Row_Is_Below_The_Caption()
    {
        var tree = Build(table: t =>
        {
            var caption = new CssBox(t, new HtmlTag("caption", false, null), BaseUrl) { Display = CssConstants.TableCaption };
            caption.SetBeforeBox(t.Boxes[0]);
            Word(caption, "cap");
        }, cell: c => c.VerticalAlign = CssConstants.Middle);
        Layout(tree);

        Assert.Equal(0, tree.Top(tree.Table), 1);
        Assert.Equal(32 - Ascent, tree.Top(tree.Before), 1);
    }

    /// <summary>
    /// With two rows, the first counts: "Text" stands on its bottom, 3.2px down, and the block is
    /// as tall as the table, 32px.
    /// </summary>
    [Fact]
    public void The_First_Of_Two_Rows_Counts()
    {
        var tree = Build(rows: 2, cell: c => c.VerticalAlign = CssConstants.Middle);
        Layout(tree);

        Assert.Equal(0, tree.Top(tree.Table), 1);
        Assert.Equal(16 - Ascent, tree.Top(tree.Before), 1);
        Assert.Equal(32, tree.Block.Size.Height, 1);
    }

    /// <summary>
    /// Beside a 100px float, the table is on the line with "Text", 136px in, at the top. It was on
    /// a line of its own below.
    /// </summary>
    [Fact]
    public void Beside_A_Float_The_Table_Stays_On_The_Line()
    {
        var tree = Build(floatWidth: 100);
        Layout(tree);

        Assert.Equal(0, tree.Top(tree.Table), 1);
        Assert.Equal(136, tree.Table.Location.X - tree.Block.Location.X, 1);
    }

    /// <summary>
    /// A 250px table, too wide for the 184px left beside the float after "Text", goes on a line
    /// below the float, 50px down, at the block's left edge, as an inline-block does there.
    /// </summary>
    [Fact]
    public void A_Table_Too_Wide_Beside_A_Float_Goes_Below_It()
    {
        var tree = Build(table: t => t.Width = "250px", floatWidth: 100);
        Layout(tree);

        Assert.Equal(50, tree.Top(tree.Table), 1);
        Assert.Equal(0, tree.Table.Location.X - tree.Block.Location.X, 1);
    }

    /// <summary>
    /// Laid out a second time, the table is on the first line still, 36px in.
    /// </summary>
    [Fact]
    public void A_Second_Layout_Gives_The_Same_Line()
    {
        var tree = Build();
        Layout(tree);
        Layout(tree);

        Assert.Equal(0, tree.Top(tree.Table), 1);
        Assert.Equal(36, tree.Table.Location.X - tree.Block.Location.X, 1);
    }

    /// <summary>
    /// The parser's fix-up for a block inside an inline box takes apart the inline-level children
    /// holding blocks of a box that <c>ContainsInlinesOnly</c> says holds inline-level children
    /// only, and an inline table's rows are blocks to it. A block holding an inline table answers
    /// no, which leaves the table whole; the layout asks <c>LaysOutOnLines</c>, which counts the
    /// table in, and lays the block out on lines, as the tests above show. With the table counted
    /// inline-level, the block answered yes, and the parser took the table to pieces.
    /// </summary>
    [Fact]
    public void The_Parser_Is_Told_To_Leave_The_Table_Whole()
    {
        var tree = Build();

        Assert.False(LayoutBoxUtils.ContainsInlinesOnly(tree.Block));
    }

    /// <summary>
    /// Controls, which pass before and after: to the parser, a block holding text alone, or text
    /// and a floated inline table, holds inline-level children only.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Control_Text_Alone_Or_Beside_A_Floated_Table(bool floatedTable)
    {
        var tree = Build(table: t =>
        {
            if (floatedTable)
                t.Float = CssConstants.Left;
            else
                t.Display = CssConstants.None;
        });

        Assert.True(LayoutBoxUtils.ContainsInlinesOnly(tree.Block));
    }

    /// <summary>
    /// Control, which passes before and after: a table that is not inline goes on a line of its
    /// own, below "Text".
    /// </summary>
    [Fact]
    public void Control_A_Block_Table_Goes_Below()
    {
        var tree = Build(table: t => t.Display = CssConstants.Table);
        Layout(tree);

        Assert.Equal(16, tree.Top(tree.Table), 1);
        Assert.Equal(0, tree.Table.Location.X - tree.Block.Location.X, 1);
    }

    /// <summary>The root, the 320px block, the table, and the words before and after it.</summary>
    private sealed record Tree(CssBox Root, CssBox Block, CssBox Table, CssBox Before, CssBox After)
    {
        /// <summary>How far the box's first word, or the box, stands below the block's top.</summary>
        public double Top(CssBox box) =>
            (box.Words.Count > 0 ? box.Words[0].Top : box.Location.Y) - Block.Location.Y;
    }

    /// <summary>
    /// In a 320px block in the root, "Text", an inline table of the given number of one-cell rows,
    /// and "more", after a left float of the given width if any.
    /// </summary>
    private static Tree Build(Action<CssBox>? table = null, Action<CssBox>? cell = null, int rows = 1, int floatWidth = 0)
    {
        var root = new CssBox(null, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = "block",
            Location = new PointF(0, 0),
            Size = new SizeF(1024, 768),
            LayoutEnvironment = new FakeLayoutEnvironment(),
        };

        var body = new CssBox(root, new HtmlTag("div", false, null), BaseUrl) { Display = "block" };
        var block = new CssBox(body, new HtmlTag("div", false, null), BaseUrl) { Display = "block", Width = "320px" };

        if (floatWidth > 0)
            _ = new CssBox(block, new HtmlTag("div", false, null), BaseUrl) { Display = "block", Float = CssConstants.Left, Width = floatWidth + "px", Height = "50px" };

        var before = Word(block, "Text ");
        var tableBox = new CssBox(block, new HtmlTag("table", false, null), BaseUrl) { Display = CssConstants.InlineTable, BorderSpacing = "0" };
        var rowGroup = new CssBox(tableBox, new HtmlTag("tbody", false, null), BaseUrl) { Display = CssConstants.TableRowGroup };

        for (int i = 0; i < rows; i++)
        {
            var row = new CssBox(rowGroup, new HtmlTag("tr", false, null), BaseUrl) { Display = CssConstants.TableRow };
            var cellBox = new CssBox(row, new HtmlTag("td", false, null), BaseUrl) { Display = CssConstants.TableCell };
            cell?.Invoke(cellBox);
            Word(cellBox, "x");
        }

        table?.Invoke(tableBox);
        var after = Word(block, " more");

        return new Tree(root, block, tableBox, before, after);
    }

    private static CssBox Word(CssBox parent, string text)
    {
        var word = new CssBox(parent, null, BaseUrl) { Display = "inline", Text = text.AsMemory() };
        word.ParseToWords();
        return word;
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
        public double Size => 12;
        public double Height => 16;
        public double UnderlineOffset => 0;
        public double LeftPadding => 0;
        public string? FontFeatures => null;
    }
}
