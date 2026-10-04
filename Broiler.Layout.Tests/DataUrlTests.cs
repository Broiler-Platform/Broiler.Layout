using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Broiler.Layout.Engine;
using Broiler.Layout.IR;
using Broiler.Layout.Net;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// <c>data:</c> URLs decode as a browser decodes them: web-platform-tests' own vectors for the
/// <c>data:</c> URL processor and its forgiving-base64 bodies (copied under <c>wpt/</c>), and the
/// nested documents and SVG images <see cref="FragmentTreeBuilder"/> reads from them.
/// </summary>
/// <remarks>
/// A frame's <c>data:text/html</c> document was everything after the URL's first comma, so a
/// base64 document was painted as its own base64 text. An <c>&lt;object&gt;</c>'s
/// <c>data:image/svg+xml</c> image was read the same way, so a base64 one painted nothing: the
/// base64 branch after it, which needed the body padded besides, was never reached.
/// </remarks>
public sealed class DataUrlTests
{
    private static readonly Uri BaseUrl = new("file:///data-urls.html");

    private static readonly IReadOnlyList<Vector> Base64Vectors =
        LoadVectors("base64.json", static entry => Bytes(entry[1]) is { } body
            ? new Vector("data:;base64," + entry[0].GetString(), "text/plain", body)
            : new Vector("data:;base64," + entry[0].GetString(), null, null));

    private static readonly IReadOnlyList<Vector> DataUrlVectors =
        LoadVectors("data-urls.json", static entry => entry.Length > 2
            ? new Vector(entry[0].GetString()!, entry[1].GetString()!.Split(';')[0], Bytes(entry[2]))
            : new Vector(entry[0].GetString()!, null, null));

    /// <summary>
    /// The vectors the URL parser decides rather than the processor: this processor takes the URL as
    /// written, so their failures are the parser's (see <see cref="DataUrl"/>).
    /// </summary>
    private static readonly HashSet<string> ParserVectors = ["data://test:test/,X"];

    public static TheoryData<int> Base64VectorIndexes() => Indexes(Base64Vectors.Count);

    public static TheoryData<int> DataUrlVectorIndexes() => Indexes(DataUrlVectors.Count);

    [Theory]
    [MemberData(nameof(Base64VectorIndexes))]
    public void A_Base64_Body_Decodes_As_Wpt_Expects(int index) => AssertVector(Base64Vectors[index]);

    [Theory]
    [MemberData(nameof(DataUrlVectorIndexes))]
    public void A_Data_Url_Decodes_As_Wpt_Expects(int index)
    {
        var vector = DataUrlVectors[index];
        if (!ParserVectors.Contains(vector.Input))
            AssertVector(vector);
    }

    [Fact]
    public void The_Mime_Type_Is_The_Essence_Without_The_Base64_Marker()
    {
        Assert.True(DataUrl.TryParse("data:IMAGE/SVG+XML;base64,PHN2Zy8+", out var mimeType, out _));
        Assert.Equal("image/svg+xml", mimeType);

        Assert.True(DataUrl.TryParse("data: text/html ;charset=utf-8,X", out mimeType, out _));
        Assert.Equal("text/html", mimeType);

        // No type, or one that does not parse, is text/plain.
        Assert.True(DataUrl.TryParse("data:;charset=x;base64,WA", out mimeType, out _));
        Assert.Equal("text/plain", mimeType);

        Assert.True(DataUrl.TryParse("data:image;base64,WA", out mimeType, out _));
        Assert.Equal("text/plain", mimeType);
    }

    [Fact]
    public void A_Text_Body_Is_Utf8_Without_Its_Byte_Order_Mark()
    {
        Assert.Equal("é", DataUrl.Utf8Decode([0xEF, 0xBB, 0xBF, 0xC3, 0xA9]));
        Assert.Equal("﻿A", DataUrl.Utf8Decode([0xEF, 0xBB, 0xBF, 0xEF, 0xBB, 0xBF, (byte)'A']));
        Assert.Equal("�A", DataUrl.Utf8Decode([0xFF, (byte)'A']));
        Assert.Equal(string.Empty, DataUrl.Utf8Decode([]));
    }

    /// <summary>
    /// <c>&lt;iframe src="data:text/html;base64,…"&gt;</c> without its padding: the document is the
    /// decoded markup, as UTF-8, with the container's base URL. It was the base64 text.
    /// </summary>
    [Fact]
    public void An_Unpadded_Base64_Frame_Document_Is_Its_Markup()
    {
        const string Markup = "<p style=\"color:green\">déjà vu!</p>";
        var frame = Embedded("iframe", new() { ["src"] = "data:text/html;base64," + Unpadded(Markup) });

        Assert.Equal(Markup, frame.EmbeddedDocumentHtml);
        Assert.Equal(BaseUrl.AbsoluteUri, frame.EmbeddedDocumentBaseUrl);
    }

    /// <summary>Control, which passes before and after: a percent-encoded document.</summary>
    [Fact]
    public void Control_A_Percent_Encoded_Frame_Document_Is_Its_Markup()
    {
        var frame = Embedded("iframe", new() { ["src"] = "data:text/html;charset=utf-8,%3Cp%3Ed%C3%A9j%C3%A0%3C/p%3E" });

        Assert.Equal("<p>déjà</p>", frame.EmbeddedDocumentHtml);
    }

    /// <summary>
    /// A base64 body one character past a whole quantum does not decode, so the frame has no
    /// document, as a browser fails it. Its base64 text was the document.
    /// </summary>
    [Fact]
    public void A_Base64_Frame_Document_That_Does_Not_Decode_Is_No_Document()
    {
        var frame = Embedded("iframe", new() { ["src"] = "data:text/html;base64,PHA+aGk8L3A+A" });

        Assert.Null(frame.EmbeddedDocumentHtml);
    }

    /// <summary>A <c>data:</c> URL that declares no HTML type is no document to paint.</summary>
    [Fact]
    public void A_Data_Url_That_Is_Not_Html_Is_No_Frame_Document()
    {
        var frame = Embedded("iframe", new() { ["src"] = "data:text/plain,<p>hi</p>" });

        Assert.Null(frame.EmbeddedDocumentHtml);
    }

    /// <summary>
    /// An <c>&lt;object&gt;</c> with no <c>type</c> takes the type its <c>data:</c> URL declares,
    /// as it takes one from a response: a <c>data:text/html</c> URL is a nested document. Only its
    /// extension was read, and a <c>data:</c> URL has none.
    /// </summary>
    [Fact]
    public void An_Object_Takes_Its_Type_From_Its_Data_Url()
    {
        const string Markup = "<p>object</p>";
        var html = Embedded("object", new() { ["data"] = "data:text/html;base64," + Unpadded(Markup) });

        Assert.Equal(Markup, html.EmbeddedDocumentHtml);
        Assert.Null(html.SvgContent);
    }

    /// <summary>
    /// <c>&lt;object data="data:image/svg+xml;base64,…"&gt;</c> without its padding: the SVG is the
    /// decoded markup. It was the base64 text, which draws nothing.
    /// </summary>
    [Fact]
    public void An_Unpadded_Base64_Svg_Object_Is_Its_Markup()
    {
        const string Svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"20\" height=\"20\"><rect width=\"20\" height=\"20\" fill=\"red\"/></svg>";
        var image = Embedded("object", new() { ["data"] = "data:image/svg+xml;base64," + Unpadded(Svg) });

        Assert.Equal(Svg, image.SvgContent);
        Assert.Null(image.EmbeddedDocumentHtml);
    }

    /// <summary>Control, which passes before and after: a percent-encoded SVG.</summary>
    [Fact]
    public void Control_A_Percent_Encoded_Svg_Object_Is_Its_Markup()
    {
        var image = Embedded("object", new() { ["data"] = "data:image/svg+xml,%3Csvg%20xmlns='http://www.w3.org/2000/svg'/%3E" });

        Assert.Equal("<svg xmlns='http://www.w3.org/2000/svg'/>", image.SvgContent);
    }

    /// <summary>
    /// A WPT vector: the URL, and the essence of its MIME type and its body when it decodes, both
    /// <see langword="null"/> when it does not.
    /// </summary>
    private sealed record Vector(string Input, string? Essence, byte[]? Body);

    private static void AssertVector(Vector vector)
    {
        var decoded = DataUrl.TryParse(vector.Input, out var mimeType, out var body);
        var shown = JsonSerializer.Serialize(vector.Input);
        if (vector.Body is null)
        {
            Assert.False(decoded, $"{shown} should not decode");
            return;
        }

        Assert.True(decoded, $"{shown} should decode");
        Assert.Equal(vector.Essence, mimeType);
        Assert.Equal(vector.Body, body);
    }

    /// <summary><paramref name="text"/>'s UTF-8 bytes in base64, without the padding an encoder adds.</summary>
    private static string Unpadded(string text)
    {
        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
        Assert.EndsWith("=", base64);
        return base64.TrimEnd('=');
    }

    /// <summary>The fragment of a <paramref name="tagName"/> box with these attributes, in a root block.</summary>
    private static Fragment Embedded(string tagName, Dictionary<string, string> attributes)
    {
        var root = new CssBox(null, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = "block",
            Location = new PointF(0, 0),
            Size = new SizeF(300, 300),
            LayoutEnvironment = new FakeLayoutEnvironment(),
        };

        _ = new CssBox(root, new HtmlTag(tagName, false, attributes), BaseUrl)
        {
            Display = "block",
            Location = new PointF(10, 10),
            Size = new SizeF(100, 100),
        };

        return FragmentTreeBuilder.Build(root).Children[0];
    }

    private static IReadOnlyList<Vector> LoadVectors(string file, Func<JsonElement[], Vector> select)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "wpt", "fetch", "data-urls", "resources", file);
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return [.. document.RootElement.EnumerateArray()
            .Where(static entry => entry.ValueKind == JsonValueKind.Array)
            .Select(entry => select([.. entry.EnumerateArray()]))];
    }

    private static byte[]? Bytes(JsonElement expected) =>
        expected.ValueKind == JsonValueKind.Null
            ? null
            : [.. expected.EnumerateArray().Select(static b => (byte)b.GetInt32())];

    private static TheoryData<int> Indexes(int count)
    {
        var data = new TheoryData<int>();
        for (var i = 0; i < count; i++)
            data.Add(i);
        return data;
    }

    // These boxes carry no text or replaced content, so only the font is ever asked for.
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
