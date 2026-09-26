using System;
using System.Collections.Generic;
using System.Drawing;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// CSS Flexbox §4.5: a flex item's automatic minimum size, its content-based minimum, is clamped
/// by its maximum main size when that is definite. Content wider than an item's
/// <c>max-width</c>, or taller than its <c>max-height</c> in a column, overflows the item rather
/// than stretching it past its maximum.
/// </summary>
/// <remarks>
/// The clamp applied the maximum first and the automatic minimum after it, so the minimum won:
/// an item with <c>max-width: 100px</c> holding a 120px box came out 120px wide, and a shrinking
/// row could not take it below 120px either. An explicit <c>min-width</c> still wins over
/// <c>max-width</c>, as CSS 2.1 §10.4 has it; only the automatic minimum is capped. Each item here
/// holds a block of a fixed size, or one word, 8px wide and 16px tall.
/// </remarks>
public sealed class FlexAutomaticMinimumMaxSizeTests
{
    private static readonly Uri BaseUrl = new("file:///flex-automatic-minimum-max-size.html");

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
    /// tall, holding one item per entry of <paramref name="content"/>: a block that many px wide
    /// (in a row) or tall (in a column), or one word for 0. Each is styled by
    /// <paramref name="style"/> with its index.
    /// </summary>
    private static CssBox[] Lay(bool column, string size, int[] content, Action<int, CssBox> style)
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

        var items = new CssBox[content.Length];
        for (int i = 0; i < content.Length; i++)
        {
            items[i] = Box(container, "block", "section");

            if (content[i] > 0)
            {
                var box = Box(items[i], "block");
                box.Width = column ? "16px" : $"{content[i]}px";
                box.Height = column ? $"{content[i]}px" : "16px";
            }
            else
            {
                var text = Box(items[i], "inline", "span");
                text.Words.Add(new CssRectWord(text, "X", false, false));
            }

            style(i, items[i]);
        }

        root.PerformLayout(root.LayoutEnvironment);
        return items;
    }

    private static double MainSize(bool column, CssBox item) =>
        column ? item.Size.Height : item.Size.Width;

    /// <summary>
    /// An item with <c>max-width: 100px</c> holding a 120px box is 100px wide in a 400px row, and
    /// the box overflows it. So is one with <c>max-width: 50%</c> of a 200px row.
    /// </summary>
    [Theory]
    [InlineData("400px", "100px")]
    [InlineData("200px", "50%")]
    public void An_Item_Is_Held_At_Its_Max_Width_Though_Its_Content_Is_Wider(
        string size, string max)
    {
        var items = Lay(false, size, [120], (_, item) => item.MaxWidth = max);

        Assert.Equal(100, items[0].Size.Width, 1);
    }

    /// <summary>
    /// A shrinking row takes the item no further than its maximum either. In a 300px row, a
    /// <c>flex-basis: 200px; max-width: 100px</c> item holding a 120px box, beside a
    /// <c>flex-basis: 500px</c> one, stops at 100px, and the other shrinks to the 200px left.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Shrinking_Item_Stops_At_Its_Max_Width_Not_At_Its_Content()
    {
        var items = Lay(false, "300px", [120, 0], (i, item) =>
        {
            item.FlexBasis = i == 0 ? "200px" : "500px";
            if (i == 0)
                item.MaxWidth = "100px";
        });

        Assert.Equal(100, items[0].Size.Width, 1);
        Assert.Equal(200, items[1].Size.Width, 1);
    }

    /// <summary>
    /// Controls, which pass before and after:
    /// <list type="bullet">
    /// <item>an explicit <c>min-width: 150px</c> still wins over <c>max-width: 100px</c>, and
    /// <c>min-height: 150px</c> over <c>max-height: 100px</c>;</item>
    /// <item>an item whose 80px content fits its 100px maximum is 80px wide;</item>
    /// <item><c>max-width: max-content</c>, which is no cap below the content, leaves an item
    /// holding a 120px box at 120px;</item>
    /// <item>and in a column, whose non-replaced items' content is measured through block layout,
    /// which applies their <c>max-height</c> already, an item holding a 120px-tall box is held at
    /// its 100px maximum.</item>
    /// </list>
    /// </summary>
    [Theory]
    [InlineData("explicit minimum", false, 120, 150)]
    [InlineData("explicit minimum", true, 120, 150)]
    [InlineData("content within the maximum", false, 80, 80)]
    [InlineData("max-content maximum", false, 120, 120)]
    [InlineData("content above the maximum", true, 120, 100)]
    public void Control_Sizes_That_Were_Already_Right(
        string variant, bool column, int content, float size)
    {
        var items = Lay(column, "400px", [content], (_, item) =>
        {
            string max = variant == "max-content maximum" ? "max-content" : "100px";

            if (column)
            {
                item.MaxHeight = max;
                if (variant == "explicit minimum")
                {
                    item.MinHeight = "150px";
                    item.IsMinHeightSpecified = true;
                }
            }
            else
            {
                item.MaxWidth = max;
                if (variant == "explicit minimum")
                {
                    item.MinWidth = "150px";
                    item.IsMinWidthSpecified = true;
                }
            }
        });

        Assert.Equal(size, MainSize(column, items[0]), 1);
    }

    /// <summary>
    /// Control, which passes before and after: a shrinking column, whose non-replaced item is
    /// measured at its maximum already, stops a <c>flex-basis: 200px; max-height: 100px</c> item
    /// holding a 120px-tall box at 100px in a 300px column, and gives the
    /// <c>flex-basis: 500px</c> one beside it the 200px left.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Control_A_Shrinking_Column_Stops_An_Item_At_Its_Max_Height()
    {
        var items = Lay(true, "300px", [120, 0], (i, item) =>
        {
            item.FlexBasis = i == 0 ? "200px" : "500px";
            if (i == 0)
                item.MaxHeight = "100px";
        });

        Assert.Equal(100, items[0].Size.Height, 1);
        Assert.Equal(200, items[1].Size.Height, 1);
    }

    /// <summary>
    /// A replaced item's content size suggestion is its natural size, 300×150 here, and that is
    /// capped the same way. In a 400px row an image with <c>max-width: 100px</c> is 100px wide, and
    /// in a 200px row one with <c>max-width: 100%</c>, the responsive-image rule, is 200px wide;
    /// both kept the bitmap's 300px.
    /// </summary>
    [Theory]
    [InlineData("400px", "100px", 100)]
    [InlineData("200px", "100%", 200)]
    public void An_Image_Is_Held_At_Its_Max_Width_Though_Its_Bitmap_Is_Wider(
        string width, string maxWidth, float expected)
    {
        var (root, container) = ImageRoot(column: false, width);
        var image = Image(container);
        image.MaxWidth = maxWidth;

        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(expected, image.Size.Width, 1);
    }

    /// <summary>
    /// In a column, block layout applies an item's <c>max-height</c> whenever it lays the item out,
    /// so a replaced item ends at its maximum however tall a minimum its bitmap gave it. But the
    /// flex algorithm counted it at that minimum. In a 300px column, not stretching them, an image
    /// with <c>width: 300px; max-height: 100px</c>, whose bitmap and width both make it 150px tall,
    /// sits beside a <c>flex: 1</c> item. The image is 100px tall and the item takes the other
    /// 200px, where the item took only the 150px left beside a 150px image and left 50px empty.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Columns_Image_Held_At_Its_Max_Height_Leaves_The_Rest_To_Its_Sibling()
    {
        var (root, container) = ImageRoot(column: true, "300px");
        var image = Image(container);
        image.Width = "300px";
        image.MaxHeight = "100px";

        var sibling = Box(container, "block", "section");
        sibling.FlexGrow = "1";
        sibling.FlexBasis = "0px";
        var text = Box(sibling, "inline", "span");
        text.Words.Add(new CssRectWord(text, "X", false, false));

        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(100, image.Size.Height, 1);
        Assert.Equal(200, sibling.Size.Height, 1);
    }

    private static (CssBox Root, CssBox Container) ImageRoot(bool column, string size)
    {
        var root = new CssBox(null, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = "block",
            Location = new PointF(0, 0),
            Size = new SizeF(1024, 768),
            LayoutEnvironment = new ImageLayoutEnvironment(),
        };
        var container = Box(root, "flex");

        if (column)
        {
            container.FlexDirection = "column";
            container.AlignItems = "flex-start";
            container.Height = size;
        }
        else
        {
            container.Width = size;
        }

        return (root, container);
    }

    private static CssBoxImage Image(CssBox container) =>
        new(container,
            new HtmlTag("img", true, new Dictionary<string, string> { ["src"] = "i.png" }),
            BaseUrl)
        {
            Display = "block",
        };

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

    // The same environment, with every image a 300×150 bitmap that loads at once.
    private sealed class ImageLayoutEnvironment : ILayoutEnvironment
    {
        private static readonly Broiler.Graphics.Text.ILayoutFont TheFont = new FakeFont();
        public Broiler.Graphics.Text.ILayoutFont GetFont(string family, double size, LayoutFontStyle style, string? fontFeatures = null) => TheFont;
        public SizeF MeasureText(Broiler.Graphics.Text.ILayoutFont font, string text) => new(8, 16);
        public void MeasureText(Broiler.Graphics.Text.ILayoutFont font, string text, double maxWidth, out int charFit, out double charFitWidth) { charFit = text.Length; charFitWidth = 8; }
        public double GetWhitespaceWidth(Broiler.Graphics.Text.ILayoutFont font) => 4;
        public ImageIntrinsics GetImageIntrinsics(object imageHandle) => new(300, 150, true);
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
        public ILayoutImageLoader CreateImageLoader(Action<object?, RectangleF, bool> onComplete) => new ImageLoader(onComplete);
        public string FormatListMarker(int number, string style) => string.Empty;
    }

    private sealed class ImageLoader(Action<object?, RectangleF, bool> onComplete) : ILayoutImageLoader
    {
        private static readonly object TheImage = new();

        public object? Image { get; private set; }
        public RectangleF Rectangle => RectangleF.Empty;

        public void LoadImage(
            string src, IReadOnlyDictionary<string, string>? attributes, Uri baseUrl)
        {
            Image = TheImage;
            onComplete(TheImage, RectangleF.Empty, false);
        }

        public void Dispose() { }
    }
}
