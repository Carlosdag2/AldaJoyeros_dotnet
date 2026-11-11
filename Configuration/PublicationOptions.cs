namespace AldaJoyeros.Configuration;

// Enable indexing only after configuring the final, public HTTPS address.
public sealed class PublicationOptions
{
    public bool IndexingEnabled { get; set; }
    public bool RedirectToPublicOrigin { get; set; }
    public string PublicBaseUrl { get; set; } = "";
    public const string DefaultLegalOwner = "Patricia García Guzmán";
    public string LegalOwner { get; set; } = DefaultLegalOwner;
    public string EffectiveLegalOwner => string.IsNullOrWhiteSpace(LegalOwner) ? DefaultLegalOwner : LegalOwner.Trim();
    public string RegistryDetails { get; set; } = "";
    public string? PublicOrigin => Uri.TryCreate(PublicBaseUrl, UriKind.Absolute, out var uri)
        && uri.Scheme == "https" && !uri.IsLoopback && string.IsNullOrEmpty(uri.Query)
        && string.IsNullOrEmpty(uri.Fragment) && string.IsNullOrEmpty(uri.UserInfo)
        ? uri.AbsoluteUri.TrimEnd('/') : null;
    public bool CanIndex => IndexingEnabled && PublicOrigin != null;
}
