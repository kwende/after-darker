using AfterDarker.Runtime;

namespace AfterDarker.Tests.Conformance;

[TestClass]
public sealed class AfterDarkLibraryTests
{
    [TestMethod]
    public void CatalogHasUniqueStableIdentitiesAndHashes()
    {
        var catalog = SupportedModules.Artifacts;
        Assert.IsTrue(catalog.Count > 0);
        Assert.AreEqual(catalog.Count, catalog.Select(item => item.Id).Distinct().Count());
        Assert.AreEqual(catalog.Count, catalog.Select(item => item.Sha256).Distinct().Count());
        foreach (var item in catalog) Assert.AreEqual(item, SupportedModules.FindByHash(item.Sha256.ToLowerInvariant()));
    }

    [TestMethod]
    public async Task MissingFolderLeavesNativeCollectionUsable()
    {
        var result = await AfterDarkLibrary.DiscoverAsync(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));
        Assert.AreEqual(0, result.Artifacts.Count);
        Assert.IsTrue(result.Diagnostics.Count > 0);
    }

    [TestMethod]
    public async Task FamiliarFilenameDoesNotGrantSupportAndReplacementIsRejected()
    {
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "SPHERES.AD");
        try
        {
            await File.WriteAllBytesAsync(path, [0x4D, 0x5A, 0]);
            var discovery = await AfterDarkLibrary.DiscoverAsync(directory);
            Assert.AreEqual(0, discovery.Artifacts.Count);
            Assert.IsTrue(discovery.Diagnostics.Single().Contains("Unsupported revision"));
            var expected = SupportedModules.FindByHash(SupportedModules.SpheresSha256)!;
            Assert.ThrowsExactly<InvalidDataException>(() => AfterDarkLibrary.ReadVerified(new(expected, path)));
        }
        finally { File.Delete(path); Directory.Delete(directory); }
    }
}
