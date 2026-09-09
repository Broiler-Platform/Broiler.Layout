using System.IO;
using System.Linq;
using System.Reflection;

namespace Broiler.Layout.Tests;

/// <summary>
/// Proves that a build of this repository used this repository's own component
/// checkouts, and not some consumer's.
/// </summary>
/// <remarks>
/// <para>
/// The component roots in <c>Directory.Build.props</c> are deliberately
/// overridable. Broiler.Browser vendors this component alongside its own copies of
/// Broiler.CSS, Broiler.DOM and Broiler.Graphics and sets the roots so that one
/// assembly of each name is built instead of two, and that is correct: the
/// override is a feature. Without it a build produces two Broiler.Dom assemblies
/// from two checkouts, and whichever one wins the copy into the output directory
/// decides at run time whether anything loads.
/// </para>
/// <para>
/// What the override also does is silently change what a local build proves.
/// Building this repository from inside such a consumer's tree imports the
/// consumer's <c>Directory.Build.props</c>, compiles against <em>their</em>
/// components rather than this repository's pinned submodules, and goes green
/// while this repository's own CI, which clones it alone, is compiling something
/// else entirely and may be failing.
/// </para>
/// <para>
/// That is not hypothetical here. This component only became a repository at all
/// because Broiler.HTML could not build without it: HTML referenced it as a
/// sibling directory that existed nowhere but inside the aggregate, so HTML's own
/// CI had been failing to compile since before anyone noticed, while every local
/// build from inside the aggregate passed.
/// </para>
/// <para>
/// So the build records which checkout each root resolved to, and this reads it
/// back. A run from inside a consumer's tree now says so instead of being mistaken
/// for evidence about this repository.
/// </para>
/// </remarks>
public sealed class ComponentRootTests
{
    /// <summary>The file that marks this repository's own root.</summary>
    private const string RepositoryMarker = "Broiler.Layout.slnx";

    [Theory]
    [InlineData("BroilerCssRoot")]
    [InlineData("BroilerDomRoot")]
    [InlineData("BroilerGraphicsRoot")]
    public void The_Build_Used_This_Repositorys_Own_Checkout(string root)
    {
        string repository = RepositoryRoot();
        string resolved = ResolvedRoot(root);

        Assert.True(
            resolved.StartsWith(repository, StringComparison.OrdinalIgnoreCase),
            $"""
             {root} resolved to a checkout outside this repository, so this build did not
             compile against the submodule this repository pins and proves nothing about its CI.

                 {root} = {resolved}
                 repository = {repository}

             This happens when the repository is built from inside a consumer's tree: the
             consumer's Directory.Build.props is imported first and its roots win. To verify a
             change here, either build a standalone clone, or override every root back to this
             repository's own submodules:

                 dotnet test {RepositoryMarker} -p:{root}=<repository>/<submodule>
             """);
    }

    [Fact]
    public void Every_Root_Is_Recorded_So_None_Can_Drift_Unwatched()
    {
        // A root added to Directory.Build.props without a matching AssemblyMetadata
        // entry would be exactly as invisible as the problem this guards against,
        // and the guard above would pass while saying nothing about it.
        string properties = File.ReadAllText(Path.Combine(RepositoryRoot(), "Directory.Build.props"));

        var declared = System.Text.RegularExpressions.Regex
            .Matches(properties, @"<(Broiler\w+Root)>")
            .Select(match => match.Groups[1].Value)
            .Distinct()
            .ToList();

        Assert.NotEmpty(declared);
        foreach (string root in declared)
            Assert.NotNull(Metadata(root));
    }

    /// <summary>The value the build recorded for one root.</summary>
    private static string ResolvedRoot(string root)
    {
        string? value = Metadata(root);
        Assert.False(
            string.IsNullOrWhiteSpace(value),
            $"The build recorded no {root}. Directory.Build.props should carry an AssemblyMetadata entry for every component root.");

        return Path.GetFullPath(value!).TrimEnd(Path.DirectorySeparatorChar);
    }

    private static string? Metadata(string key) =>
        typeof(ComponentRootTests).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => string.Equals(attribute.Key, key, StringComparison.Ordinal))
            ?.Value;

    /// <summary>
    /// This repository's root, found by walking up from the test assembly to the
    /// nearest directory holding the solution. The nearest one is this
    /// repository's even when it sits inside a consumer's tree, which is the case
    /// that matters.
    /// </summary>
    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, RepositoryMarker)))
                return directory.FullName.TrimEnd(Path.DirectorySeparatorChar);

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"No {RepositoryMarker} above {AppContext.BaseDirectory}; this test cannot tell which repository it is in.");
    }
}
