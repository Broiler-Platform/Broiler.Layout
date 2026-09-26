using System;
using System.Drawing;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// CSS Flexbox §9.7: an item that shrinking takes below its minimum width, or growing takes past
/// its maximum, is frozen at that limit, and the space it could not give or take is shared out
/// again among the items that can still flex, until none is past a limit.
/// </summary>
/// <remarks>
/// A flex row flexed its items in one pass and then clamped each one on its own. What a clamped
/// item did not give or take was never passed on: a shrinking row whose smallest item hit its
/// minimum overflowed by the difference, and a growing row whose item hit its maximum left the
/// difference empty. Unless a test says otherwise, the row holds items of 1, 2 and 3 words, 8 px
/// each and 4 px apart, so 8, 20 and 32 px wide at max-content and 8 px each at min-content.
/// </remarks>
public sealed class FlexRowFlexibleLengthsTests
{
    private static readonly Uri BaseUrl = new("file:///flex-row-flexible-lengths.html");

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

    /// <summary>A block item holding <paramref name="words"/> words.</summary>
    private static CssBox Item(CssBox parent, int words)
    {
        var item = Box(parent, "block", "section");
        var text = Box(item, "inline", "span");
        for (int i = 0; i < words; i++)
            text.Words.Add(new CssRectWord(text, "X", i > 0, false));
        return item;
    }

    /// <summary>
    /// A block-level flex row <paramref name="width"/> wide and styled by
    /// <paramref name="styleRow"/>, holding three items of <paramref name="words"/> words (1, 2 and
    /// 3 by default), each styled by <paramref name="style"/> with its index.
    /// </summary>
    private static (CssBox Row, CssBox A, CssBox B, CssBox C) Lay(
        string width,
        Action<CssBox>? styleRow = null,
        Action<int, CssBox>? style = null,
        int[]? words = null)
    {
        words ??= [1, 2, 3];
        var root = Root();
        var row = Box(root, "flex");
        row.Width = width;
        styleRow?.Invoke(row);

        var items = new CssBox[3];
        for (int i = 0; i < 3; i++)
        {
            items[i] = Item(row, words[i]);
            style?.Invoke(i, items[i]);
        }

        root.PerformLayout(root.LayoutEnvironment);
        return (row, items[0], items[1], items[2]);
    }

    private static void AssertWidths(CssBox a, CssBox b, CssBox c, double wa, double wb, double wc)
    {
        Assert.Equal(wa, a.Size.Width, 1);
        Assert.Equal(wb, b.Size.Width, 1);
        Assert.Equal(wc, c.Size.Width, 1);
    }

    private static void AssertEndsAt(CssBox last, double right) =>
        Assert.Equal(right, last.Location.X + last.Size.Width, 1);

    /// <summary>
    /// A 40px row takes 20px from items worth 60px. In proportion to their widths, the 8px item
    /// would give 2.67px and fall below its 8px minimum, so it is frozen at 8px and the other two
    /// give all 20px between them in proportion to theirs: 20 − 20 × 20/52 and 32 − 20 × 32/52,
    /// 12.31px and 19.69px. The last item ends at the row's edge, not 2.67px past it.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void An_Item_Held_At_Its_Minimum_Leaves_The_Rest_Of_The_Shrink_To_The_Others()
    {
        var (_, a, b, c) = Lay("40px");

        AssertWidths(a, b, c, 8, 12.31, 19.69);
        AssertEndsAt(c, 40);
    }

    /// <summary>
    /// A row with no more room than its items' minimums holds every item at its minimum, 8px, and
    /// the last item ends at the row's edge: a 24px row; a min-content one, which is 24px; a 44px
    /// row with two 10px gaps; and a 34px row whose first item has a 10px right margin. Each takes
    /// three passes: the 8px item is held at its minimum first, then the 20px one, and the 32px one
    /// gives the rest.
    /// </summary>
    [Theory]
    [InlineData("width", "24px", 24)]
    [InlineData("min-content", "min-content", 24)]
    [InlineData("gap", "44px", 44)]
    [InlineData("margin", "34px", 34)]
    public void A_Row_As_Narrow_As_Its_Items_Minimums_Holds_Each_At_Its_Minimum(
        string variant, string width, float right)
    {
        var (row, a, b, c) = Lay(width,
            styleRow: row =>
            {
                if (variant == "gap")
                    row.ColumnGap = "10px";
            },
            style: (i, item) =>
            {
                if (variant == "margin" && i == 0)
                    item.MarginRight = "10px";
            });

        Assert.Equal(right, row.Size.Width, 1);
        AssertWidths(a, b, c, 8, 8, 8);
        AssertEndsAt(c, right);
    }

    /// <summary>
    /// An explicit <c>min-width</c> is held the same way. Three one-word items 100px wide in a
    /// 150px row would each give 50px, but the first has a 90px minimum. It gives 10px, and the
    /// other two give the remaining 140px equally, down to 30px each.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void An_Item_Held_At_Its_Min_Width_Leaves_The_Rest_Of_The_Shrink_To_The_Others()
    {
        var (_, a, b, c) = Lay("150px", words: [1, 1, 1], style: (i, item) =>
        {
            item.Width = "100px";
            if (i == 0)
            {
                item.MinWidth = "90px";
                item.IsMinWidthSpecified = true;
            }
        });

        AssertWidths(a, b, c, 90, 30, 30);
        AssertEndsAt(c, 150);
    }

    /// <summary>
    /// Growing, an item held at its <c>max-width</c> leaves what it cannot take to the others.
    /// All three items have <c>flex-grow: 1</c>. In a 200px row, the middle one stops at a 30px
    /// maximum, and the other two share the 130px left over equally: 8 + 65 and 32 + 65. In a
    /// 300px row, the first two stop at 20px and 40px maximums, and the third takes the remaining
    /// 208px.
    /// </summary>
    [Theory]
    [InlineData("200px", null, "30px", 73, 30, 97)]
    [InlineData("300px", "20px", "40px", 20, 40, 240)]
    public void An_Item_Held_At_Its_Max_Width_Leaves_The_Rest_Of_The_Growth_To_The_Others(
        string width, string? maxA, string? maxB, float wa, float wb, float wc)
    {
        var (row, a, b, c) = Lay(width, style: (i, item) =>
        {
            item.FlexGrow = "1";
            if (i == 0 && maxA is not null)
                item.MaxWidth = maxA;
            if (i == 1 && maxB is not null)
                item.MaxWidth = maxB;
        });

        AssertWidths(a, b, c, wa, wb, wc);
        AssertEndsAt(c, row.Size.Width);
    }

    /// <summary>
    /// Control: with no limit in the way, one pass is all there is. One-word items 50, 100 and
    /// 150px wide in a 150px row each give half their width, down to 25, 50 and 75px.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Control_Items_Above_Their_Minimums_Shrink_In_Proportion_To_Their_Widths()
    {
        var (_, a, b, c) = Lay("150px", words: [1, 1, 1], style: (i, item) =>
            item.Width = $"{50 * (i + 1)}px");

        AssertWidths(a, b, c, 25, 50, 75);
        AssertEndsAt(c, 150);
    }

    /// <summary>
    /// Control: with no maximum in the way, <c>flex-grow: 1</c> items in a 200px row share the
    /// 140px left over equally.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Control_Items_Below_Their_Maximums_Grow_Equally()
    {
        var (_, a, b, c) = Lay("200px", style: (_, item) => item.FlexGrow = "1");

        AssertWidths(a, b, c, 8 + 140 / 3.0, 20 + 140 / 3.0, 32 + 140 / 3.0);
        AssertEndsAt(c, 200);
    }

    /// <summary>
    /// Control: an overflow no item can absorb stays. With the 32px item at <c>flex-shrink: 0</c>,
    /// a 40px row holds the other two at their 8px minimums, and the row overflows by 8px, as in
    /// browsers.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Control_An_Overflow_No_Item_Can_Absorb_Stays()
    {
        var (_, a, b, c) = Lay("40px", style: (i, item) =>
        {
            if (i == 2)
                item.FlexShrink = "0";
        });

        AssertWidths(a, b, c, 8, 8, 32);
        AssertEndsAt(c, 48);
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
