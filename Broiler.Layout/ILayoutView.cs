using System;
using System.Collections.Generic;
using System.Drawing;
using Broiler.Dom;

namespace Broiler.Layout;

/// <summary>
/// A headless, document-scoped view over the real layout tree that answers per-element
/// box-geometry queries without the caller taking a dependency on a graphics backend or
/// the HTML renderer. Implementations lay the bound document out at the requested viewport
/// and return a per-element <see cref="BoxGeometry"/> map keyed by the canonical
/// <see cref="DomElement"/>.
/// </summary>
/// <remarks>
/// <para>
/// This is the narrow contract that lets the script bridge (Broiler.HtmlBridge.Dom) read
/// accurate element geometry while depending only on the canonical layout read-model, not
/// on Broiler.HTML.Image. Implementations own renderer resources, so callers dispose the
/// view when the owning document is torn down.
/// </para>
/// <para>
/// <b>This component ships no implementation of this interface, and none exists in any
/// Broiler component today.</b> The contract is one a consumer implements over the box
/// tree and injects; a consumer that injects nothing gets whatever null view it falls back
/// to, and therefore no engine geometry at all. Earlier revisions of these remarks named
/// <c>Broiler.HTML.Headless.HeadlessLayoutView</c> as the implementation. That project was
/// deleted from Broiler.HTML on 2026-09-15 because it could not build after the move to
/// packages, so the reference was dangling; whether this component should grow a default
/// implementation is an open question and not answered here.
/// </para>
/// </remarks>
public interface ILayoutView : IDisposable
{
    /// <summary>
    /// Returns the per-element geometry map for <paramref name="document"/> at its current
    /// <see cref="DomDocument.Version"/> and the given <paramref name="viewport"/>. The
    /// implementation caches the map per <c>(document, version, viewport, baseUrl)</c> and
    /// re-lays out only when one of those changes.
    /// </summary>
    /// <param name="document">The canonical document to lay out.</param>
    /// <param name="viewport">The viewport size to lay out against.</param>
    /// <param name="baseUrl">The document base URL used for resource resolution.</param>
    /// <param name="contentDocumentResolver">
    /// Optional host callback (HtmlBridge Phase 4 P4.4b) mapping a nested-browsing-context
    /// container element (<c>&lt;iframe&gt;</c>/<c>&lt;object&gt;</c>/<c>&lt;frame&gt;</c>) to its
    /// referenced content <see cref="DomDocument"/>. When supplied, a sub-document that is no
    /// longer an in-tree child is projected into the box tree (and its geometry composed into the
    /// main frame). Null preserves the legacy in-tree-materialisation behaviour.
    /// </param>
    IReadOnlyDictionary<DomElement, BoxGeometry> GetGeometry(
        DomDocument document, SizeF viewport, string baseUrl,
        Func<DomElement, DomDocument?>? contentDocumentResolver = null);
}
