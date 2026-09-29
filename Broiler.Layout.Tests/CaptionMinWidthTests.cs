using System;
using System.Drawing;
using System.Linq;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// A table is at least as wide as its widest caption's min-content contribution, and its columns
/// share out the width that adds.
/// </summary>
/// <remarks>
/// <para>
/// CSS Tables 3: the used width of a table is at least its captions' widest min-content
/// contribution, whatever its <c>width</c> and <c>max-width</c>, and the columns share the width
/// out as they share any width the table is given: from their minimums up to their maximums, then
/// past them in proportion to them. The engine sized the columns from the cells alone and laid the
/// captions out across them, so "Caption" over a cell holding an x made the table 8px wide, and the
/// word ran out of it; a table holding only a caption was 0px wide.
/// </para>
/// <para>
/// Each table here is in a 500px block, with no border spacing unless given, and none of its cells
/// is padded. Words are 16px tall and 8px wide a letter, and a space is 4px wide: "Caption" is
/// 56px wide.
/// </para>
/// </remarks>
public sealed class CaptionMinWidthTests
{
    private static readonly Uri BaseUrl = new("file:///caption-min-width.html");

    /// <summary>
    /// "Caption" over a cell holding an x makes the table, the caption and the cell 56px wide.
    /// They were 8px wide.
    /// </summary>
    [Fact]
    public void A_Table_Is_As_Wide_As_Its_Caption_Needs()
    {
        var tree = Build("Caption", ["x"]);
        Layout(tree);

        Assert.Equal(56, tree.Table.Size.Width, 1);
        Assert.Equal(56, tree.Caption.Size.Width, 1);
        Assert.Equal(56, tree.Cells[0].Size.Width, 1);
    }

    /// <summary>
    /// A table holding only a caption, "cap", is 24px wide, as is the caption. Both were 0px wide.
    /// With <c>border-spacing: 2px</c> and a 3px border it is 24px wide still: with no columns
    /// there is no spacing, and the caption lies across the border. The table was 6px wide and
    /// the caption 8px.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_Table_Holding_Only_A_Caption_Is_As_Wide_As_It_Needs(bool border)
    {
        var tree = Build("cap", [], table =>
        {
            if (!border)
                return;

            table.BorderSpacing = "2px";
            table.BorderLeftWidth = table.BorderTopWidth = table.BorderRightWidth = table.BorderBottomWidth = "3px";
            table.BorderLeftStyle = table.BorderTopStyle = table.BorderRightStyle = table.BorderBottomStyle = "solid";
        });
        Layout(tree);

        Assert.Equal(24, tree.Table.Size.Width, 1);
        Assert.Equal(24, tree.Caption.Size.Width, 1);
    }

    /// <summary>
    /// A table holding only a caption, with <c>width: 100px</c>, is 100px wide, and so is the
    /// caption. The caption was 0px wide.
    /// </summary>
    [Fact]
    public void The_Caption_Of_A_Table_With_No_Columns_Spans_Its_Width()
    {
        var tree = Build("cap", [], table => table.Width = "100px");
        Layout(tree);

        Assert.Equal(100, tree.Table.Size.Width, 1);
        Assert.Equal(100, tree.Caption.Size.Width, 1);
    }

    /// <summary>
    /// A 168px caption over "xxxx" and "y": past their maximums, the columns take the width in
    /// proportion to them, 134.4px and 33.6px. They were 32px and 8px.
    /// </summary>
    [Fact]
    public void Past_Their_Maximums_The_Columns_Share_It_In_Proportion_To_Them()
    {
        var tree = Build("Captioncaptioncaption", ["xxxx", "y"]);
        Layout(tree);

        Assert.Equal(168, tree.Table.Size.Width, 1);
        Assert.Equal(134.4, tree.Cells[0].Size.Width, 1);
        Assert.Equal(33.6, tree.Cells[1].Size.Width, 1);
    }

    /// <summary>
    /// The same with <c>width: 20px</c> on the "xxxx" cell: that column keeps the 32px its word
    /// needs, and the other takes the rest, 136px. They were 32px and 8px.
    /// </summary>
    [Fact]
    public void A_Column_With_A_Width_Of_Its_Own_Keeps_It()
    {
        var tree = Build("Captioncaptioncaption", ["xxxx", "y"]);
        tree.Cells[0].Width = "20px";
        Layout(tree);

        Assert.Equal(32, tree.Cells[0].Size.Width, 1);
        Assert.Equal(136, tree.Cells[1].Size.Width, 1);
    }

    /// <summary>
    /// In a 50px block, "Captionx" over "xx xx xx xx" and "yy yy" makes the table 64px wide. Up to
    /// their maximums, the columns grow from their minimums in proportion to what they have yet to
    /// take: to 40px and 24px. The table was 50px wide, the columns 29.5px and 20.5px.
    /// </summary>
    [Fact]
    public void Up_To_Their_Maximums_The_Columns_Grow_Toward_Them()
    {
        var tree = Build("Captionx", ["xx xx xx xx", "yy yy"], container: 50);
        Layout(tree);

        Assert.Equal(64, tree.Table.Size.Width, 1);
        Assert.Equal(40, tree.Cells[0].Size.Width, 1);
        Assert.Equal(24, tree.Cells[1].Size.Width, 1);
    }

    /// <summary>
    /// A table with <c>width: 100px</c> under a 168px caption, over "xxxxxx" and "y", is 168px wide,
    /// its columns 144px and 24px, in proportion to their maximums. It was 100px wide, the columns
    /// 70px and 30px.
    /// </summary>
    [Fact]
    public void A_Table_With_A_Width_Is_As_Wide_As_Its_Caption_Needs()
    {
        var tree = Build("Captioncaptioncaption", ["xxxxxx", "y"], table => table.Width = "100px");
        Layout(tree);

        Assert.Equal(168, tree.Table.Size.Width, 1);
        Assert.Equal(144, tree.Cells[0].Size.Width, 1);
        Assert.Equal(24, tree.Cells[1].Size.Width, 1);
    }

    /// <summary>
    /// Neither <c>width: 20px</c> nor <c>max-width: 20px</c> makes the table narrower than
    /// "Caption", 56px. It was 20px and 8px wide.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void The_Width_And_Max_Width_Do_Not_Narrow_It(bool maxWidth)
    {
        var tree = Build("Caption", ["x"], table =>
        {
            if (maxWidth)
                table.MaxWidth = "20px";
            else
                table.Width = "20px";
        });
        Layout(tree);

        Assert.Equal(56, tree.Table.Size.Width, 1);
        Assert.Equal(56, tree.Cells[0].Size.Width, 1);
    }

    /// <summary>
    /// Under "cap", a table with <c>max-width: 20px</c> around "xxxxxx" is 48px wide, as wide as
    /// the word, as browsers make it: the columns share the caption's width out from their
    /// minimums. It was 20px wide, the word running out of it.
    /// </summary>
    [Fact]
    public void The_Columns_Share_It_Out_From_Their_Minimums()
    {
        var tree = Build("cap", ["xxxxxx"], table => table.MaxWidth = "20px");
        Layout(tree);

        Assert.Equal(48, tree.Table.Size.Width, 1);
        Assert.Equal(48, tree.Cells[0].Size.Width, 1);
    }

    /// <summary>
    /// The caption's padding, border and margins count, and its own width where it has one:
    /// "Caption" with 10px of padding on either side, or a 10px border, or 10px margins, makes the
    /// table 76px wide, and a caption with <c>width: 200px</c> makes it 200px wide. It was 8px wide.
    /// </summary>
    [Theory]
    [InlineData("padding", 76)]
    [InlineData("border", 76)]
    [InlineData("margin", 76)]
    [InlineData("width", 200)]
    public void The_Caption_Box_Counts(string style, double width)
    {
        var tree = Build("Caption", ["x"], caption: caption =>
        {
            switch (style)
            {
                case "padding":
                    caption.PaddingLeft = caption.PaddingRight = "10px";
                    break;
                case "border":
                    caption.BorderLeftWidth = caption.BorderRightWidth = "10px";
                    caption.BorderLeftStyle = caption.BorderRightStyle = "solid";
                    break;
                case "margin":
                    caption.MarginLeft = caption.MarginRight = "10px";
                    break;
                default:
                    caption.Width = "200px";
                    break;
            }
        });
        Layout(tree);

        Assert.Equal(width, tree.Table.Size.Width, 1);
        Assert.Equal(width, tree.Cells[0].Size.Width, 1);
    }

    /// <summary>
    /// With 10px margins, the caption lies 10px in and is 56px wide, the table 76px.
    /// </summary>
    [Fact]
    public void A_Caption_With_Margins_Lies_Between_Them()
    {
        var tree = Build("Caption", ["x"], caption: caption => caption.MarginLeft = caption.MarginRight = "10px");
        Layout(tree);

        Assert.Equal(tree.Table.Location.X + 10, tree.Caption.Location.X, 1);
        Assert.Equal(56, tree.Caption.Size.Width, 1);
    }

    /// <summary>
    /// A caption below the table widens it the same: 56px. It was 8px wide.
    /// </summary>
    [Fact]
    public void A_Caption_Below_The_Table_Widens_It()
    {
        var tree = Build("Caption", ["x"], caption: caption => caption.CaptionSide = CssConstants.Bottom);
        Layout(tree);

        Assert.Equal(56, tree.Table.Size.Width, 1);
        Assert.Equal(56, tree.Caption.Size.Width, 1);
    }

    /// <summary>
    /// Of two captions, "Caption" and "Captioncaption", the wider makes the table 112px wide. It
    /// was 8px wide.
    /// </summary>
    [Fact]
    public void The_Widest_Caption_Counts()
    {
        var tree = Build("Caption", ["x"]);
        var second = new CssBox(tree.Table, new HtmlTag("caption", false, null), BaseUrl) { Display = CssConstants.TableCaption };
        second.SetBeforeBox(tree.Table.Boxes.First(b => b.Display == "table-row-group"));
        Word(second, "Captioncaption");
        Layout(tree);

        Assert.Equal(112, tree.Table.Size.Width, 1);
        Assert.Equal(112, second.Size.Width, 1);
    }

    /// <summary>
    /// A table with a 2px border, 5px of padding and <c>border-spacing: 3px</c>, under a 112px
    /// caption, is 112px wide: its columns, "x" and "yyy", share the 89px its border, padding and
    /// spacing leave, 22.25px and 66.75px. The table was 55px wide, the columns 8px and 24px.
    /// </summary>
    [Fact]
    public void The_Border_Padding_And_Spacing_Count()
    {
        var tree = Build("Captioncaption", ["x", "yyy"], table =>
        {
            table.BorderSpacing = "3px";
            table.PaddingLeft = table.PaddingTop = table.PaddingRight = table.PaddingBottom = "5px";
            table.BorderLeftWidth = table.BorderTopWidth = table.BorderRightWidth = table.BorderBottomWidth = "2px";
            table.BorderLeftStyle = table.BorderTopStyle = table.BorderRightStyle = table.BorderBottomStyle = "solid";
        });
        Layout(tree);

        Assert.Equal(112, tree.Table.Size.Width, 1);
        Assert.Equal(22.25, tree.Cells[0].Size.Width, 1);
        Assert.Equal(66.75, tree.Cells[1].Size.Width, 1);
    }

    /// <summary>
    /// Two empty cells, with no maximums to go by, share "Caption" evenly: 28px each. They were
    /// 0px wide.
    /// </summary>
    [Fact]
    public void Empty_Columns_Share_It_Evenly()
    {
        var tree = Build("Caption", ["", ""]);
        Layout(tree);

        Assert.Equal(56, tree.Table.Size.Width, 1);
        Assert.Equal(28, tree.Cells[0].Size.Width, 1);
        Assert.Equal(28, tree.Cells[1].Size.Width, 1);
    }

    /// <summary>
    /// Laid out a second time, the table and its cell are 56px wide still.
    /// </summary>
    [Fact]
    public void A_Second_Layout_Gives_The_Same_Table()
    {
        var tree = Build("Caption", ["x"]);
        Layout(tree);
        Layout(tree);

        Assert.Equal(56, tree.Table.Size.Width, 1);
        Assert.Equal(56, tree.Cells[0].Size.Width, 1);
    }

    /// <summary>
    /// Controls, which pass before and after: "cap" over a 10-letter word leaves the table 80px
    /// wide, and over an x in a table with <c>width: 100px</c>, 100px wide.
    /// </summary>
    [Theory]
    [InlineData("xxxxxxxxxx", null, 80)]
    [InlineData("x", "100px", 100)]
    public void Control_A_Caption_Narrower_Than_The_Table(string text, string? width, double expected)
    {
        var tree = Build("cap", [text], table =>
        {
            if (width != null)
                table.Width = width;
        });
        Layout(tree);

        Assert.Equal(expected, tree.Table.Size.Width, 1);
        Assert.Equal(expected, tree.Caption.Size.Width, 1);
    }

    /// <summary>The root, the table, its caption and its cells.</summary>
    private sealed record Tree(CssBox Root, CssBox Table, CssBox Caption, CssBox[] Cells);

    /// <summary>
    /// In a block of the given width in the root, a table with no border spacing, styled by
    /// <paramref name="table"/>, holding a caption with the given text, styled by
    /// <paramref name="caption"/>, and, unless there are none, one row of cells holding the given
    /// texts. An empty text makes an empty cell.
    /// </summary>
    private static Tree Build(string captionText, string[] texts, Action<CssBox>? table = null, Action<CssBox>? caption = null, int container = 500)
    {
        var root = new CssBox(null, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = "block",
            Location = new PointF(0, 0),
            Size = new SizeF(1024, 768),
            LayoutEnvironment = new FakeLayoutEnvironment(),
        };

        var body = new CssBox(root, new HtmlTag("div", false, null), BaseUrl) { Display = "block" };
        var block = new CssBox(body, new HtmlTag("div", false, null), BaseUrl) { Display = "block", Width = container + "px" };
        var tableBox = new CssBox(block, new HtmlTag("table", false, null), BaseUrl) { Display = "table", BorderSpacing = "0" };
        table?.Invoke(tableBox);

        var captionBox = new CssBox(tableBox, new HtmlTag("caption", false, null), BaseUrl) { Display = CssConstants.TableCaption };
        caption?.Invoke(captionBox);
        Word(captionBox, captionText);

        var cells = new CssBox[texts.Length];

        if (texts.Length > 0)
        {
            var rows = new CssBox(tableBox, new HtmlTag("tbody", false, null), BaseUrl) { Display = "table-row-group" };
            var row = new CssBox(rows, new HtmlTag("tr", false, null), BaseUrl) { Display = "table-row" };

            for (int i = 0; i < texts.Length; i++)
            {
                cells[i] = new CssBox(row, new HtmlTag("td", false, null), BaseUrl) { Display = "table-cell" };

                if (texts[i].Length > 0)
                    Word(cells[i], texts[i]);
            }
        }

        return new Tree(root, tableBox, captionBox, cells);
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
