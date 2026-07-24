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
}
