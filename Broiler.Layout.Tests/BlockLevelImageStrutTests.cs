using System;
using System.Collections.Generic;
using System.Drawing;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// A block-level image is on no line, so no strut (CSS2.1 §10.8) is laid out with it: the next box
/// starts directly below the image, and an image shorter than the strut takes only its own height.
/// </summary>
/// <remarks>
/// <para>
/// Block flow here cannot position a block-level replaced box, so the box fix-up in Broiler.HTML
/// (<c>DomParser.CorrectImgBoxes</c>) wraps each <c>display: block</c> image that is not a row flex
/// item in an anonymous block and makes the image inline inside it. That is an image declared
/// <c>display: block</c>, and every image that is a column flex item or a grid item, which
/// blockification makes block-level. The wrapper's one line had a strut, so the image stood on its
/// baseline: the next box started the strut's descent below the image, and a short image was pushed
/// down onto the baseline.
/// </para>
/// <para>
/// Each tree is built the way <c>DomParser.PrepareCssTree</c> leaves it: the box fix-ups start with
/// <see cref="FlexGridItemBlockification.Generate"/>, and <see cref="WrapBlockLevelImages"/> then
/// does what <c>CorrectImgBoxes</c> does. Words are 8px wide and 16px tall, and an image is a 300×150
/// bitmap.
/// </para>
/// </remarks>
public sealed class BlockLevelImageStrutTests
{
    private static readonly Uri BaseUrl = new("file:///block-level-image-strut.html");

    /// <summary>
    /// The next box starts where the image ends, with no descent between them: after a
    /// <c>display: block</c> image in a block, after an image that is an item of a column flex
    /// container that does not stretch it (<c>flex-start</c>, and <c>center</c> with a 100px width),
    /// and after an image that is a grid item. Each started 3.2px lower, the strut's descent here.
    /// </summary>
    [Theory]
    [InlineData("block", null, 150)]
    [InlineData("column", null, 150)]
    [InlineData("centered column", "100px", 50)]
    [InlineData("grid", null, 150)]
    public void The_Next_Box_Starts_Directly_Below_A_Block_Level_Image(
        string layout, string? width, float bottom)
    {
        var (_, container, image, next) = Lay(layout, image => image.Width = width ?? CssConstants.Auto);

        Assert.Equal(0, ImageTop(image, container), 1);
        Assert.Equal(bottom, next.Location.Y - container.Location.Y, 1);
    }

    /// <summary>
    /// A 10px-tall image takes 10px. It was lowered onto the strut's baseline and its wrapper was as
    /// tall as the strut, and taller still with <c>line-height: 40px</c> on the container.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("40px")]
    public void A_Short_Block_Level_Image_Takes_Only_Its_Height(string? lineHeight)
    {
        var (_, container, image, next) = Lay("block", image => image.Height = "10px", lineHeight);

        Assert.Equal(0, ImageTop(image, container), 1);
        Assert.Equal(10, next.Location.Y - container.Location.Y, 1);
    }

    /// <summary>The image's own vertical margins stay around it: 5px above and below.</summary>
    [Fact(Timeout = 600000)]
    public void Its_Margins_Stay_Around_It()
    {
        var (_, container, image, next) = Lay("block", image =>
        {
            image.MarginTop = "5px";
            image.MarginBottom = "5px";
        });

        Assert.Equal(5, ImageTop(image, container), 1);
        Assert.Equal(160, next.Location.Y - container.Location.Y, 1);
    }

    /// <summary>
    /// <c>vertical-align</c> does not apply to a block-level box, as in the reset that declares
    /// <c>img { display: block; vertical-align: middle }</c>. The image stays at the top, and the
    /// geometry script reads puts it there too: alignment had given the box a position of its own,
    /// 52.6px above its container and 0px wide, which script read instead of its line rectangle.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Vertical_Align_Does_Not_Move_A_Block_Level_Image()
    {
        var (_, container, image, next) = Lay("block", image => image.VerticalAlign = CssConstants.Middle);

        Assert.Equal(0, ImageTop(image, container), 1);
        Assert.Equal(150, next.Location.Y - container.Location.Y, 1);
        AssertRectangle(0, 0, 300, 150, ScriptRectangle(image));
    }

    /// <summary>
    /// The same for an image the strut lowered: script reads it where it is drawn, 20×10 at the top,
    /// not 0px wide at the baseline it was lowered to.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Script_Reads_A_Short_Block_Level_Image_Where_It_Is_Drawn()
    {
        var (_, _, image, _) = Lay("block", image => image.Height = "10px");

        AssertRectangle(0, 0, 20, 10, ScriptRectangle(image));
    }

    /// <summary>
    /// In paged media the image still starts the next page rather than straddle a page break, as the
    /// flow places every word. After 700px of a 768px page, a 150px image starts the next page, at
    /// 769, and the next box follows it directly.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Block_Level_Image_That_Would_Straddle_A_Page_Break_Starts_The_Next_Page()
    {
        var (_, container, image, next) = Lay("block", _ => { }, spacer: 700);

        Assert.Equal(769, ImageTop(image, container), 1);
        Assert.Equal(919, next.Location.Y - container.Location.Y, 1);
    }

    /// <summary>
    /// Control, which passes before and after: an inline image is on a line, and its line keeps the
    /// strut's descent below it, as in browsers. Beside a block, the block-inside-inline correction
    /// puts it in an anonymous block of its own, the same shape as a block-level image's wrapper, so
    /// the next box starts 3.2px below it.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Control_An_Inline_Image_Keeps_The_Descent_Below_It()
    {
        var (_, container, image, next) = Lay("inline", _ => { });

        Assert.Equal(0, ImageTop(image, container), 1);
        Assert.Equal(153.2, next.Location.Y - container.Location.Y, 1);
    }

    /// <summary>
    /// The fix-up that blockifies flex and grid items records which images are block-level, before
    /// the next fix-up rewrites their <c>display</c>: a <c>display: block</c> image, and an image that
    /// is an item of a column flex container, a grid container or a row flex container, the last of
    /// which is left unwrapped. An inline image in a block is not block-level.
    /// </summary>
    [Theory]
    [InlineData("block", true)]
    [InlineData("column", true)]
    [InlineData("grid", true)]
    [InlineData("row", true)]
    [InlineData("inline", false)]
    public void Blockification_Records_Which_Images_Are_Block_Level(string layout, bool blockLevel)
    {
        var (root, _, image, _) = Build(layout, _ => { }, lineHeight: null, spacer: 0);

        FlexGridItemBlockification.Generate(root);

        Assert.Equal(blockLevel, image.IsBlockLevel);
    }

    /// <summary>
    /// A 320px-wide container holding an image and, after it, a 10px-tall block, prepared as
    /// <c>DomParser.PrepareCssTree</c> prepares a tree and laid out. A <paramref name="spacer"/>
    /// block that many px tall comes first when it is not 0.
    /// </summary>
    private static (CssBox Root, CssBox Container, CssBoxImage Image, CssBox Next) Lay(
        string layout, Action<CssBoxImage> style, string? lineHeight = null, int spacer = 0)
    {
        var (root, container, image, next) = Build(layout, style, lineHeight, spacer);

        FlexGridItemBlockification.Generate(root);
        WrapBlockLevelImages(root);

        // CorrectInlineBoxesParent: an inline image beside a block goes into an anonymous block.
        if (layout == "inline")
            image.ParentBox = CssBoxHelper.CreateBlock(container, BaseUrl, null, image);

        root.PerformLayout(root.LayoutEnvironment);
        return (root, container, image, next);
    }

    private static (CssBox Root, CssBox Container, CssBoxImage Image, CssBox Next) Build(
        string layout, Action<CssBoxImage> style, string? lineHeight, int spacer)
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

        if (lineHeight != null)
            container.LineHeight = lineHeight;

        switch (layout)
        {
            case "row":
                container.Display = "flex";
                break;
            case "column":
                container.Display = "flex";
                container.FlexDirection = "column";
                container.AlignItems = "flex-start";
                break;
            case "centered column":
                container.Display = "flex";
                container.FlexDirection = "column";
                container.AlignItems = CssConstants.Center;
                break;
            case "grid":
                container.Display = "grid";
                break;
        }

        if (spacer > 0)
        {
            _ = new CssBox(container, new HtmlTag("div", false, null), BaseUrl)
            {
                Display = CssConstants.Block,
                Height = $"{spacer}px",
            };
        }

        // An <img> is inline; a flex or grid item is blockified, and "block" declares it block-level.
        var image = new CssBoxImage(
            container,
            new HtmlTag("img", true, new Dictionary<string, string> { ["src"] = "i.png" }),
            BaseUrl)
        {
            Display = layout == "block" ? CssConstants.Block : CssConstants.Inline,
        };

        style(image);

        var next = new CssBox(container, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = CssConstants.Block,
            Width = "50px",
            Height = "10px",
        };

        return (root, container, image, next);
    }

    /// <summary>
    /// What <c>DomParser.CorrectImgBoxes</c> does: a <c>display: block</c> image that is not a row
    /// flex item is wrapped in an anonymous block and made inline, and one a column flex container
    /// will stretch is given <c>width: 100%</c> of the wrapper. (Its auto-margin case, which moves a
    /// declared width and the margins onto the wrapper, does not arise here.)
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

    /// <summary>Where the image is drawn: the top of its line rectangle, below the container's top.</summary>
    private static double ImageTop(CssBoxImage image, CssBox container)
    {
        Assert.Single(image.Rectangles);
        foreach (var rectangle in image.Rectangles.Values)
            return rectangle.Y - container.Location.Y;

        return double.NaN;
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

    private static void AssertRectangle(float x, float y, float width, float height, RectangleF actual)
    {
        Assert.Equal(x, actual.X, 1);
        Assert.Equal(y, actual.Y, 1);
        Assert.Equal(width, actual.Width, 1);
        Assert.Equal(height, actual.Height, 1);
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
