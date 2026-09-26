using System;
using System.Drawing;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// CSS Flexbox §9.7: a line's free space is distributed from its items' flex base sizes, before
/// their min and max sizes clamp them. An item already clamped the way the line flexes, grown past
/// its maximum or shrunk below its minimum by the clamp alone, is frozen at its hypothetical main
/// size from the start (step 3); every other item starts from its flex base size.
/// </summary>
/// <remarks>
/// Rows and columns distributed from the hypothetical main sizes instead, the flex base sizes
/// already clamped. Two <c>flex: 1</c> items in a 300px row, one holding a 120px box, came out 206
/// and 94px wide, because the first started from its 120px minimum rather than from 0; browsers
/// make them 150 each. Unless a test says otherwise, each item holds one word, 8px wide and 16px
/// tall, so its automatic minimum is 8px wide or 16px tall.
/// </remarks>
public sealed class FlexBaseSizeDistributionTests
{
    private static readonly Uri BaseUrl = new("file:///flex-base-size-distribution.html");

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
    /// A flex container, a row <paramref name="size"/> wide or a column <paramref name="size"/>
    /// tall, holding two items styled by <paramref name="style"/> with their index. An item holds
    /// one word, or a block <paramref name="boxWidths"/>[index] px wide when that is not zero.
    /// </summary>
    private static (CssBox A, CssBox B) Lay(
        bool column, string size, Action<int, CssBox> style, int[]? boxWidths = null)
    {
        var root = Root();
        var container = Box(root, "flex");
        if (column)
        {
            container.FlexDirection = "column";
            container.Height = size;
        }
        else
        {
            container.Width = size;
        }

        var items = new CssBox[2];
        for (int i = 0; i < 2; i++)
        {
            items[i] = Box(container, "block", "section");

            if (boxWidths is not null && boxWidths[i] > 0)
            {
                var box = Box(items[i], "block");
                box.Width = $"{boxWidths[i]}px";
                box.Height = "16px";
            }
            else
            {
                var text = Box(items[i], "inline", "span");
                text.Words.Add(new CssRectWord(text, "X", false, false));
            }

            style(i, items[i]);
        }

        root.PerformLayout(root.LayoutEnvironment);
        return (items[0], items[1]);
    }

    private static void Flex1(CssBox item)
    {
        item.FlexGrow = "1";
        item.FlexShrink = "1";
        item.FlexBasis = "0px";
    }

    private static void AssertWidths(CssBox a, CssBox b, double wa, double wb)
    {
        Assert.Equal(wa, a.Size.Width, 1);
        Assert.Equal(wb, b.Size.Width, 1);
        Assert.Equal(a.Location.X + a.Size.Width, b.Location.X, 1);
    }

    private static void AssertHeights(CssBox a, CssBox b, double ha, double hb)
    {
        Assert.Equal(ha, a.Size.Height, 1);
        Assert.Equal(hb, b.Size.Height, 1);
        Assert.Equal(a.Location.Y + a.Size.Height, b.Location.Y, 1);
    }

    /// <summary>
    /// Two <c>flex: 1</c> items in a 300px row, the first holding a 120px box, share the row
    /// equally, 150px each: both grow from a flex base size of 0, and 150px is above the first
    /// one's 120px minimum. Growing from their minimums instead gave 206 and 94px.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Flex_1_Items_Grow_Equally_From_Zero_Whatever_Their_Content_Minimums()
    {
        var (a, b) = Lay(false, "300px", (_, item) => Flex1(item), boxWidths: [120, 0]);

        AssertWidths(a, b, 150, 150);
    }

    /// <summary>
    /// The same with an explicit minimum: two <c>flex: 1</c> items in a 300px row, the first with
    /// <c>min-width: 100px</c>, are 150px each, where growing from their 100 and 8px minimums gave
    /// 196 and 104px. In a 300px column, with <c>min-height: 100px</c>, they are 150px tall each,
    /// not 192 and 108px.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Flex_1_Items_Grow_Equally_From_Zero_Past_An_Explicit_Minimum(bool column)
    {
        var (a, b) = Lay(column, "300px", (i, item) =>
        {
            Flex1(item);
            if (i == 0 && column)
            {
                item.MinHeight = "100px";
                item.IsMinHeightSpecified = true;
            }
            else if (i == 0)
            {
                item.MinWidth = "100px";
                item.IsMinWidthSpecified = true;
            }
        });

        if (column)
            AssertHeights(a, b, 150, 150);
        else
            AssertWidths(a, b, 150, 150);
    }

    /// <summary>
    /// A minimum still holds when the share from zero falls short of it: two <c>flex: 1</c> items
    /// in a 300px row, the first holding a 200px box, are 200 and 100px. The first is frozen at its
    /// minimum, and the second takes the rest, where growing from their minimums gave 246 and 54px.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Minimum_Above_The_Equal_Share_Still_Holds()
    {
        var (a, b) = Lay(false, "300px", (_, item) => Flex1(item), boxWidths: [200, 0]);

        AssertWidths(a, b, 200, 100);
    }

    /// <summary>
    /// Shrinking starts from the flex base size too. Two <c>flex-basis: 500px</c> items in a 400px
    /// row, the first with <c>max-width: 300px</c>, each give up half of the 600px they overflow
    /// by, down to 200px, which is below the first one's maximum; starting from its 300px maximum
    /// gave 150 and 250px. The same in a 400px column, with <c>max-height: 300px</c>.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Items_Shrink_From_Their_Flex_Base_Sizes_Not_Their_Maximums(bool column)
    {
        var (a, b) = Lay(column, "400px", (i, item) =>
        {
            item.FlexBasis = "500px";
            if (i == 0)
            {
                if (column)
                    item.MaxHeight = "300px";
                else
                    item.MaxWidth = "300px";
            }
        });

        if (column)
            AssertHeights(a, b, 200, 200);
        else
            AssertWidths(a, b, 200, 200);
    }

    /// <summary>
    /// And an item shrunk from its flex base size can still meet its maximum on the way down: with
    /// <c>max-width: 150px</c> the first item's half would leave it at 200px, above its maximum, so
    /// it is frozen at 150px and the second shrinks to the 250px left. Starting from the
    /// maximum gave 92.31 and 307.69px.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void An_Item_Shrinking_From_Its_Flex_Base_Size_Is_Still_Held_At_Its_Maximum()
    {
        var (a, b) = Lay(false, "400px", (i, item) =>
        {
            item.FlexBasis = "500px";
            if (i == 0)
                item.MaxWidth = "150px";
        });

        AssertWidths(a, b, 150, 250);
    }

    /// <summary>
    /// Controls, which pass before and after, where the flex base size and the hypothetical main
    /// size agree or the difference cannot show: two one-word <c>flex: 1</c> items in a 300px row
    /// or column share it equally; two 100px items shrink to 75px each in a 150px row; and in a
    /// 300px row a <c>flex-basis: 400px; max-width: 100px</c> item that grows stays at its maximum
    /// beside a <c>flex: 1</c> one that takes the other 200px.
    /// </summary>
    [Theory]
    [InlineData("equal row")]
    [InlineData("equal column")]
    [InlineData("unclamped shrink")]
    [InlineData("grown past its maximum")]
    public void Control_Distributions_That_Were_Already_Right(string variant)
    {
        switch (variant)
        {
            case "equal row":
            {
                var (a, b) = Lay(false, "300px", (_, item) => Flex1(item));
                AssertWidths(a, b, 150, 150);
                break;
            }
            case "equal column":
            {
                var (a, b) = Lay(true, "300px", (_, item) => Flex1(item));
                AssertHeights(a, b, 150, 150);
                break;
            }
            case "unclamped shrink":
            {
                var (a, b) = Lay(false, "150px", (_, item) => item.FlexBasis = "100px");
                AssertWidths(a, b, 75, 75);
                break;
            }
            default:
            {
                var (a, b) = Lay(false, "300px", (i, item) =>
                {
                    item.FlexGrow = "1";
                    item.FlexBasis = i == 0 ? "400px" : "0px";
                    if (i == 0)
                        item.MaxWidth = "100px";
                });
                AssertWidths(a, b, 100, 200);
                break;
            }
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
