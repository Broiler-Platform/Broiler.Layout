using System;

namespace Broiler.Layout.Diagnostics;

/// <summary>
/// Opt-in reports of the CSS the layout engine was handed and did not apply as written: properties
/// it does not model, and syntax it meets and replaces with a fallback.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why it exists.</b> The cascade hands every declared longhand to
/// <see cref="Engine.CssUtils.SetPropertyValue"/>, whose switch ignores the names it does not model,
/// and a handful of places in the engine meet a value they cannot lay out and quietly use a simpler
/// one. Neither leaves any trace: the page renders, just not as written, and nothing says which
/// declaration was the reason. A host diagnosing a page — the browser's <c>--analyze</c> — subscribes
/// here and gets the list.
/// </para>
/// <para>
/// <b>Off by default, and free when off.</b> Each report site is one read of a static delegate. It is
/// process-wide, like <c>Broiler.CSS.Dom.CssEngineDiagnostics</c>, which reports the declarations the
/// style engine rejects before they get here; handlers are called from whichever thread lays out, so
/// they must be thread-safe.
/// </para>
/// <para>
/// <b>A handler cannot break a layout.</b> What a handler throws is swallowed: diagnostics observe a
/// layout, they never change its outcome.
/// </para>
/// </remarks>
public static class LayoutDiagnostics
{
    /// <summary>
    /// Invoked with <c>(property, value)</c> for each cascaded declaration whose property the layout
    /// engine does not model, and so ignores. Custom properties are not reported. Called once per box
    /// the declaration applies to; a consumer aggregates.
    /// </summary>
    public static Action<string, string>? PropertyNotModeled { get; set; }

    /// <summary>
    /// Invoked with <c>(feature, detail)</c> when the layout engine replaces what the CSS asked for
    /// with a fallback: a timing function it does not implement, a grid whose declared templates the
    /// track-sizing pass declined and the approximation laid out instead.
    /// </summary>
    public static Action<string, string>? FallbackTaken { get; set; }

    internal static void ReportPropertyNotModeled(string property, string value) =>
        Invoke(PropertyNotModeled, property, value);

    internal static void ReportFallback(string feature, string detail) =>
        Invoke(FallbackTaken, feature, detail);

    private static void Invoke(Action<string, string>? handler, string first, string second)
    {
        if (handler is null)
            return;

        try
        {
            handler(first, second);
        }
        catch (Exception)
        {
            // See the type remarks: a diagnostics handler never changes a layout.
        }
    }
}
