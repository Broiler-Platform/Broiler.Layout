using Broiler.CSS;

namespace Broiler.Layout.Engine;

/// <summary>
/// One border in the collapsing border model (CSS 2.1 §17.6.2): its style, its width in pixels and
/// its colour.
/// </summary>
internal readonly record struct CollapsedBorder(string Style, double Width, string Color)
{
    /// <summary>No border: what a <c>none</c> or <c>hidden</c> border collapses to.</summary>
    internal static CollapsedBorder None { get; } = new(CssConstants.None, 0, string.Empty);
}

/// <summary>
/// The borders the collapsing border model gives a table or a table cell in place of its own, one
/// for each side.
/// </summary>
internal sealed record CollapsedBorderSides(
    CollapsedBorder Top,
    CollapsedBorder Right,
    CollapsedBorder Bottom,
    CollapsedBorder Left);
