using System;
using System.Drawing;
using Broiler.Layout.Engine;
using Broiler.Layout.IR;
using Xunit;

namespace Broiler.Layout.Tests;

public sealed class EmptyLineFragmentTests
{
    [Fact]
    public void UnusedLinesAreOmittedButGeometricEmptyLinesKeepTheirPosition()
    {
        var box = new CssBox(null, null, new Uri("https://example.test/"))
        {
            Display = "block", Location = new PointF(10, 100), Size = new SizeF(100, 40),
        };
        box.LayoutEnvironment = new FakeLayoutEnvironment();
        var first = new CssLineBox(box);
        first.Rectangles[box] = new RectangleF(10, 100, 30, 20);
        _ = new CssLineBox(box);
        var blank = new CssLineBox(box);
        blank.Rectangles[box] = new RectangleF(10, 120, 0, 20);
        _ = new CssLineBox(box);

        var fragment = FragmentTreeBuilder.Build(box);

        Assert.Equal(2, fragment.Lines!.Count);
        Assert.Equal(100, fragment.Lines[0].Y);
        Assert.Equal(120, fragment.Lines[1].Y);
        Assert.Equal(20, fragment.Lines[1].Height);
        Assert.Empty(FragmentInvariantChecker.Check(fragment));
    }
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
        public SizeF ViewportSize => new(1000, 1000);
        public PointF RootLocation => PointF.Empty;
        public SizeF ActualSize { get; set; }
        public bool AvoidGeometryAntialias => false;
        public SizeF PageSize => new(1000, 1000);
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
