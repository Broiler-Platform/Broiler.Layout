using System;
using System.Drawing;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// A table in an inline-block: the height a percentage gives it there, the width it gives the
/// inline-block around it, and the baseline it leaves the inline-block, as reCAPTCHA's checkbox
/// frame has them.
/// </summary>
/// <remarks>
/// <para>
/// reCAPTCHA centres its checkbox and its label each in a <c>display: table; height: 100%</c> in a
/// <c>display: inline-block; height: 100%</c>, side by side in a 74px box, and the label is a
/// <c>width: 152px</c> cell. Chromium makes both inline-blocks, tables and cells 74px tall, the
/// inline-blocks 52px and 152px wide and level at the top, and centres the checkbox and the label
/// in their cells. Broiler made the tables 0px tall, so nothing was centred and the checkbox stood
/// at the top of the frame; made the label's inline-block as wide as its text, 89px; and stood it
/// on the text's baseline, 61px below the checkbox's inline-block, which stood on its bottom edge,
/// so the label ran out of the bottom of the frame.
/// </para>
/// <para>
/// Words are 16px tall and 8px wide a letter, and a word's baseline is 12.8px below its top.
/// </para>
/// </remarks>
public sealed class TableInInlineBlockTests
{
    private static readonly Uri BaseUrl = new("file:///table-in-inline-block.html");

    /// <summary>
    /// A <c>height: 100%</c> table in a <c>height: 100%</c> inline-block in a 74px block is 74px
    /// tall, as its cell is, and centres its text: "x" stands 29px down. The table was 0px tall,
    /// the cell as tall as the text, and "x" stood at the top.
    /// </summary>
    [Fact]
    public void A_Percentage_Height_Table_Fills_A_Percentage_Height_Inline_Block()
    {
        var root = Root(out var body);
        var inlineBlock = InlineBlock(Block(body, "300px", "74px"), height: "100%");
        var table = Table(inlineBlock, "100%");
        var cell = Cell(table);
        var x = Word(cell, "x");
        Layout(root);

        Assert.Equal(74, inlineBlock.Size.Height, 1);
        Assert.Equal(74, table.Size.Height, 1);
        Assert.Equal(74, cell.Size.Height, 1);
        Assert.Equal(29, x.Words[0].Top - cell.Location.Y, 1);
    }

    /// <summary>
    /// A <c>height: 100%</c> table in a <c>height: 50%</c> inline-block in a 100px block is 50px
    /// tall, as the inline-block and the cell are, and centres "x" in it, 17px down. The table was
    /// 0px tall.
    /// </summary>
    /// <remarks>
    /// The table sizes its rows from the inline-block's height. It read it from the inline-block's
    /// ActualHeight, which resolves the inline-block's own 50% against its own 50px: given its
    /// height before its content and no more, the inline-block made a 50px table around a 25px row,
    /// "x" centred in the top half of it.
    /// </remarks>
    [Fact]
    public void A_Table_Takes_Its_Percentage_Of_The_Inline_Blocks_Height()
    {
        var root = Root(out var body);
        var inlineBlock = InlineBlock(Block(body, "300px", "100px"), height: "50%");
        var table = Table(inlineBlock, "100%");
        var cell = Cell(table);
        var x = Word(cell, "x");
        Layout(root);

        Assert.Equal(50, inlineBlock.Size.Height, 1);
        Assert.Equal(50, table.Size.Height, 1);
        Assert.Equal(50, cell.Size.Height, 1);
        Assert.Equal(17, x.Words[0].Top - cell.Location.Y, 1);
    }

    /// <summary>
    /// Control, which passes before and after: a <c>height: 100%</c> table in an inline-block
    /// 74px tall is 74px tall.
    /// </summary>
    [Fact]
    public void Control_A_Percentage_Height_Table_Fills_A_Fixed_Height_Inline_Block()
    {
        var root = Root(out var body);
        var table = Table(InlineBlock(Block(body, "300px", "100px"), height: "74px"), "100%");
        Word(Cell(table), "x");
        Layout(root);

        Assert.Equal(74, table.Size.Height, 1);
    }

    /// <summary>
    /// A shrink-to-fit inline-block around a table is as wide as the cell's own width, and not as
    /// the cell's text, as browsers make it. The cell's padding is outside its width unless
    /// <c>box-sizing</c> says otherwise. The inline-block was as wide as the text: 24px around
    /// "abc", 44px with the padding, and 108px around "aaa bbb ccc ddd"; the table ran out of it.
    /// </summary>
    [Theory]
    [InlineData("152px", "abc", null, false, 152)]
    [InlineData("50px", "aaa bbb ccc ddd", null, false, 50)]
    [InlineData("152px", "abc", "10px", false, 172)]
    [InlineData("152px", "abc", "10px", true, 152)]
    public void An_Inline_Block_Around_A_Cell_With_A_Width_Takes_That_Width(
        string width, string text, string? padding, bool borderBox, double expected)
    {
        var root = Root(out var body);
        var inlineBlock = InlineBlock(Block(body, "300px", null));
        var table = Table(inlineBlock, null);
        var cell = Cell(table, width: width);
        Word(cell, text);

        if (padding != null)
            cell.PaddingLeft = cell.PaddingRight = padding;

        if (borderBox)
            cell.BoxSizing = "border-box";

        Layout(root);

        Assert.Equal(expected, inlineBlock.Size.Width, 1);
        Assert.Equal(expected, table.Size.Width, 1);
    }

    /// <summary>
    /// Control, which passes before and after: around a cell without a width of its own, or with a
    /// percentage one, which resolves against the width being measured, the inline-block is as wide
    /// as the cell's text; around a cell whose word is wider than its width, as wide as the word.
    /// </summary>
    [Theory]
    [InlineData(null, "abc", 24)]
    [InlineData("50%", "abc", 24)]
    [InlineData("50px", "aaaaaaaaaa", 80)]
    public void Control_An_Inline_Block_Around_A_Cell_Is_As_Wide_As_Its_Content_Needs(
        string? width, string text, double expected)
    {
        var root = Root(out var body);
        var inlineBlock = InlineBlock(Block(body, "300px", null));
        Word(Cell(Table(inlineBlock, null), width: width), text);
        Layout(root);

        Assert.Equal(expected, inlineBlock.Size.Width, 1);
    }

    /// <summary>
    /// An inline-block 74px tall holding nothing but a table stands on its bottom edge, whatever
    /// the table holds, so "a" beside it stands with its baseline there: 61.2px down. It stood level
    /// with the text in the cell: 29px down where the cell centres it, at the top where the cell
    /// aligns it to the baseline.
    /// </summary>
    [Theory]
    [InlineData("middle")]
    [InlineData("baseline")]
    public void Text_Beside_An_Inline_Block_Holding_Only_A_Table_Stands_On_Its_Bottom(string verticalAlign)
    {
        var root = Root(out var body);
        var block = Block(body, "320px", null);
        var a = Word(block, "a");
        var inlineBlock = InlineBlock(block, height: "74px");
        Word(Cell(Table(inlineBlock, "100%"), verticalAlign: verticalAlign), "y");
        Layout(root);

        Assert.Equal(0, inlineBlock.Location.Y - block.Location.Y, 1);
        Assert.Equal(74 - 12.8, a.Words[0].Top - block.Location.Y, 1);
    }

    /// <summary>
    /// An inline-block holding "b" above a table takes its baseline from "b", and not from the text
    /// in the table below it, so "a" beside it stands level with "b", at the top. It stood level
    /// with the text in the table, 28px down.
    /// </summary>
    [Fact]
    public void An_Inline_Block_Takes_Its_Baseline_From_The_Text_Around_A_Table_In_It()
    {
        var root = Root(out var body);
        var block = Block(body, "320px", null);
        var a = Word(block, "a");
        var inlineBlock = InlineBlock(block, height: "74px");
        var b = Word(Block(inlineBlock, null, null), "b");
        Word(Cell(Table(inlineBlock, "40px")), "y");
        Layout(root);

        Assert.Equal(0, a.Words[0].Top - block.Location.Y, 1);
        Assert.Equal(0, b.Words[0].Top - block.Location.Y, 1);
    }

    /// <summary>
    /// reCAPTCHA's checkbox frame: the inline-blocks holding the checkbox and the label are 74px
    /// tall and level at the top, 52px and 152px wide, their tables and cells as tall, the checkbox
    /// centred in its cell and the label in its. The tables were 0px tall and the checkbox at the
    /// top of its cell; the label's inline-block was as wide as the text, 108px, and stood on the
    /// text's baseline, 61.2px down.
    /// </summary>
    [Fact]
    public void ReCaptchas_Checkbox_And_Label_Stand_Side_By_Side_Centred()
    {
        var root = Root(out var body);

        // .rc-anchor-normal, and .rc-anchor-content in it.
        var anchor = Block(body, "300px", "74px");
        var content = InlineBlock(anchor, height: "74px", width: "206px");

        // .rc-inline-block > .rc-anchor-center-container > .rc-anchor-center-item, holding the
        // checkbox: a 24px box with a 2px border, `margin: 0 12px 2px 12px` and
        // `vertical-align: text-bottom`.
        var checkboxColumn = InlineBlock(content, height: "100%");
        var checkboxTable = Table(checkboxColumn, "100%");
        var checkboxCell = Cell(checkboxTable);
        var checkbox = InlineBlock(checkboxCell, height: "24px", width: "24px");
        checkbox.BorderLeftWidth = checkbox.BorderRightWidth = checkbox.BorderTopWidth = checkbox.BorderBottomWidth = "2px";
        checkbox.BorderLeftStyle = checkbox.BorderRightStyle = checkbox.BorderTopStyle = checkbox.BorderBottomStyle = "solid";
        checkbox.MarginLeft = checkbox.MarginRight = "12px";
        checkbox.MarginBottom = "2px";
        checkbox.VerticalAlign = "text-bottom";

        // The second .rc-inline-block, holding the label: a `width: 152px` cell.
        var labelColumn = InlineBlock(content, height: "100%");
        var labelTable = Table(labelColumn, "100%");
        var labelCell = Cell(labelTable, width: "152px");
        var label = Word(labelCell, "I'm not a robot");

        Layout(root);

        double top = content.Location.Y;

        Assert.Equal(0, checkboxColumn.Location.Y - top, 1);
        Assert.Equal(52, checkboxColumn.Size.Width, 1);
        Assert.Equal(74, checkboxColumn.Size.Height, 1);
        Assert.Equal(74, checkboxTable.Size.Height, 1);
        Assert.Equal(74, checkboxCell.Size.Height, 1);

        Assert.Equal(0, labelColumn.Location.Y - top, 1);
        Assert.Equal(52, labelColumn.Location.X - content.Location.X, 1);
        Assert.Equal(152, labelColumn.Size.Width, 1);
        Assert.Equal(74, labelColumn.Size.Height, 1);
        Assert.Equal(74, labelTable.Size.Height, 1);
        Assert.Equal(74, labelCell.Size.Height, 1);

        // Centred: as far from the cell's top as the line it is on is from the cell's bottom.
        Assert.Equal(29, label.Words[0].Top - top, 1);
        Assert.Equal(22, checkbox.Location.Y - top, 1);
    }

    private static CssBox Root(out CssBox body)
    {
        var root = new CssBox(null, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = "block",
            Location = new PointF(0, 0),
            Size = new SizeF(1024, 768),
            LayoutEnvironment = new FakeLayoutEnvironment(),
        };

        body = new CssBox(root, new HtmlTag("div", false, null), BaseUrl) { Display = "block" };
        return root;
    }

    private static CssBox Block(CssBox parent, string? width, string? height)
    {
        var block = new CssBox(parent, new HtmlTag("div", false, null), BaseUrl) { Display = "block" };

        if (width != null)
            block.Width = width;

        if (height != null)
            block.Height = height;

        return block;
    }

    private static CssBox InlineBlock(CssBox parent, string? height = null, string? width = null)
    {
        var box = new CssBox(parent, new HtmlTag("div", false, null), BaseUrl) { Display = CssConstants.InlineBlock };

        if (height != null)
            box.Height = height;

        if (width != null)
            box.Width = width;

        return box;
    }

    private static CssBox Table(CssBox parent, string? height)
    {
        var table = new CssBox(parent, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = CssConstants.Table,
            BorderSpacing = "0",
        };

        if (height != null)
            table.Height = height;

        return table;
    }

    private static CssBox Cell(CssBox table, string verticalAlign = "middle", string? width = null)
    {
        var cell = new CssBox(table, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = CssConstants.TableCell,
            VerticalAlign = verticalAlign,
        };

        if (width != null)
            cell.Width = width;

        return cell;
    }

    private static CssBox Word(CssBox parent, string text)
    {
        var word = new CssBox(parent, null, BaseUrl) { Display = "inline", Text = text.AsMemory() };
        word.ParseToWords();
        return word;
    }

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
