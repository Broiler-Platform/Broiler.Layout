using System;
using System.Collections.Generic;
using System.Drawing;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// CSS Flexbox §9.4 step 11 stretches a column flex container's items to its width, and an item
/// whose height follows from its width is then that much taller: the items after it follow it
/// down, and the container grows around them.
/// </summary>
/// <remarks>
/// <para>
/// Line layout stacks a column's items at their shrink-to-fit widths, and the stretch lays each one
/// out again at the container's width afterwards. An image given <c>width: 100%</c> of the
/// anonymous block wrapping it, as the box fix-up gives an image a column stretches, has no width
/// while it is stacked and so no height, and a box with an <c>aspect-ratio</c> is the same. Once
/// stretched it was as tall as its width allows, but the items after it kept the places line
/// layout gave them and the container its height, so they overlapped. A column that flexed or had
/// a row gap was restacked after the stretch already, which is what the controls pin.
/// </para>
/// <para>
/// Each tree is built the way <c>DomParser.PrepareCssTree</c> leaves it: the box fix-ups start with
/// <see cref="FlexGridItemBlockification.Generate"/>, and <see cref="WrapBlockLevelImages"/> then
/// does what <c>CorrectImgBoxes</c> does. The column is 320px wide; each item after the first is
/// 10px tall; words are 8px wide and 16px tall, and an image is a 300×150 bitmap.
/// </para>
/// </remarks>
public sealed class FlexColumnStretchRestackTests
{
    private static readonly Uri BaseUrl = new("file:///flex-column-stretch-restack.html");

    /// <summary>
    /// A stretched image is 320×160, and the two items after it start at 160 and 170, in a 180px
    /// container: an image with no width of its own, which the fix-up gives <c>width: 100%</c> of
    /// its wrapper, and one declared <c>width: 100%</c>. Both items sat at the image's top.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("100%")]
    public void The_Items_After_A_Stretched_Image_Follow_It(string? width)
    {
        var (container, first, items) = Lay(Image(width), count: 2);

        Assert.Equal(320, first.Size.Width, 1);
        Assert.Equal(160, OuterHeight(first), 1);
        Assert.Equal(160, Top(items[0], container), 1);
        Assert.Equal(170, Top(items[1], container), 1);
        Assert.Equal(180, Height(container), 1);
    }

    /// <summary>
    /// The same for an item that holds a box with <c>aspect-ratio: 2 / 1</c>: 160px tall once
    /// stretched to 320, with the next item at 160.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void The_Items_After_A_Stretched_Aspect_Ratio_Box_Follow_It()
    {
        var (container, first, items) = Lay(AspectRatioBox, count: 1);

        Assert.Equal(160, OuterHeight(first), 1);
        Assert.Equal(160, Top(items[0], container), 1);
        Assert.Equal(170, Height(container), 1);
    }

    /// <summary>
    /// An item's margins go with it: an image with 5px above and below is followed at 170.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Stretched_Items_Margins_Stay_Around_It()
    {
        var (container, _, items) = Lay(
            (container, image) =>
            {
                var wrapper = Image(null)(container, image);
                image.MarginTop = "5px";
                image.MarginBottom = "5px";
                return wrapper;
            },
            count: 1);

        Assert.Equal(170, Top(items[0], container), 1);
    }

    /// <summary>
    /// A stretched image that is the column's only item moves nothing, but the container still
    /// grows around it: 160px, where it kept the height it had before the stretch.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void The_Container_Grows_Around_A_Stretched_Last_Item()
    {
        var (container, _, _) = Lay(Image(null), count: 0);

        Assert.Equal(160, Height(container), 1);
    }

    /// <summary>
    /// Controls, which pass before and after: a column with <c>row-gap: 10px</c>, and one 300px
    /// tall, was restacked after the stretch already. The next item is at 170, 10px below the
    /// image, in a 180px container, and at 160 in the 300px one.
    /// </summary>
    [Theory]
    [InlineData("row gap", 170, 180)]
    [InlineData("definite height", 160, 300)]
    public void Control_A_Column_That_Was_Already_Restacked(string variant, float next, float height)
    {
        var (container, _, items) = Lay(Image(null), count: 1, container =>
        {
            if (variant == "row gap")
                container.RowGap = "10px";
            else
                container.Height = "300px";
        });

        Assert.Equal(next, Top(items[0], container), 1);
        Assert.Equal(height, Height(container), 1);
    }

    /// <summary>
    /// Control, which passes before and after: items that keep their heights when stretched, here
    /// words, keep the places line layout gave them, 16px apart in a 48px container.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Control_Items_That_Keep_Their_Heights_Keep_Their_Places()
    {
        var root = Root();
        var container = Column(root);
        var words = new CssBox[3];

        for (int i = 0; i < words.Length; i++)
        {
            words[i] = new CssBox(container, new HtmlTag("div", false, null), BaseUrl) { Display = CssConstants.Block };
            var text = new CssBox(words[i], new HtmlTag("span", false, null), BaseUrl) { Display = CssConstants.Inline };
            text.Words.Add(new CssRectWord(text, "X", false, false));
        }

        Prepare(root);
        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(0, Top(words[0], container), 1);
        Assert.Equal(16, Top(words[1], container), 1);
        Assert.Equal(32, Top(words[2], container), 1);
        Assert.Equal(48, Height(container), 1);
    }

    /// <summary>
    /// A 320px-wide column holding the item <paramref name="first"/> makes, followed by
    /// <paramref name="count"/> blocks 10px tall, prepared as <c>DomParser.PrepareCssTree</c>
    /// prepares a tree and laid out. Returns the first item as the column holds it: an image's
    /// anonymous wrapper.
    /// </summary>
    private static (CssBox Container, CssBox First, CssBox[] Items) Lay(
        Func<CssBox, CssBoxImage, CssBox> first, int count, Action<CssBox>? style = null)
    {
        var root = Root();
        var container = Column(root);
        style?.Invoke(container);

        var image = new CssBoxImage(
            container,
            new HtmlTag("img", true, new Dictionary<string, string> { ["src"] = "i.png" }),
            BaseUrl)
        {
            Display = CssConstants.Inline,
        };

        CssBox made = first(container, image);

        var items = new CssBox[count];
        for (int i = 0; i < count; i++)
        {
            items[i] = new CssBox(container, new HtmlTag("div", false, null), BaseUrl)
            {
                Display = CssConstants.Block,
                Height = "10px",
            };
        }

        Prepare(root);
        root.PerformLayout(root.LayoutEnvironment);

        // The fix-up put the image in an anonymous block, which is the item the column holds.
        return (container, made == image ? image.ParentBox! : made, items);
    }

    /// <summary>An image, with its own <c>width</c> when <paramref name="width"/> is given.</summary>
    private static Func<CssBox, CssBoxImage, CssBox> Image(string? width) => (_, image) =>
    {
        if (width != null)
            image.Width = width;

        return image;
    };

    /// <summary>
    /// A block holding a box with <c>aspect-ratio: 2 / 1</c> and no width of its own, in place of
    /// the image, which is removed.
    /// </summary>
    private static CssBox AspectRatioBox(CssBox container, CssBoxImage image)
    {
        image.ParentBox = null;

        var item = new CssBox(container, new HtmlTag("div", false, null), BaseUrl) { Display = CssConstants.Block };
        _ = new CssBox(item, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = CssConstants.Block,
            AspectRatio = "2 / 1",
        };

        return item;
    }

    private static CssBox Root() =>
        new(null, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = CssConstants.Block,
            Location = new PointF(0, 0),
            Size = new SizeF(1024, 768),
            LayoutEnvironment = new ImageLayoutEnvironment(),
        };

    private static CssBox Column(CssBox root) =>
        new(root, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = "flex",
            FlexDirection = "column",
            Width = "320px",
        };

    private static void Prepare(CssBox root)
    {
        FlexGridItemBlockification.Generate(root);
        WrapBlockLevelImages(root);
    }

    /// <summary>
    /// What <c>DomParser.CorrectImgBoxes</c> does: a <c>display: block</c> image that is not a row
    /// flex item is wrapped in an anonymous block and made inline, and one a column flex container
    /// will stretch is given <c>width: 100%</c> of the wrapper.
    /// </summary>
    private static void WrapBlockLevelImages(CssBox box)
    {
        for (int i = box.Boxes.Count - 1; i >= 0; i--)
        {
            var child = box.Boxes[i];
            if (child is CssBoxImage
                && child.Display == CssConstants.Block
                && !FlexGridItemBlockification.IsRowFlexItem(child))
            {
                bool stretched = FlexGridItemBlockification.IsStretchedColumnFlexItem(child);
                child.ParentBox = CssBoxHelper.CreateBlock(box, BaseUrl, null, child);
                child.Display = CssConstants.Inline;
                if (stretched)
                    child.Width = "100%";
            }
            else
            {
                WrapBlockLevelImages(child);
            }
        }
    }

    private static double Top(CssBox box, CssBox container) => box.Location.Y - container.Location.Y;

    private static double Height(CssBox box) => box.ActualBottom - box.Location.Y;

    private static double OuterHeight(CssBox box) =>
        box.ActualBottom - box.Location.Y + box.ActualMarginTop + box.ActualMarginBottom;

    // Every word 8px wide and 16px tall, a space 4px, and every image a 300×150 bitmap that loads
    // at once.
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

    private sealed class FakeFont : Broiler.Graphics.Text.ILayoutFont
    {
        public double Size => 16;
        public double Height => 16;
        public double UnderlineOffset => 0;
        public double LeftPadding => 0;
        public string? FontFeatures => null;
    }
}
