namespace SnapLingoWindows.Models;

public sealed record ProviderModelOption(
    string Id,
    string Label,
    DateTimeOffset? CreatedAt = null
);

public sealed record ProviderModelCatalog(
    IReadOnlyList<ProviderModelOption> Models,
    string DefaultModelId
);
