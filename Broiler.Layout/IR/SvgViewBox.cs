using System;
using System.Drawing;

namespace Broiler.Layout.IR;

/// <summary>
/// SVG 1.1 §7.7 <c>viewBox</c> and §7.8 <c>preserveAspectRatio</c>: the scale and translation that
/// map a view box onto the viewport an element establishes.
/// </summary>
/// <remarks>
/// <para>
/// This is the mapping the renderer applies when it paints a view-boxed <c>&lt;svg&gt;</c>,
/// <c>&lt;symbol&gt;</c> or nested viewport, exposed because a consumer measuring the same document
/// has to agree with it. An element rectangle or a hit test computed against a different mapping puts
/// the box somewhere the paint did not, and the default-only approximation — uniform scale to fit,
/// centred — is wrong for every <c>slice</c> and every corner alignment.
/// </para>
/// <para>
/// A point in view-box user units maps to the viewport's coordinate space as
/// <c>(x · ScaleX + TranslateX, y · ScaleY + TranslateY)</c>, which also carries the view box's own
/// origin: the translations already subtract <c>viewBox.X</c> and <c>viewBox.Y</c> scaled, so a caller
/// does not offset by them again.
/// </para>
/// </remarks>
public static class SvgViewBox
{
    /// <summary>
    /// An affine map from view-box user units onto the viewport, with no rotation or skew — which is
    /// all <c>viewBox</c>/<c>preserveAspectRatio</c> can express.
    /// </summary>
    /// <param name="ScaleX">The horizontal scale. Equal to <paramref name="ScaleY"/> unless the
    /// alignment is <c>none</c>, which is the only value that scales the axes independently.</param>
    /// <param name="ScaleY">The vertical scale.</param>
    /// <param name="TranslateX">The horizontal translation, applied after the scale.</param>
    /// <param name="TranslateY">The vertical translation, applied after the scale.</param>
    public readonly record struct Mapping(float ScaleX, float ScaleY, float TranslateX, float TranslateY);

    /// <summary>
    /// Resolves how <paramref name="viewBox"/> maps onto <paramref name="viewport"/> under
    /// <paramref name="preserveAspectRatio"/>.
    /// </summary>
    /// <param name="preserveAspectRatio">
    /// The element's <c>preserveAspectRatio</c> attribute, or null when it declares none. Only
    /// <c>none</c> and the <c>&lt;align&gt; [meet|slice]</c> forms are read, with the legacy
    /// <c>defer</c> prefix skipped; anything this cannot parse takes the initial
    /// <c>xMidYMid meet</c>, which is also what the attribute's absence means.
    /// </param>
    /// <param name="viewport">The rectangle the view box is mapped onto, in the caller's coordinate space.</param>
    /// <param name="viewBox">The <c>viewBox</c> attribute's four numbers, as a rectangle in user units.</param>
    /// <returns>
    /// The mapping, or a zero-scale <see cref="Mapping"/> when <paramref name="viewBox"/> has a
    /// non-positive width or height. SVG 1.1 §7.7 disables rendering of an element whose view box has
    /// a zero extent and makes a negative one an error, so there is no meaningful scale to report and
    /// a zero one collapses the content to nothing, which is what such an element paints.
    /// </returns>
    public static Mapping Resolve(string? preserveAspectRatio, RectangleF viewport, RectangleF viewBox)
    {
        if (viewBox.Width <= 0 || viewBox.Height <= 0)
            return default;

        var parts = (preserveAspectRatio ?? string.Empty)
            .Split([' ', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries);

        // "defer" is a legacy prefix that only applies to <image>, and is ignored here.
        int first = parts.Length > 0 && parts[0].Equals("defer", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        string align = parts.Length > first ? parts[first] : "xMidYMid";
        bool slice = parts.Length > first + 1
            && parts[first + 1].Equals("slice", StringComparison.OrdinalIgnoreCase);

        float scaleX = viewport.Width / viewBox.Width;
        float scaleY = viewport.Height / viewBox.Height;

        if (align.Equals("none", StringComparison.OrdinalIgnoreCase))
            return new Mapping(scaleX, scaleY, -viewBox.X * scaleX, -viewBox.Y * scaleY);

        float scale = slice ? Math.Max(scaleX, scaleY) : Math.Min(scaleX, scaleY);
        float slackX = viewport.Width - viewBox.Width * scale;
        float slackY = viewport.Height - viewBox.Height * scale;

        // Carried over from the renderer unchanged, asymmetry included: the x half is matched
        // case-insensitively and the Y half is not. The spec's alignment values are case-sensitive
        // keywords, so the Y half is the strict reading and the x half is lenient beyond it; a
        // lowercase "xminymin" therefore aligns x to the minimum and centres Y.
        float alignX = align.Contains("xMin", StringComparison.OrdinalIgnoreCase) ? 0f
            : align.Contains("xMax", StringComparison.OrdinalIgnoreCase) ? slackX
            : slackX / 2f;
        float alignY = align.Contains("YMin", StringComparison.Ordinal) ? 0f
            : align.Contains("YMax", StringComparison.Ordinal) ? slackY
            : slackY / 2f;

        return new Mapping(scale, scale, -viewBox.X * scale + alignX, -viewBox.Y * scale + alignY);
    }
}
