namespace EntryHub.Api.Documentation;

/// <summary>
/// The look of the Scalar page: logo, favicon and brand colors. The static docs of the release
/// (<c>docs/index.html</c>) repeat the same header and CSS, so keep both in step.
/// </summary>
internal static class ScalarBranding
{
    /// <summary>
    /// The same drawing as <c>docs/logo.svg</c>: a calendar with a door, a room you book.
    /// </summary>
    private const string Logo =
        """<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64" width="28" height="28" aria-hidden="true">"""
        + """<rect width="64" height="64" rx="14" fill="#0f766e"/>"""
        + """<rect x="14" y="14" width="36" height="38" rx="5" fill="#fff"/>"""
        + """<rect x="14" y="14" width="36" height="9" rx="5" fill="#99f6e4"/>"""
        + """<rect x="14" y="19" width="36" height="4" fill="#99f6e4"/>"""
        + """<rect x="21" y="9" width="4" height="10" rx="2" fill="#fff"/>"""
        + """<rect x="39" y="9" width="4" height="10" rx="2" fill="#fff"/>"""
        + """<path d="M25 52V30h14v22" fill="none" stroke="#0f766e" stroke-width="3" stroke-linejoin="round"/>"""
        + """<circle cx="35" cy="42" r="1.8" fill="#0f766e"/></svg>""";

    /// <summary>
    /// The logo as a data URI, so the favicon needs no static files in the API.
    /// </summary>
    public static readonly string Favicon = "data:image/svg+xml," + Uri.EscapeDataString(Logo);

    /// <summary>
    /// A bar above the reference with the logo and the name of the API.
    /// </summary>
    public const string Header =
        """<header class="entryhub-brand">""" + Logo + """<span>EntryHub API</span></header>""";

    /// <summary>
    /// Scalar reads its colors from CSS variables, one set for light mode and one for dark mode.
    /// </summary>
    public const string Css =
        """
        .light-mode {
          --scalar-color-accent: #0f766e;
          --scalar-background-accent: #0f766e1f;
        }
        .dark-mode {
          --scalar-color-accent: #2dd4bf;
          --scalar-background-accent: #2dd4bf1f;
        }
        .entryhub-brand {
          display: flex;
          align-items: center;
          gap: 10px;
          padding: 10px 16px;
          font: 600 16px/1 var(--scalar-font, sans-serif);
          color: #fff;
          background: #134e4a;
        }
        """;
}