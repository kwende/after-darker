using AfterDarker.Core.Ne;

namespace AfterDarker.Core.Win16;

/// <summary>Associates one loaded guest instance with its file's resource directory.</summary>
/// <param name="Instance">Guest DLL instance identity, not a native module handle.</param>
/// <param name="Catalog">Owned resource bytes resolved from parsed NE metadata.</param>
public sealed record Win16ModuleResources(ushort Instance, NeResourceCatalog Catalog);
