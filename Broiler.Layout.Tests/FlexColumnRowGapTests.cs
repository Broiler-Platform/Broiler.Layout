using System;
using System.Drawing;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// CSS Box Alignment §8 and CSS Flexbox §9.7: a column flex container's <c>row-gap</c> separates
/// each two items along its main axis, whatever the container's height and whether or not its
/// items flex.
/// </summary>
/// <remarks>
/// Line layout stacks a single-line column container's items touching, and only the column pass
/// that flexes them placed the gaps, when it had something to flex. A container with
/// <c>height: auto</c>, and one whose items all kept their heights, had no gaps at all: three
/// 16px items with a 10px row gap sat at 0, 16 and 32 in a 48px container, where browsers put
/// them at 0, 26 and 52 in a 68px one. Each item here holds one word, 16px tall.
/// </remarks>
public sealed class FlexColumnRowGapTests
{
    private static readonly Uri BaseUrl = new("file:///flex-column-row-gap.html");

    private static CssBox Root() =>
        new(null, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = "block",
            Location = new PointF(0, 0),
            Size = new SizeF(1024, 768),
            LayoutEnvironment = new FakeLayoutEnvironment(),
        };

    private static CssBox Box(CssBox parent, string display, string tag = "div") =>
        new(parent, new HtmlTag(tag, false, null), BaseUrl) { Display = display };

    /// <summary>A block item holding one word.</summary>
    private static CssBox Item(CssBox parent)
    {
        var item = Box(parent, "block", "section");
        var text = Box(item, "inline", "span");
        text.Words.Add(new CssRectWord(text, "X", false, false));
        return item;
    }

    /// <summary>
    /// A column flex container of <paramref name="display"/>, on a line of its own and styled by
    /// <paramref name="style"/>, holding three one-word items styled by
    /// <paramref name="styleItem"/> with their index.
    /// </summary>
    private static (CssBox Column, CssBox A, CssBox B, CssBox C) Lay(
        string display = "flex",
        Action<CssBox>? style = null,
        Action<int, CssBox>? styleItem = null)
    {
        var root = Root();
        var line = Box(root, "block");
        var column = Box(line, display, display == "inline-flex" ? "span" : "div");
        column.FlexDirection = "column";
        style?.Invoke(column);

        var items = new CssBox[3];
        for (int i = 0; i < 3; i++)
        {
            items[i] = Item(column);
            styleItem?.Invoke(i, items[i]);
        }

        root.PerformLayout(root.LayoutEnvironment);
        return (column, items[0], items[1], items[2]);
    }

    private static void AssertTops(
        CssBox column, CssBox a, CssBox b, CssBox c, double ta, double tb, double tc)
    {
        Assert.Equal(column.Location.Y + ta, a.Location.Y, 1);
        Assert.Equal(column.Location.Y + tb, b.Location.Y, 1);
        Assert.Equal(column.Location.Y + tc, c.Location.Y, 1);
    }

    /// <summary>
    /// A <c>height: auto</c> column with a 10px row gap puts its items at 0, 26 and 52, and is
    /// 68px tall, gaps included.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void An_Auto_Height_Column_Puts_Its_Row_Gap_Between_Its_Items()
    {
        var (column, a, b, c) = Lay(style: column => column.RowGap = "10px");

        AssertTops(column, a, b, c, 0, 26, 52);
        Assert.Equal(68, column.Size.Height, 1);
    }

    /// <summary>
    /// A 300px column whose items keep their heights, because none of them grows, puts its
    /// items at 0, 26 and 52 as well.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Definite_Height_Column_Whose_Items_Do_Not_Flex_Puts_Its_Row_Gap_Between_Them()
    {
        var (column, a, b, c) = Lay(style: column =>
        {
            column.Height = "300px";
            column.RowGap = "10px";
        });

        AssertTops(column, a, b, c, 0, 26, 52);
        Assert.Equal(300, column.Size.Height, 1);
    }

    /// <summary>
    /// An <c>inline-flex</c> column, laid out through line layout, puts its items at 0, 26 and 52
    /// and is 68px tall.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void An_Inline_Flex_Column_Puts_Its_Row_Gap_Between_Its_Items()
    {
        var (column, a, b, c) = Lay("inline-flex", column => column.RowGap = "10px");

        AssertTops(column, a, b, c, 0, 26, 52);
        Assert.Equal(68, column.Size.Height, 1);
    }

    /// <summary>
    /// The gap adds to the items' margins and sits inside the container's padding: with a 5px top
    /// margin on the second item the items are at 0, 31 and 57 and the column is 73px tall, and
    /// with 10px of padding on the column they are at 10, 36 and 62 and it is 88px tall.
    /// </summary>
    [Theory]
    [InlineData("margin", 0, 31, 57, 73)]
    [InlineData("padding", 10, 36, 62, 88)]
    public void The_Gap_Adds_To_Margins_And_Sits_Inside_Padding(
        string variant, float ta, float tb, float tc, float height)
    {
        var (column, a, b, c) = Lay(
            style: column =>
            {
                column.RowGap = "10px";
                if (variant == "padding")
                    column.PaddingTop = column.PaddingBottom = "10px";
            },
            styleItem: (i, item) =>
            {
                if (variant == "margin" && i == 1)
                    item.MarginTop = "5px";
            });

        AssertTops(column, a, b, c, ta, tb, tc);
        Assert.Equal(height, column.Size.Height, 1);
    }

    /// <summary>
    /// A relatively positioned item keeps its offset from the place the gapped stack gives it:
    /// with <c>position: relative; top: 5px</c> the second item is at 31 in a <c>height: auto</c>
    /// column with a 10px gap, and at 108.33 in a 300px one whose items grow to 93.33px each.
    /// Restacking the items used to move such an item to its place without its offset, which a
    /// column whose items flexed already did before the gaps were placed for every column. The
    /// control, a column without a gap, which is not restacked, has it at 21 before and after.
    /// </summary>
    [Theory]
    [InlineData("auto", 31)]
    [InlineData("growing", 108.33)]
    [InlineData("no gap", 21)]
    public void A_Relatively_Positioned_Item_Keeps_Its_Offset_In_The_Gapped_Stack(
        string variant, float top)
    {
        var (column, _, b, _) = Lay(
            style: column =>
            {
                if (variant != "no gap")
                    column.RowGap = "10px";
                if (variant == "growing")
                    column.Height = "300px";
            },
            styleItem: (i, item) =>
            {
                if (variant == "growing")
                    item.FlexGrow = "1";
                if (i == 1)
                {
                    item.Position = "relative";
                    item.Top = "5px";
                }
            });

        Assert.Equal(column.Location.Y + top, b.Location.Y, 1);
    }

    /// <summary>
    /// Controls, which pass before and after: a column without a row gap stacks its items touching
    /// in a 48px container; a <c>column-reverse</c> one, placed by the line pass, and a 300px one
    /// whose items grow, placed as they flex, already had their gaps.
    /// </summary>
    [Theory]
    [InlineData("no gap")]
    [InlineData("column-reverse")]
    [InlineData("growing")]
    public void Control_Columns_That_Already_Had_Their_Gaps(string variant)
    {
        var (column, a, b, c) = Lay(
            style: column =>
            {
                if (variant != "no gap")
                    column.RowGap = "10px";
                if (variant == "column-reverse")
                    column.FlexDirection = "column-reverse";
                if (variant == "growing")
                    column.Height = "300px";
            },
            styleItem: (_, item) =>
            {
                if (variant == "growing")
                    item.FlexGrow = "1";
            });

        switch (variant)
        {
            case "no gap":
                AssertTops(column, a, b, c, 0, 16, 32);
                Assert.Equal(48, column.Size.Height, 1);
                break;
            case "column-reverse":
                AssertTops(column, a, b, c, 52, 26, 0);
                Assert.Equal(68, column.Size.Height, 1);
                break;
            default:
                // (300 − 20) / 3 = 93.33px each, 10px apart.
                AssertTops(column, a, b, c, 0, 103.33, 206.67);
                break;
        }
    }

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
