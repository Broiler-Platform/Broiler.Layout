using System;
using System.Collections.Concurrent;
using System.Drawing;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// A line in a block with <c>font: 0/0</c> reaches down to its baseline, where its strut stands,
/// however high vertical alignment raises the boxes on it.
/// </summary>
/// <remarks>
/// <para>
/// CSS 2.1 §10.8: every line box starts with a strut, an empty inline box in the block's font and
/// line height, and the line box reaches from the top of the highest box on it to the bottom of the
/// lowest (§10.8.1), the strut's among them. In a <c>font: 0/0</c> block the strut has no height and
/// stands on the baseline, so a line of inline-blocks raised above the baseline ends at the
/// baseline, not at the bottom of the lowest of them. The engine counted the strut only for a
/// block with a line height, and the line ended where the lowest inline-block does.
/// </para>
/// <para>
/// Acid3 builds its row of six coloured buckets so: in <c>.buckets { font: 0/0 }</c>, each bucket
/// an empty inline-block with <c>vertical-align: 2em</c>, <c>1em</c> of padding above and below and a
/// 1px border, the largest in a 40px font. Its stylesheet works the line out to be 162px tall, the
/// largest bucket's 82px and the 80px it is raised; the engine made it 122px, ending at the bottom
/// of the 20px bucket, raised 40px, and the score below it stood 40px too high.
/// </para>
/// </remarks>
public sealed class ZeroLineHeightStrutTests
{
    private static readonly Uri BaseUrl = new("file:///zero-line-height-strut.html");

    /// <summary>
    /// A 40px bucket, 82px tall and raised 80px, alone: the line is 162px tall, and the bucket
    /// stands at its top. The line was 82px tall.
    /// </summary>
    [Fact]
    public void A_Raised_Inline_Block_Alone_Makes_The_Line_Reach_Down_To_The_Baseline()
    {
        var block = Block();
        var bucket = Bucket(block, "40px");
        Layout(block);

        Assert.Equal(0, bucket.Location.Y - block.Location.Y, 1);
        Assert.Equal(82, bucket.Size.Height, 1);
        Assert.Equal(162, block.Size.Height, 1);
    }

    /// <summary>
    /// Acid3's six buckets, in 20 to 40px fonts: the line is 162px tall, each bucket's bottom is
    /// two of its ems above the baseline, and the 20px one's is 40px above it. The line was 122px
    /// tall, ending at the 20px bucket's bottom.
    /// </summary>
    [Fact]
    public void Acid3s_Buckets_Make_A_162px_Line()
    {
        var block = Block();
        var buckets = new CssBox[6];
        for (int i = 0; i < 6; i++)
            buckets[i] = Bucket(block, (20 + 4 * i) + "px");
        Layout(block);

        Assert.Equal(162, block.Size.Height, 1);
        for (int i = 0; i < 6; i++)
        {
            double em = 20 + 4 * i;
            Assert.Equal(2 + 2 * em, buckets[i].Size.Height, 1);
            Assert.Equal(162 - 2 * em, buckets[i].Location.Y + buckets[i].Size.Height - block.Location.Y, 1);
        }
    }

    /// <summary>
    /// A block after the buckets' block, with its 150px of bottom padding as Acid3 gives it, starts
    /// 312px down. It started 272px down.
    /// </summary>
    [Fact]
    public void The_Next_Block_Starts_Below_The_Baseline_And_The_Padding()
    {
        var block = Block();
        block.PaddingBottom = "150px";
        for (int i = 0; i < 6; i++)
            Bucket(block, (20 + 4 * i) + "px");
        var next = new CssBox(block.ParentBox, new HtmlTag("div", false, null), BaseUrl) { Display = "block", Height = "10px" };
        Layout(block);

        Assert.Equal(312, block.Size.Height, 1);
        Assert.Equal(312, next.Location.Y - block.Location.Y, 1);
    }

    /// <summary>
    /// Laid out a second time, the line is 162px tall still.
    /// </summary>
    [Fact]
    public void A_Second_Layout_Gives_The_Same_Line()
    {
        var block = Block();
        Bucket(block, "20px");
        Bucket(block, "40px");
        Layout(block);
        Layout(block);

        Assert.Equal(162, block.Size.Height, 1);
    }

    /// <summary>
    /// Controls: a bucket lowered 2em below the baseline instead reaches below it, and the line is
    /// as tall as the bucket, 82px; one on the baseline makes the line 82px tall too.
    /// </summary>
    [Theory]
    [InlineData("-2em")]
    [InlineData(CssConstants.Baseline)]
    public void Control_A_Bucket_Not_Raised(string verticalAlign)
    {
        var block = Block();
        var bucket = Bucket(block, "40px");
        bucket.VerticalAlign = verticalAlign;
        Layout(block);

        Assert.Equal(0, bucket.Location.Y - block.Location.Y, 1);
        Assert.Equal(82, block.Size.Height, 1);
    }

    /// <summary>A 562px block with <c>font: 0/0</c> in a block in the root.</summary>
    private static CssBox Block()
    {
        var root = new CssBox(null, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = "block",
            Location = new PointF(0, 0),
            Size = new SizeF(1024, 768),
            LayoutEnvironment = new FakeLayoutEnvironment(),
        };

        var body = new CssBox(root, new HtmlTag("div", false, null), BaseUrl) { Display = "block" };
        return new CssBox(body, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = "block",
            Width = "562px",
            FontSize = "0px",
            LineHeight = "0",
        };
    }

    private static void Layout(CssBox block)
    {
        var root = block.ParentBox!.ParentBox!;
        root.PerformLayout(root.LayoutEnvironment);
    }

    /// <summary>
    /// An empty inline-block in the given font, inheriting the block's line height, raised 2em,
    /// with 1em of padding above and below it and a 1px border, as Acid3's buckets are.
    /// </summary>
    private static CssBox Bucket(CssBox block, string fontSize)
    {
        var bucket = new CssBox(block, new HtmlTag("p", false, null), BaseUrl);
        bucket.InheritStyle();
        bucket.Display = CssConstants.InlineBlock;
        bucket.FontSize = fontSize;
        bucket.VerticalAlign = "2em";
        bucket.PaddingTop = bucket.PaddingBottom = "1em";
        bucket.PaddingLeft = "2em";
        bucket.BorderLeftWidth = bucket.BorderTopWidth = bucket.BorderRightWidth = bucket.BorderBottomWidth = "1px";
        bucket.BorderLeftStyle = bucket.BorderTopStyle = bucket.BorderRightStyle = bucket.BorderBottomStyle = "solid";
        return bucket;
    }

    private sealed class FakeLayoutEnvironment : ILayoutEnvironment
    {
        private static readonly ConcurrentDictionary<double, FakeFont> Fonts = new();
        public Broiler.Graphics.Text.ILayoutFont GetFont(string family, double size, LayoutFontStyle style, string? fontFeatures = null) => Fonts.GetOrAdd(size, s => new FakeFont(s));
        public SizeF MeasureText(Broiler.Graphics.Text.ILayoutFont font, string text) => new(8 * text.Length, (float)font.Height);
        public void MeasureText(Broiler.Graphics.Text.ILayoutFont font, string text, double maxWidth, out int charFit, out double charFitWidth) { charFit = text.Length; charFitWidth = 8 * text.Length; }
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
        public ILayoutImageLoader CreateImageLoader(Action<object?, RectangleF, bool> onComplete) => throw new NotSupportedException();
        public string FormatListMarker(int number, string style) => string.Empty;
    }

    /// <summary>A font of the given size in points, as tall in pixels as its size in pixels.</summary>
    private sealed class FakeFont(double size) : Broiler.Graphics.Text.ILayoutFont
    {
        public double Size => size;
        public double Height => size * 4 / 3;
        public double UnderlineOffset => 0;
        public double LeftPadding => 0;
        public string? FontFeatures => null;
    }
}
