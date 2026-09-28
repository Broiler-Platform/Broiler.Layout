using System;
using System.Drawing;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// CSS Flexbox §9.7: when a flex line shrinks, an item's share of the overflow is its
/// <c>flex-shrink</c> scaled by its <em>inner</em> flex base size, the content box of its flex
/// base size. Its margins, borders and padding take no part in it.
/// </summary>
/// <remarks>
/// <para>
/// The row algorithm scaled the factor by the item's outer flex base size, margins, borders and
/// padding included, and the column one did the same with heights. Two <c>flex: 0 1 100px</c>
/// items overflowing a 150px row by 100px each give up 50px in browsers; when one had a 50px
/// margin, or 25px of padding on each side, it gave up 60px and the other 40.
/// </para>
/// <para>
/// Each container here is 150px along its main axis and holds two empty items, which have no
/// content to hold them open, with <c>flex: 0 1 100px</c> unless a test says otherwise.
/// </para>
/// </remarks>
public sealed class FlexShrinkInnerBaseSizeTests
{
    private static readonly Uri BaseUrl = new("file:///flex-shrink-inner-base-size.html");

    /// <summary>
    /// A 50px margin before the first item, or 25px of padding on each of its sides, leaves both
    /// items the same 100px inner flex base size, so they give up the same 50px of the overflow:
    /// both are 50px wide inside, in a row and in a column. The first came out 40px and the second
    /// 60px.
    /// </summary>
    [Theory]
    [InlineData("row", "margin")]
    [InlineData("row", "padding")]
    [InlineData("column", "margin")]
    [InlineData("column", "padding")]
    public void Margins_And_Padding_Take_No_Part_In_The_Shrink(string direction, string spacing)
    {
        var (first, second) = Lay(direction, first =>
        {
            if (spacing == "margin")
                SetMainStartMargin(first, direction, "50px");
            else
                SetMainPadding(first, direction, "25px");
        });

        Assert.Equal(50, ContentSize(first, direction), 1);
        Assert.Equal(50, ContentSize(second, direction), 1);
    }

    /// <summary>
    /// With <c>box-sizing: border-box</c>, a 100px flex basis with 25px of padding on each side
    /// is an inner flex base size of 50px, half the second item's, so of the 50px of overflow the
    /// first gives up a third: it is 83.3px wide, and the second 66.7px. Both were 75px.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Border_Box_Item_Shrinks_By_Its_Content_Box()
    {
        var (first, second) = Lay("row", first =>
        {
            first.BoxSizing = "border-box";
            SetMainPadding(first, "row", "25px");
        });

        Assert.Equal(83.33, first.Size.Width, 1);
        Assert.Equal(66.67, second.Size.Width, 1);
    }

    /// <summary>
    /// Controls, which pass before and after: two items with nothing around them share a 50px
    /// overflow equally, at 75px each, and a line that grows gives each item its <c>flex-grow</c>
    /// share whatever its margin, 175px each of a 400px row.
    /// </summary>
    [Theory]
    [InlineData(false, 150, 75)]
    [InlineData(true, 400, 175)]
    public void Control_Shares_That_Do_Not_Depend_On_Spacing(bool grow, int mainSize, float width)
    {
        var (first, second) = Lay("row", first =>
        {
            if (grow)
                first.MarginLeft = "50px";
        }, mainSize, grow);

        Assert.Equal(width, first.Size.Width, 1);
        Assert.Equal(width, second.Size.Width, 1);
    }

    /// <summary>The item's content box along the main axis.</summary>
    private static double ContentSize(CssBox item, string direction) =>
        direction == "row"
            ? item.Size.Width - item.ActualPaddingLeft - item.ActualPaddingRight
                - item.ActualBorderLeftWidth - item.ActualBorderRightWidth
            : item.Size.Height - item.ActualPaddingTop - item.ActualPaddingBottom
                - item.ActualBorderTopWidth - item.ActualBorderBottomWidth;

    private static void SetMainStartMargin(CssBox box, string direction, string value)
    {
        if (direction == "row")
            box.MarginLeft = value;
        else
            box.MarginTop = value;
    }

    private static void SetMainPadding(CssBox box, string direction, string value)
    {
        if (direction == "row")
        {
            box.PaddingLeft = value;
            box.PaddingRight = value;
        }
        else
        {
            box.PaddingTop = value;
            box.PaddingBottom = value;
        }
    }

    /// <summary>
    /// A flex container of <paramref name="direction"/>, <paramref name="mainSize"/> px along its
    /// main axis, holding two empty <c>flex: 0 1 100px</c> items, <c>flex: 1 1 100px</c> with
    /// <paramref name="grow"/>, the first styled by <paramref name="styleFirst"/>.
    /// </summary>
    private static (CssBox First, CssBox Second) Lay(
        string direction, Action<CssBox> styleFirst, int mainSize = 150, bool grow = false)
    {
        var root = new CssBox(null, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = "block",
            Location = new PointF(0, 0),
            Size = new SizeF(1024, 768),
            LayoutEnvironment = new FakeLayoutEnvironment(),
        };

        var container = new CssBox(root, new HtmlTag("div", false, null), BaseUrl) { Display = "flex" };
        if (direction == "row")
        {
            container.Width = $"{mainSize}px";
        }
        else
        {
            container.FlexDirection = "column";
            container.Width = "320px";
            container.Height = $"{mainSize}px";
        }

        CssBox Item(string basis)
        {
            var item = new CssBox(container, new HtmlTag("section", false, null), BaseUrl) { Display = "block" };
            item.FlexGrow = grow ? "1" : "0";
            item.FlexShrink = "1";
            item.FlexBasis = basis;
            return item;
        }

        var first = Item("100px");
        var second = Item("100px");
        styleFirst(first);

        FlexGridItemBlockification.Generate(root);
        root.PerformLayout(root.LayoutEnvironment);
        return (first, second);
    }

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
