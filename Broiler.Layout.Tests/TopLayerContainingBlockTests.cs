using System;
using System.Collections.Generic;
using System.Drawing;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// A box in the top layer -- an open modal dialog, a showing popover, a <c>::backdrop</c> -- is placed against
/// the viewport whatever its ancestors are: the <c>transform</c> that makes an ancestor the containing block of
/// every fixed box under it does not capture one. Chromium, measured: a modal
/// dialog in a transformed 200x200 container is centred in the viewport, and its backdrop covers the viewport.
/// </summary>
/// <remarks>
/// The bridge marks a top-layer element with its order (<c>data-broiler-top-layer</c>). The box was centred in
/// the container, and its <c>::backdrop</c> covered the container alone.
/// </remarks>
public sealed class TopLayerContainingBlockTests
{
    private static readonly Uri BaseUrl = new("file:///top-layer-containing-block.html");

    [Fact]
    public void A_Top_Layer_Box_In_A_Transformed_Container_Is_Centred_In_The_Viewport()
    {
        var box = Lay(topLayer: true);

        Assert.Equal(462, box.Location.X, 1);
        Assert.Equal(359, box.Location.Y, 1);
    }

    /// <summary>Control, which passes before and after: a fixed box that is not in the top layer is the container's.</summary>
    [Fact]
    public void Control_A_Fixed_Box_In_A_Transformed_Container_Is_Centred_In_It()
    {
        var box = Lay(topLayer: false);

        Assert.Equal(150, box.Location.X, 1);
        Assert.Equal(175, box.Location.Y, 1);
    }

    // A 200x200 container at (100, 100) with a transform, and in it a 100x50 fixed box with all four insets 0
    // and auto margins: centred in its containing block, whichever that is.
    private static CssBox Lay(bool topLayer)
    {
        var root = new CssBox(null, null, BaseUrl)
        {
            Display = "block",
            Location = new PointF(0, 0),
            Size = new SizeF(1024, 768),
            LayoutEnvironment = new FakeLayoutEnvironment(),
        };

        var html = new CssBox(root, new HtmlTag("html", false, null), BaseUrl) { Display = "block" };
        var body = new CssBox(html, new HtmlTag("body", false, null), BaseUrl) { Display = "block", MarginTop = "0", MarginBottom = "0" };
        var container = new CssBox(body, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = "block",
            Position = CssConstants.Absolute,
            Left = "100px",
            Top = "100px",
            Width = "200px",
            Height = "200px",
            Transform = "translateX(0)",
        };

        var attributes = topLayer ? new Dictionary<string, string> { ["data-broiler-top-layer"] = "1" } : null;
        var box = new CssBox(container, new HtmlTag("dialog", false, attributes), BaseUrl)
        {
            Display = "block",
            Position = CssConstants.Fixed,
            Top = "0",
            Right = "0",
            Bottom = "0",
            Left = "0",
            Width = "100px",
            Height = "50px",
            MarginTop = CssConstants.Auto,
            MarginRight = CssConstants.Auto,
            MarginBottom = CssConstants.Auto,
            MarginLeft = CssConstants.Auto,
        };

        root.PerformLayout(root.LayoutEnvironment);
        return box;
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
        public double Size => 12;
        public double Height => 16;
        public double UnderlineOffset => 0;
        public double LeftPadding => 0;
        public string? FontFeatures => null;
    }
}
