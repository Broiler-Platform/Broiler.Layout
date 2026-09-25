using Broiler.Layout.Diagnostics;
using Broiler.Layout.Engine;

namespace Broiler.Layout.Tests;

/// <summary>
/// <see cref="LayoutDiagnostics"/> names what the layout engine was handed and did not apply as
/// written: the properties it does not model and the fallbacks it takes.
/// </summary>
/// <remarks>The hooks are process-wide, so these tests do not run beside each other.</remarks>
[Collection(nameof(LayoutDiagnosticsTests))]
[CollectionDefinition(nameof(LayoutDiagnosticsTests), DisableParallelization = true)]
public sealed class LayoutDiagnosticsTests
{
    private static readonly Uri BaseUrl = new("https://example.test/");

    [Fact(Timeout = 600000)]
    public void A_Property_The_Box_Does_Not_Model_Is_Reported()
    {
        var reported = Record(static () =>
        {
            var box = new CssBox(null, null, BaseUrl);
            CssUtils.SetPropertyValue(box, "frobnicate-marker", "1px");
            CssUtils.SetPropertyValue(box, "color", "red");
            CssUtils.SetPropertyValue(box, "--custom-marker", "1");
        }, static handler => LayoutDiagnostics.PropertyNotModeled = handler, static () => LayoutDiagnostics.PropertyNotModeled = null);

        Assert.Equal([("frobnicate-marker", "1px")], reported);
    }

    [Fact(Timeout = 600000)]
    public void An_Unsupported_Timing_Function_Is_Reported_As_Sampled_Linearly()
    {
        double sampled = 0;
        var reported = Record(() =>
        {
            sampled = CssAnimationResolver.ApplyTimingFunction(0.25, "steps(4, end)");
            CssAnimationResolver.ApplyTimingFunction(0.25, "ease-in");
        }, static handler => LayoutDiagnostics.FallbackTaken = handler, static () => LayoutDiagnostics.FallbackTaken = null);

        Assert.Equal(0.25, sampled);
        var (feature, detail) = Assert.Single(reported);
        Assert.Equal("animation-timing-function", feature);
        Assert.Contains("steps(4, end)", detail, StringComparison.Ordinal);
    }

    [Fact(Timeout = 600000)]
    public void Only_A_Grid_That_Declares_A_Template_Is_Reported_As_Approximated()
    {
        Assert.Null(CssBox.DescribeApproximatedTemplates(null, "none"));
        Assert.Null(CssBox.DescribeApproximatedTemplates("  ", null));

        var described = CssBox.DescribeApproximatedTemplates("repeat(auto-fit, minmax(10rem, 1fr))", "none");
        Assert.NotNull(described);
        Assert.Contains("repeat(auto-fit, minmax(10rem, 1fr))", described, StringComparison.Ordinal);
    }

    [Fact(Timeout = 600000)]
    public void A_Throwing_Handler_Does_Not_Break_The_Layout()
    {
        LayoutDiagnostics.PropertyNotModeled = static (_, _) => throw new InvalidOperationException("handler-marker");
        try
        {
            var box = new CssBox(null, null, BaseUrl);
            CssUtils.SetPropertyValue(box, "frobnicate-marker", "1px");
            CssUtils.SetPropertyValue(box, "color", "red");
        }
        finally
        {
            LayoutDiagnostics.PropertyNotModeled = null;
        }
    }

    private static List<(string, string)> Record(Action work, Action<Action<string, string>> subscribe, Action unsubscribe)
    {
        var reported = new List<(string, string)>();
        subscribe((first, second) =>
        {
            lock (reported)
                reported.Add((first, second));
        });
        try
        {
            work();
        }
        finally
        {
            unsubscribe();
        }

        return reported;
    }
}
