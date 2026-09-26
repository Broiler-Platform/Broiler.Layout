using System;
using System.Drawing;
using System.Linq;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// CSS Flexbox §9.7 along a column's main axis: an item that shrinking takes below its minimum
/// height, or growing takes past its maximum, is frozen at that limit, and the space it could not
/// give or take is shared out again among the items that can still flex, until none is past a
/// limit.
/// </summary>
/// <remarks>
/// A column flex container with a definite height flexed its items in one pass and then clamped
/// each one on its own. What a clamped item did not give or take was never passed on: a shrinking
/// column whose smallest item hit its minimum overflowed by the difference, and a growing column
/// whose item hit its maximum left the difference empty. Each item here holds one word, 16px
/// tall, and has an explicit height, so its minimum is 16px unless a test gives it another.
/// </remarks>
public sealed class FlexColumnFlexibleLengthsTests
{
    private static readonly Uri BaseUrl = new("file:///flex-column-flexible-lengths.html");

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

    /// <summary>
    /// A column flex container <paramref name="height"/> tall and styled by
    /// <paramref name="styleColumn"/>, holding one-word items of <paramref name="heights"/>, each
    /// styled by <paramref name="style"/> with its index.
    /// </summary>
    private static (CssBox Column, CssBox[] Items) Lay(
        string height,
        int[] heights,
        Action<CssBox>? styleColumn = null,
        Action<int, CssBox>? style = null)
    {
        var root = Root();
        var column = Box(root, "flex");
        column.FlexDirection = "column";
        column.Height = height;
        styleColumn?.Invoke(column);

        var items = new CssBox[heights.Length];
        for (int i = 0; i < heights.Length; i++)
        {
            items[i] = Box(column, "block", "section");
            items[i].Height = $"{heights[i]}px";
            var text = Box(items[i], "inline", "span");
            text.Words.Add(new CssRectWord(text, "X", false, false));
            style?.Invoke(i, items[i]);
        }

        root.PerformLayout(root.LayoutEnvironment);
        return (column, items);
    }

    private static void AssertHeights(CssBox[] items, params double[] heights)
    {
        for (int i = 0; i < heights.Length; i++)
            Assert.Equal(heights[i], items[i].Size.Height, 1);
    }

    /// <summary>
    /// The items together run from <paramref name="top"/> to <paramref name="bottom"/>.
    /// </summary>
    private static void AssertSpan(CssBox[] items, double top, double bottom)
    {
        Assert.Equal(top, items.Min(item => item.Location.Y), 1);
        Assert.Equal(bottom, items.Max(item => item.Location.Y + item.Size.Height), 1);
    }

    /// <summary>
    /// A 100px column takes 50px from items 20, 50 and 80px tall. In proportion to their heights,
    /// the 20px item would give 6.67px and fall below its 16px minimum, so it is frozen at 16px and
    /// the other two give the remaining 46px between them in proportion to theirs:
    /// 50 − 46 × 50/130 and 80 − 46 × 80/130, 32.31px and 51.69px. The last item ends at the
    /// column's bottom, not 2.67px below it.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void An_Item_Held_At_Its_Minimum_Leaves_The_Rest_Of_The_Shrink_To_The_Others()
    {
        var (_, items) = Lay("100px", [20, 50, 80]);

        AssertHeights(items, 16, 32.31, 51.69);
        AssertSpan(items, 0, 100);
    }

    /// <summary>
    /// A column with no more room than its items' minimums holds every item at its minimum, 16px,
    /// and the items fill it exactly: a 48px column; a 68px one with two 10px gaps; a 58px one
    /// whose first item has a 10px bottom margin; and a 48px <c>column-reverse</c> one, which is
    /// flexed line by line through the reversing pass. Each takes three passes: the 20px item is
    /// held at its minimum first, then the 50px one, and the 80px one gives the rest. Every item
    /// ends at the content height it was measured at, which is the case the gaps and the reversed
    /// items' heights were lost in.
    /// </summary>
    [Theory]
    [InlineData("height", "48px", 48)]
    [InlineData("gap", "68px", 68)]
    [InlineData("margin", "58px", 58)]
    [InlineData("reverse", "48px", 48)]
    public void A_Column_As_Short_As_Its_Items_Minimums_Holds_Each_At_Its_Minimum(
        string variant, string height, float bottom)
    {
        var (_, items) = Lay(height, [20, 50, 80],
            styleColumn: column =>
            {
                if (variant == "gap")
                    column.RowGap = "10px";
                if (variant == "reverse")
                    column.FlexDirection = "column-reverse";
            },
            style: (i, item) =>
            {
                if (variant == "margin" && i == 0)
                    item.MarginBottom = "10px";
            });

        AssertHeights(items, 16, 16, 16);
        AssertSpan(items, 0, bottom);
    }

    /// <summary>
    /// An explicit <c>min-height</c> is held the same way. Three 100px items in a 150px column
    /// would each give 50px, but the first has a 90px minimum. It gives 10px, and the other two
    /// give the remaining 140px equally, down to 30px each.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void An_Item_Held_At_Its_Min_Height_Leaves_The_Rest_Of_The_Shrink_To_The_Others()
    {
        var (_, items) = Lay("150px", [100, 100, 100], style: (i, item) =>
        {
            if (i == 0)
            {
                item.MinHeight = "90px";
                item.IsMinHeightSpecified = true;
            }
        });

        AssertHeights(items, 90, 30, 30);
        AssertSpan(items, 0, 150);
    }

    /// <summary>
    /// Growing, an item held at its <c>max-height</c> leaves what it cannot take to the others.
    /// Three 10px items have <c>flex-grow: 1</c>. In a 200px column, the middle one stops at a
    /// 30px maximum, and the other two share the 150px left over equally, to 85px each. In a
    /// 300px column, the first two stop at 20px and 40px maximums, and the third takes the
    /// remaining 230px, to 240px.
    /// </summary>
    [Theory]
    [InlineData("200px", null, "30px", 85, 30, 85)]
    [InlineData("300px", "20px", "40px", 20, 40, 240)]
    public void An_Item_Held_At_Its_Max_Height_Leaves_The_Rest_Of_The_Growth_To_The_Others(
        string height, string? maxA, string? maxB, float ha, float hb, float hc)
    {
        var (column, items) = Lay(height, [10, 10, 10], style: (i, item) =>
        {
            item.FlexGrow = "1";
            if (i == 0 && maxA is not null)
                item.MaxHeight = maxA;
            if (i == 1 && maxB is not null)
                item.MaxHeight = maxB;
        });

        AssertHeights(items, ha, hb, hc);
        AssertSpan(items, 0, column.Size.Height);
    }

    /// <summary>
    /// Control: with no limit in the way, one pass is all there is. Items 50, 100 and 150px tall
    /// in a 150px column each give half their height, down to 25, 50 and 75px.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Control_Items_Above_Their_Minimums_Shrink_In_Proportion_To_Their_Heights()
    {
        var (_, items) = Lay("150px", [50, 100, 150]);

        AssertHeights(items, 25, 50, 75);
        AssertSpan(items, 0, 150);
    }

    /// <summary>
    /// Control: with no maximum in the way, three 10px <c>flex-grow: 1</c> items in a 200px
    /// column share the 170px left over equally.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Control_Items_Below_Their_Maximums_Grow_Equally()
    {
        var (_, items) = Lay("200px", [10, 10, 10], style: (_, item) => item.FlexGrow = "1");

        AssertHeights(items, 10 + 170 / 3.0, 10 + 170 / 3.0, 10 + 170 / 3.0);
        AssertSpan(items, 0, 200);
    }

    /// <summary>
    /// Control: an overflow no item can absorb stays. Beside a 100px item at
    /// <c>flex-shrink: 0</c>, a 100px column holds the other 100px item at its 60px
    /// <c>min-height</c>, and the items overflow the column by 60px, as in browsers.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Control_An_Overflow_No_Item_Can_Absorb_Stays()
    {
        var (_, items) = Lay("100px", [100, 100], style: (i, item) =>
        {
            if (i == 0)
            {
                item.MinHeight = "60px";
                item.IsMinHeightSpecified = true;
            }
            else
            {
                item.FlexShrink = "0";
            }
        });

        AssertHeights(items, 60, 100);
        AssertSpan(items, 0, 160);
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
