using System;
using System.Collections.Generic;
using System.Drawing;
using Broiler.Layout.Engine;
using Broiler.Layout.IR;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// An <c>&lt;object&gt;</c> renders what its data turned out to be once loaded (HTML §4.8.7), which
/// the host knows and markup need not say: an image is a replaced image, a document is a nested
/// document, and anything else leaves the object a box that draws its fallback.
/// </summary>
/// <remarks>
/// Acid3's test 16 nests <c>support-a.png</c> (a 404), <c>support-b.png</c> (a <c>text/html</c>
/// page) and <c>support-c.png</c> (an <c>image/png</c>): no URL ends in a document's extension, and
/// none carries a <c>type</c>. An object was an image only for a <c>data:image</c> URL, so the
/// image loaded from <c>support-c.png</c> was a plain box that drew its "FAIL" fallback. Broiler.HtmlBridge
/// stamps what it loaded as <c>data-broiler-object-type</c>, and a document as the frame document
/// the renderer already reads (<c>data-broiler-frame-document</c>).
/// </remarks>
public sealed class LoadedObjectContentTests
{
    private static readonly Uri BaseUrl = new("http://acid3.test/");

    /// <summary>
    /// Data the host loaded as a raster image is drawn as one, whatever the URL says: the object is
    /// the replaced image box whose fallback Broiler.HTML's <c>CorrectObjectBoxes</c> drops.
    /// </summary>
    [Theory]
    [InlineData("image/png")]
    [InlineData("image/gif")]
    [InlineData("IMAGE/JPEG")]
    public void Data_Loaded_As_An_Image_Is_A_Replaced_Image(string loadedType)
    {
        var box = ObjectBox(new() { ["data"] = "support-c.png", ["data-broiler-object-type"] = loadedType });

        Assert.IsType<CssBoxImage>(box);
    }

    /// <summary>
    /// Data the host loaded as anything but a raster image is not one, even a <c>data:image</c> URL
    /// the host saw as something else, and an SVG image is drawn from its markup instead.
    /// </summary>
    [Theory]
    [InlineData("support-b.png", "text/html")]
    [InlineData("support-b.png", "image/svg+xml")]
    [InlineData("data:image/png;base64,AAAA", "text/html")]
    public void Data_Loaded_As_Anything_Else_Is_No_Image(string data, string loadedType)
    {
        var box = ObjectBox(new() { ["data"] = data, ["data-broiler-object-type"] = loadedType });

        Assert.IsNotType<CssBoxImage>(box);
    }

    /// <summary>Control, which passes before and after: with no host, the URL decides.</summary>
    [Theory]
    [InlineData("data:image/png;base64,AAAA", true)]
    [InlineData("support-c.png", false)]
    public void Control_With_No_Host_A_Data_Image_Url_Is_An_Image(string data, bool image)
    {
        var box = ObjectBox(new() { ["data"] = data });

        Assert.Equal(image, box is CssBoxImage);
    }

    /// <summary>
    /// The author's <c>type</c> is not what decides it: it stays the author's for selectors such
    /// as Acid2's <c>object[type]</c>, and the host says what loaded in an attribute of its own.
    /// </summary>
    [Fact]
    public void The_Authored_Type_Does_Not_Make_An_Image()
    {
        var box = ObjectBox(new() { ["data"] = "support-c.png", ["type"] = "image/png" });

        Assert.IsNotType<CssBoxImage>(box);
    }

    /// <summary>
    /// Data the host loaded as a document is the nested document it stamped, though neither the
    /// URL's extension nor a <c>type</c> names one, and with its URL as the document's base.
    /// </summary>
    [Fact]
    public void Data_Loaded_As_A_Document_Is_The_Stamped_Document()
    {
        const string Markup = "<!DOCTYPE html><html><head><title>FAIL</title></head><body><p></p></body></html>";
        var fragment = ObjectFragment(new()
        {
            ["data"] = "support-b.png",
            ["data-broiler-object-type"] = "text/html",
            ["data-broiler-frame-document"] = Markup,
            ["data-broiler-frame-base"] = "http://acid3.test/support-b.png",
        });

        Assert.Equal(Markup, fragment.EmbeddedDocumentHtml);
        Assert.Equal("http://acid3.test/support-b.png", fragment.EmbeddedDocumentBaseUrl);
        Assert.Null(fragment.ImageHandle);
    }

    private static CssBox ObjectBox(Dictionary<string, string> attributes) =>
        CssBoxHelper.CreateBox(new HtmlTag("object", false, attributes), BaseUrl);

    /// <summary>The fragment of an object box with these attributes, in a root block.</summary>
    private static Fragment ObjectFragment(Dictionary<string, string> attributes)
    {
        var root = new CssBox(null, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = "block",
            Location = new PointF(0, 0),
            Size = new SizeF(300, 300),
            LayoutEnvironment = new FakeLayoutEnvironment(),
        };

        var box = CssBoxHelper.CreateBox(new HtmlTag("object", false, attributes), BaseUrl, root);
        box.Display = "block";
        box.Location = new PointF(10, 10);
        box.Size = new SizeF(100, 100);

        return FragmentTreeBuilder.Build(root).Children[0];
    }

    // The box carries no text or replaced content, so only the font is ever asked for.
    private sealed class FakeLayoutEnvironment : ILayoutEnvironment
    {
        private static readonly Broiler.Graphics.Text.ILayoutFont TheFont = new FakeFont();
        public Broiler.Graphics.Text.ILayoutFont GetFont(string family, double size, LayoutFontStyle style, string? fontFeatures = null) => TheFont;
        public SizeF MeasureText(Broiler.Graphics.Text.ILayoutFont font, string text) => SizeF.Empty;
        public void MeasureText(Broiler.Graphics.Text.ILayoutFont font, string text, double maxWidth, out int charFit, out double charFitWidth) { charFit = 0; charFitWidth = 0; }
        public double GetWhitespaceWidth(Broiler.Graphics.Text.ILayoutFont font) => 0;
        public ImageIntrinsics GetImageIntrinsics(object imageHandle) => default;
        public Broiler.Graphics.Color.BColor ParseColor(string value) => default;
        public void RequestRefresh(bool relayout) { }
        public SizeF ViewportSize => new(300, 300);
        public PointF RootLocation => PointF.Empty;
        public SizeF ActualSize { get; set; }
        public bool AvoidGeometryAntialias => false;
        public SizeF PageSize => new(300, 300);
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
