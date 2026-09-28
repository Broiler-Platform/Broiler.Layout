using System;
using System.Collections.Generic;
using System.Drawing;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// A block-level image is placed across its container by its margins, as a block-level box is
/// (CSS2.1 §10.3.4, which applies §10.3.3's margin rules): <c>margin: 0 auto</c> centres it, and
/// <c>text-align</c> does not move it.
/// </summary>
/// <remarks>
/// <para>
/// The box fix-up in Broiler.HTML (<c>DomParser.CorrectImgBoxes</c>) wraps a <c>display: block</c>
/// image in an anonymous block and makes it inline inside it; only when the image declares a width
/// as well as an <c>auto</c> margin does it hand both to the wrapper. The wrapper's line then placed
/// every other block-level image: where <c>text-align</c> put the line's content, and with an
/// <c>auto</c> margin counting as nothing, as an inline box's does. So
/// <c>img { display: block; margin: 0 auto }</c>, the usual way to centre an image, left it at the
/// start of its container, and a container with <c>text-align: center</c> centred an image that
/// browsers leave at its start.
/// </para>
/// <para>
/// Each tree is built the way <c>DomParser.PrepareCssTree</c> leaves it:
/// <see cref="FlexGridItemBlockification.Generate"/>, then what <c>CorrectImgBoxes</c> does. The
/// container is 320px wide, and an image is a 300×150 bitmap.
/// </para>
/// </remarks>
public sealed class BlockLevelImageHorizontalTests
{
    private static readonly Uri BaseUrl = new("file:///block-level-image-horizontal.html");

    /// <summary>
    /// The margins place the image: both <c>auto</c> centre it at 10, a left one alone puts it at
    /// the end, 20, and in a right-to-left container both centre it too, a right one alone puts it at
    /// the start, 10, and a 5px right margin leaves it 5px from the right, at 15. The line had put
    /// each at the start of the line: 0 in a left-to-right container and 20 in a right-to-left one.
    /// </summary>
    [Theory]
    [InlineData("auto", "auto", null, 10)]
    [InlineData("auto", "0", null, 20)]
    [InlineData("auto", "auto", "rtl", 10)]
    [InlineData("10px", "auto", "rtl", 10)]
    [InlineData("0", "5px", "rtl", 15)]
    public void The_Margins_Place_A_Block_Level_Image(
        string marginLeft, string marginRight, string? direction, float x)
    {
        var (container, image) = Lay(container =>
        {
            if (direction != null)
                container.Direction = direction;
        }, image =>
        {
            image.MarginLeft = marginLeft;
            image.MarginRight = marginRight;
        });

        AssertBorderBox(x, 300, container, image);
    }

    /// <summary>
    /// <c>text-align</c> places inline content, which a block-level image is not: with
    /// <c>center</c> and with <c>right</c> on its container, it starts at 0. It was at 10 and 20.
    /// </summary>
    [Theory]
    [InlineData("center")]
    [InlineData("right")]
    public void Text_Align_Does_Not_Move_A_Block_Level_Image(string textAlign)
    {
        var (container, image) = Lay(container => container.TextAlign = textAlign, _ => { });

        AssertBorderBox(0, 300, container, image);
    }

    /// <summary>
    /// It is the border box that is centred: a 2px border makes it 304px wide, at 8, with the image
    /// inside it at 10. It was at 0.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Auto_Margins_Centre_The_Border_Box()
    {
        var (container, image) = Lay(_ => { }, image =>
        {
            image.MarginLeft = image.MarginRight = CssConstants.Auto;
            image.BorderLeftWidth = image.BorderRightWidth = image.BorderTopWidth = image.BorderBottomWidth = "2px";
            image.BorderLeftStyle = image.BorderRightStyle = image.BorderTopStyle = image.BorderBottomStyle = "solid";
        });

        AssertBorderBox(8, 304, container, image);
        Assert.Equal(10, Assert.Single(image.Words).Left - container.Location.X, 1);
    }

    /// <summary>
    /// An image wider than its 200px container counts its <c>auto</c> margins as zero and is
    /// over-constrained: it starts at the left of a left-to-right container, and ends at the right of
    /// a right-to-left one, 100px outside its left edge.
    /// </summary>
    [Theory]
    [InlineData(null, 0)]
    [InlineData("rtl", -100)]
    public void An_Image_Too_Wide_For_Its_Container_Overflows_At_The_End(string? direction, float x)
    {
        var (container, image) = Lay(container =>
        {
            container.Width = "200px";
            if (direction != null)
                container.Direction = direction;
        }, image => image.MarginLeft = image.MarginRight = CssConstants.Auto);

        AssertBorderBox(x, 300, container, image);
    }

    /// <summary>
    /// An image that is a column flex item with <c>margin: 0 auto</c> is centred on the column, at
    /// 10, as CSS Flexbox §8.1 centres an item with <c>auto</c> margins. The fix-up does not stretch
    /// such an image, and it was at 0 in the stretched item wrapping it.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Column_Flex_Item_Image_With_Auto_Margins_Is_Centred()
    {
        var (container, image) = Lay(container =>
        {
            container.Display = "flex";
            container.FlexDirection = "column";
        }, image => image.MarginLeft = image.MarginRight = CssConstants.Auto);

        AssertBorderBox(10, 300, container, image);
    }

    /// <summary>
    /// Script reads a centred image where it is drawn: 300×150 at 10, the top of its container.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Script_Reads_A_Centred_Image_Where_It_Is_Drawn()
    {
        var (container, image) = Lay(_ => { }, image => image.MarginLeft = image.MarginRight = CssConstants.Auto);

        var rectangle = ScriptRectangle(image);
        Assert.Equal(10, rectangle.X - container.Location.X, 1);
        Assert.Equal(0, rectangle.Y - container.Location.Y, 1);
        Assert.Equal(300, rectangle.Width, 1);
        Assert.Equal(150, rectangle.Height, 1);
    }

    /// <summary>
    /// Controls, which pass before and after: an image with a 5px left margin starts at 5; one that
    /// is centred with a declared <c>width: 50%</c>, which the fix-up hands to the wrapper, is 160px
    /// wide at 80; and an inline image in a container with <c>text-align: center</c> is centred, at
    /// 10, since it is inline content.
    /// </summary>
    [Theory]
    [InlineData("margin-left", 5, 300)]
    [InlineData("declared width", 80, 160)]
    [InlineData("inline", 10, 300)]
    public void Control_Images_Placed_As_Before(string layout, float x, float width)
    {
        var (container, image) = Lay(container =>
        {
            if (layout == "inline")
                container.TextAlign = CssConstants.Center;
        }, image =>
        {
            switch (layout)
            {
                case "margin-left":
                    image.MarginLeft = "5px";
                    break;
                case "declared width":
                    image.Width = "50%";
                    image.MarginLeft = image.MarginRight = CssConstants.Auto;
                    break;
                case "inline":
                    image.Display = CssConstants.Inline;
                    break;
            }
        });

        AssertBorderBox(x, width, container, image);
    }

    /// <summary>
    /// The image's border box on its line starts <paramref name="x"/> from the container's left edge
    /// and is <paramref name="width"/> wide.
    /// </summary>
    private static void AssertBorderBox(float x, float width, CssBox container, CssBoxImage image)
    {
        var rectangle = Assert.Single(image.Rectangles).Value;
        Assert.Equal(x, rectangle.X - container.Location.X, 1);
        Assert.Equal(width, rectangle.Width, 1);
    }

    /// <summary>
    /// A 320px container, styled by <paramref name="styleContainer"/>, holding a
    /// <c>display: block</c> image styled by <paramref name="styleImage"/> and a 10px block after
    /// it, prepared as <c>DomParser.PrepareCssTree</c> prepares a tree and laid out.
    /// </summary>
    private static (CssBox Container, CssBoxImage Image) Lay(
        Action<CssBox> styleContainer, Action<CssBoxImage> styleImage)
    {
        var root = new CssBox(null, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = CssConstants.Block,
            Location = new PointF(0, 0),
            Size = new SizeF(1024, 768),
            LayoutEnvironment = new ImageLayoutEnvironment(),
        };

        var container = new CssBox(root, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = CssConstants.Block,
            Width = "320px",
        };
        styleContainer(container);

        var image = new CssBoxImage(
            container,
            new HtmlTag("img", true, new Dictionary<string, string> { ["src"] = "i.png" }),
            BaseUrl)
        {
            Display = CssConstants.Block,
        };
        styleImage(image);

        _ = new CssBox(container, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = CssConstants.Block,
            Height = "10px",
        };

        FlexGridItemBlockification.Generate(root);
        WrapBlockLevelImages(root);

        // CorrectInlineBoxesParent: an inline image beside a block goes into an anonymous block.
        if (image.Display == CssConstants.Inline && image.ParentBox == container)
            image.ParentBox = CssBoxHelper.CreateBlock(container, BaseUrl, null, image);

        root.PerformLayout(root.LayoutEnvironment);
        return (container, image);
    }

    /// <summary>
    /// What <c>DomParser.CorrectImgBoxes</c> does: a <c>display: block</c> image that is not a row
    /// flex item is wrapped in an anonymous block and made inline. An image with an <c>auto</c> side
    /// margin and a declared width hands both to the wrapper and fills it; one a column flex container
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
                var wrapper = CssBoxHelper.CreateBlock(box, BaseUrl, null, child);
                child.ParentBox = wrapper;
                child.Display = CssConstants.Inline;

                bool autoMargin = child.MarginLeft == CssConstants.Auto || child.MarginRight == CssConstants.Auto;
                bool declaredWidth = !string.IsNullOrEmpty(child.Width) && child.Width != CssConstants.Auto;
                if (autoMargin && declaredWidth)
                {
                    wrapper.Width = child.Width;
                    wrapper.MarginLeft = child.MarginLeft;
                    wrapper.MarginRight = child.MarginRight;
                    wrapper.MaxWidth = child.MaxWidth;
                    wrapper.MinWidth = child.MinWidth;
                    child.Width = "100%";
                    child.MarginLeft = "0";
                    child.MarginRight = "0";
                }
                else if (stretched)
                {
                    child.Width = "100%";
                }
            }
            else
            {
                WrapBlockLevelImages(child);
            }
        }
    }

    /// <summary>
    /// The border box script reads for the image (<c>getBoundingClientRect</c>), as
    /// <c>HtmlContainerInt.CollectLayoutGeometry</c> in Broiler.HTML takes it: the box's own bounds,
    /// unless they are empty, and otherwise the union of its line rectangles.
    /// </summary>
    private static RectangleF ScriptRectangle(CssBox box)
    {
        if (box.Bounds.Width != 0 || box.Bounds.Height != 0)
            return box.Bounds;

        RectangleF union = RectangleF.Empty;
        foreach (var rectangle in box.Rectangles.Values)
            union = union.IsEmpty ? rectangle : RectangleF.Union(union, rectangle);

        return union;
    }

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
