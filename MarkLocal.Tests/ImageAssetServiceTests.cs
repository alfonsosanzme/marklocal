using System.IO;
using MarkLocal.Core;
using Xunit;

namespace MarkLocal.Tests;

public class ImageAssetServiceTests
{
    [Fact]
    public void BuildRelativePath_ProducesForwardSlashedRelativePath()
    {
        var settings = TestFactory.CreateIsolatedSettings();
        var svc = new ImageAssetService(settings);

        string docDir = TestFactory.CreateTempDir();
        string assetsDir = Path.Combine(docDir, "assets");
        Directory.CreateDirectory(assetsDir);
        string img = Path.Combine(assetsDir, "foto.png");
        File.WriteAllBytes(img, new byte[] { 1, 2 });

        string rel = svc.BuildRelativePath(docDir, img);
        Assert.Equal("assets/foto.png", rel);
    }

    [Fact]
    public void BuildMarkdownImageReference_EscapesSpacesAndUsesAlt()
    {
        var settings = TestFactory.CreateIsolatedSettings();
        var svc = new ImageAssetService(settings);

        string md = svc.BuildMarkdownImageReference("Una imagen", "assets/un archivo.png");
        Assert.Equal("![Una imagen](assets/un%20archivo.png)", md);
    }

    [Fact]
    public void BuildMarkdownImageReference_FallsBackToFilenameWhenAltEmpty()
    {
        var settings = TestFactory.CreateIsolatedSettings();
        var svc = new ImageAssetService(settings);

        string md = svc.BuildMarkdownImageReference(string.Empty, "assets/foto-de-prueba.png");
        Assert.Equal("![foto-de-prueba](assets/foto-de-prueba.png)", md);
    }

    [Theory]
    [InlineData("a.png", true)]
    [InlineData("a.PNG", true)]
    [InlineData("a.jpeg", true)]
    [InlineData("a.svg", true)]
    [InlineData("a.webp", true)]
    [InlineData("foo.md", false)]
    [InlineData("foo", false)]
    [InlineData("foo.zip", false)]
    public void IsImageFile_DetectsByExtension(string path, bool expected)
    {
        Assert.Equal(expected, ImageAssetService.IsImageFile(path));
    }

    [Fact]
    public void CopyImageToAssets_CopiesIntoAssetsFolderAndAvoidsCollisions()
    {
        var settings = TestFactory.CreateIsolatedSettings();
        settings.Settings.AssetsFolderName = "assets";
        var svc = new ImageAssetService(settings);

        string docDir = TestFactory.CreateTempDir();
        string source1 = Path.Combine(TestFactory.CreateTempDir(), "foto.png");
        string source2 = Path.Combine(TestFactory.CreateTempDir(), "foto.png");
        File.WriteAllBytes(source1, new byte[] { 1 });
        File.WriteAllBytes(source2, new byte[] { 2 });

        string copy1 = svc.CopyImageToAssets(docDir, source1);
        string copy2 = svc.CopyImageToAssets(docDir, source2);

        Assert.Equal(Path.Combine(docDir, "assets", "foto.png"), copy1);
        Assert.Equal(Path.Combine(docDir, "assets", "foto-1.png"), copy2);
        Assert.True(File.Exists(copy1));
        Assert.True(File.Exists(copy2));
        Assert.Equal(new byte[] { 1 }, File.ReadAllBytes(copy1));
        Assert.Equal(new byte[] { 2 }, File.ReadAllBytes(copy2));
    }

    [Fact]
    public void CopyImageToAssets_ThrowsWhenDocumentDirectoryIsEmpty()
    {
        var settings = TestFactory.CreateIsolatedSettings();
        var svc = new ImageAssetService(settings);
        Assert.ThrowsAny<System.Exception>(() => svc.CopyImageToAssets(string.Empty, "C:/non/existing.png"));
    }
}
