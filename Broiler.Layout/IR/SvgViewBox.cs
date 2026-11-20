using System.Drawing;
using MediaSvgViewBox = Broiler.Media.Image.SvgViewBox;

namespace Broiler.Layout.IR;

/// <summary>
/// Maps an SVG view box onto a viewport using the geometry shared with SVG image decoders.
/// </summary>
public static class SvgViewBox
{
    /// <summary>The scale and translation from view-box units to viewport coordinates.</summary>
    public readonly record struct Mapping(float ScaleX, float ScaleY, float TranslateX, float TranslateY);

    /// <inheritdoc cref="MediaSvgViewBox.Resolve"/>
    public static Mapping Resolve(string? preserveAspectRatio, RectangleF viewport, RectangleF viewBox)
    {
        var (scaleX, scaleY, translateX, translateY) = MediaSvgViewBox.Resolve(preserveAspectRatio, viewport, viewBox);
        return new Mapping(scaleX, scaleY, translateX, translateY);
    }
}
