using System;
using System.Drawing;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// HTML §15.3.13: a fieldset's rendered legend is centred on its block-start border. A legend
/// taller than the border sits at the top of the fieldset's border box, which grows to hold it, and
/// one thinner than the border sits in the middle of it; the content begins below whichever reaches
/// further down, plus the padding. Each expectation was measured in Chromium.
/// </summary>
/// <remarks>
/// The legend's margin box was centred on the border instead, so a legend taller than it stood
/// above the fieldset's border box, over whatever came before: the 18px legend of reCAPTCHA's demo
/// form reached 8px into the paragraph above it. Chromium draws the border through the legend's
/// middle and keeps the legend inside the fieldset's box.
/// </remarks>
public sealed class FieldsetLegendPlacementTests
{
    private static readonly Uri BaseUrl = new("file:///fieldset.html");

    /// <summary>
    /// An 18px legend on a 2px border starts where the fieldset does, and the content 18px plus the
    /// 5px of padding below that: Chromium's demo form, with its 5.6px of padding rounded.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Legend_Taller_Than_The_Border_Sits_At_The_Top_Of_The_Fieldset()
    {
        var (root, fieldset, legend, content) = Fieldset(border: "2px", legendHeight: "18px");

        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(fieldset.Location.Y, legend.Location.Y, 1);
        Assert.Equal(fieldset.Location.Y + 18 + 5, content.Location.Y, 1);
        Assert.Equal(18 + 5 + 10 + 10 + 2, fieldset.Size.Height, 1);
    }

    /// <summary>
    /// An 18px legend in a 20px border is 1px down, in the border's middle, and the content starts
    /// below the border.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Legend_Thinner_Than_The_Border_Sits_In_Its_Middle()
    {
        var (root, fieldset, legend, content) = Fieldset(border: "20px", legendHeight: "18px");

        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(fieldset.Location.Y + 1, legend.Location.Y, 1);
        Assert.Equal(fieldset.Location.Y + 20 + 5, content.Location.Y, 1);
    }

    /// <summary>The paragraph before the fieldset ends where the legend begins.</summary>
    [Fact(Timeout = 600000)]
    public void The_Legend_Stays_Below_What_Comes_Before()
    {
        var (root, fieldset, legend, _) = Fieldset(border: "2px", legendHeight: "18px");
        var body = fieldset.ParentBox!;
        var before = body.Boxes[0];

        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(before.Location.Y + before.Size.Height, legend.Location.Y, 1);
    }

    private static (CssBox Root, CssBox Fieldset, CssBox Legend, CssBox Content) Fieldset(string border, string legendHeight)
    {
        var root = new CssBox(null, new HtmlTag("html", false, null), BaseUrl)
        {
            Display = CssConstants.Block,
            Location = new PointF(0, 0),
            Size = new SizeF(1024, 768),
            LayoutEnvironment = new FakeLayoutEnvironment(),
        };
        var body = new CssBox(root, new HtmlTag("body", false, null), BaseUrl) { Display = CssConstants.Block };
        new CssBox(body, new HtmlTag("div", false, null), BaseUrl) { Display = CssConstants.Block, Height = "18px" };

        var fieldset = new CssBox(body, new HtmlTag("fieldset", false, null), BaseUrl)
        {
            Display = CssConstants.Block,
            Width = "300px",
            BorderTopWidth = border,
            BorderBottomWidth = "2px",
            BorderTopStyle = "solid",
            BorderBottomStyle = "solid",
            PaddingTop = "5px",
            PaddingBottom = "10px",
        };
        var legend = new CssBox(fieldset, new HtmlTag("legend", false, null), BaseUrl)
        {
            Display = CssConstants.Block,
            Width = "100px",
            Height = legendHeight,
        };
        var content = new CssBox(fieldset, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = CssConstants.Block,
            Height = "10px",
        };

        return (root, fieldset, legend, content);
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
