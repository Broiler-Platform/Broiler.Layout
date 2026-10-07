using Broiler.CSS;
using System;

namespace Broiler.Layout.Engine;

/// <summary>
/// HTML §15.5.13 — the <c>&lt;fieldset&gt;</c> element's rendered legend.
/// </summary>
/// <remarks>
/// <para>
/// A fieldset's first <c>&lt;legend&gt;</c> child is not laid out in the fieldset's content. It is
/// the <em>rendered legend</em>, and it belongs to the block-start border: its border box is centred
/// on that border. A legend thinner than the border sits inside it; a taller one sits at the top of
/// the fieldset's border box, which grows to hold it, and the border is drawn through its middle.
/// The fieldset's content begins below whichever of the border and the legend reaches further down.
/// That is Chromium's geometry: an 18px-tall legend on the default 2px border starts where the
/// fieldset's border box does, and the content 18px plus the padding below it.
/// </para>
/// <para>
/// WPT's <c>css-break/fieldset-001</c> pins the same picture by construction — its reference states
/// the layout with a <c>&lt;p&gt;</c>, a <c>margin-top</c> that makes room for the part of the
/// legend standing above its border, and an absolutely positioned legend at a negative <c>top</c>:
/// a 49px legend on a 6px border begins <c>49/2 − 6/2 = 21.5</c> above the border's top edge, and the
/// content that follows starts at the legend's bottom plus the fieldset's own padding. Drawing the
/// border there, and stopping it behind the legend, is paint's part; the legend is the fieldset's
/// first child in the fragment tree, which is all paint needs to find it.
/// </para>
/// <para>
/// The legend's inline size is the other half of the rule and is resolved earlier, in
/// <c>ResolveBlockUsedWidth</c>: an <c>auto</c> inline size is the <em>fit-content</em> inline
/// size, so the legend shrink-wraps rather than stretching to the fieldset's content width the way
/// an ordinary block child would. <see cref="IsRenderedLegend"/> is what that branch asks.
/// </para>
/// <para>
/// Applied after the children are laid out, because the legend's margin box is only measured then —
/// the same shape as every other post-layout placement here. What is <em>not</em> done is the
/// block-size rule that goes with a non-<c>auto</c> <c>block-size</c> (subtract the part of the
/// legend's margin box that spills past the border), which is what WPT's <c>fieldset-block-size</c>
/// asks for.
/// </para>
/// </remarks>
internal partial class CssBox
{
    /// <summary>Moves the rendered legend onto the block-start border and the rest below it.</summary>
    private void ApplyFieldsetLegendPlacement()
    {
        if (!IsFieldset || RenderedLegend() is not { } legend)
            return;

        double border = ActualBorderTopWidth;
        double legendBorderBox = legend.ActualBottom - legend.Location.Y;
        double legendBox = legend.ActualMarginTop + legendBorderBox + legend.ActualMarginBottom;

        if (legendBox <= 0)
            return;

        // Laid out in the flow, the legend came first in the content: the rest follows its margin box.
        double marginBoxBottom = legend.Location.Y - legend.ActualMarginTop + legendBox;

        // Centred in a border thicker than it; otherwise at the top of the border box, its margin
        // included, with the border drawn through its middle.
        double legendTop = legendBorderBox < border
            ? Location.Y + (border - legendBorderBox) / 2
            : Location.Y + legend.ActualMarginTop;
        double moveLegend = legendTop - legend.Location.Y;

        if (Math.Abs(moveLegend) > 0.01)
            legend.OffsetTop(moveLegend);

        // The content starts below whichever of the border and the legend reaches further down.
        double legendBottom = legendTop + legendBorderBox + legend.ActualMarginBottom;
        double contentTop = Math.Max(Location.Y + border, legendBottom) + ActualPaddingTop;
        double moveRest = contentTop - marginBoxBottom;

        if (Math.Abs(moveRest) <= 0.01)
            return;

        foreach (var child in Boxes)
        {
            if (ReferenceEquals(child, legend)
                || child.Display == CssConstants.None
                || child.Position is CssConstants.Absolute or CssConstants.Fixed)
            {
                continue;
            }

            child.OffsetTop(moveRest);
        }

        // The children moved, so the auto height measured from them is stale by the same amount.
        // MarginBottomCollapse cannot be re-run to find out — it only ever grows ActualBottom
        // (`Math.Max(ActualBottom, …)`), and the move that matters here is upwards: the legend had
        // been laid out in the flow, so the fieldset was left as tall as if the border carried the
        // content *and* a legend-sized block below it. WPT's `legend-block-position-centering`
        // rendered a 100px-bordered fieldset 315px tall where every engine draws 218.
        //
        // Every in-flow child shifted by exactly `moveRest`, and the legend itself never reaches
        // below `contentTop` (the content begins at the legend's margin-box bottom whenever the
        // legend is the taller). So the measured bottom shifts with the children, floored at an
        // empty content box.
        if (Height == CssConstants.Auto || string.IsNullOrEmpty(Height))
        {
            ActualBottom = Math.Max(
                contentTop + ActualPaddingBottom + ActualBorderBottomWidth,
                ActualBottom + moveRest);
        }
    }

    private bool IsFieldset =>
        string.Equals(HtmlTag?.Name, "fieldset", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Whether this box is a <c>&lt;button&gt;</c>, <c>&lt;input&gt;</c>, <c>&lt;select&gt;</c> or
    /// <c>&lt;textarea&gt;</c>, which keeps its fit-content inline size when it is laid out as a
    /// block, as a rendered legend does; see <c>ResolveBlockUsedWidth</c>.
    /// </summary>
    internal bool IsFormControl =>
        HtmlTag?.Name is { } name
        && (name.Equals("button", StringComparison.OrdinalIgnoreCase)
            || name.Equals("input", StringComparison.OrdinalIgnoreCase)
            || name.Equals("select", StringComparison.OrdinalIgnoreCase)
            || name.Equals("textarea", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Whether this box is the rendered legend of the fieldset it is a child of.
    /// </summary>
    /// <remarks>
    /// HTML §15.5.13 gives the rendered legend a fit-content inline size when its own
    /// <c>inline-size</c> is <c>auto</c> — it is blockified, but it is not stretched to the
    /// fieldset's content width the way an ordinary block child would be. Layout asks this before
    /// resolving the used width; see <c>ResolveBlockUsedWidth</c>.
    /// </remarks>
    internal bool IsRenderedLegend =>
        string.Equals(HtmlTag?.Name, "legend", StringComparison.OrdinalIgnoreCase)
        && ParentBox is { } parent
        && parent.IsFieldset
        && ReferenceEquals(parent.RenderedLegend(), this);

    /// <summary>
    /// The fieldset's rendered legend: its first in-flow <c>&lt;legend&gt;</c> child. A second one
    /// is an ordinary block and stays in the content, which is what HTML §15.5.13 says by naming
    /// only the first.
    /// </summary>
    private CssBox RenderedLegend()
    {
        foreach (var child in Boxes)
        {
            if (child.Display == CssConstants.None
                || child.Position is CssConstants.Absolute or CssConstants.Fixed
                || child.Float != CssConstants.None)
            {
                continue;
            }

            // Block-level, because that is what a rendered legend is (HTML §15.5.13). An engine
            // whose user-agent sheet leaves `legend` at the CSS initial `inline` has no rendered
            // legend to place, and this stays out of its way rather than moving an inline box onto
            // a border.
            return string.Equals(child.HtmlTag?.Name, "legend", StringComparison.OrdinalIgnoreCase)
                && !IsInlineLevelDisplay(child.Display)
                ? child
                : null;
        }

        return null;
    }

    private static bool IsInlineLevelDisplay(string display)
    {
        var value = display?.Trim();
        return string.IsNullOrEmpty(value)
            || value.StartsWith(CssConstants.Inline, StringComparison.OrdinalIgnoreCase);
    }
}
