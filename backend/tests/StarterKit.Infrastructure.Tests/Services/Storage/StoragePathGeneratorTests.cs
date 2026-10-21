using StarterKit.Infrastructure.Services.Storage;

namespace StarterKit.Infrastructure.Tests.Services.Storage;

public class StoragePathGeneratorTests
{
    [Fact]
    public void Generate_ContainsDateSegments_AndForwardSlashes()
    {
        StoragePathGenerator generator = new();
        DateTime now = DateTime.UtcNow;

        string path = generator.Generate("avatar.png");

        Assert.Contains($"{now:yyyy}/{now:MM}/{now:dd}/", path);
        Assert.DoesNotContain('\\', path);
    }

    [Fact]
    public void Generate_PreservesFileExtension()
    {
        StoragePathGenerator generator = new();

        string path = generator.Generate("report.pdf");

        Assert.EndsWith(".pdf", path);
    }

    [Fact]
    public void Generate_TwoCallsForSameFileName_ProduceDifferentPaths()
    {
        StoragePathGenerator generator = new();

        string first = generator.Generate("avatar.png");
        string second = generator.Generate("avatar.png");

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Generate_WithFolder_PrefixesThePath()
    {
        StoragePathGenerator generator = new();

        string path = generator.Generate("a.png", "public/avatars");

        Assert.StartsWith("public/avatars/", path);
        Assert.EndsWith(".png", path);
    }

    [Theory]
    [InlineData("../secrets")]
    [InlineData("/abs")]
    [InlineData("a//b")]
    public void Generate_FolderEscapingTheRoot_Throws(string folder)
    {
        StoragePathGenerator generator = new();

        Assert.Throws<ArgumentException>(() => generator.Generate("a.png", folder));
    }
}
