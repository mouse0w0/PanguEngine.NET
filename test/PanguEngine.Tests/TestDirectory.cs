namespace PanguEngine.Tests;

internal sealed class TestDirectory : IDisposable
{
    private TestDirectory(string path)
    {
        Path = path;
    }

    internal string Path { get; }

    internal static TestDirectory Create()
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"pangu-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return new TestDirectory(path);
    }

    internal static void WriteResource(TestDirectory directory, string relativePath, string content)
    {
        var path = System.IO.Path.Combine(
            directory.Path,
            "assets",
            relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    internal static void WriteResource(TestDirectory directory, string relativePath, byte[] content)
    {
        var path = System.IO.Path.Combine(
            directory.Path,
            "assets",
            relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, content);
    }

    public void Dispose()
    {
        if (Directory.Exists(Path))
            Directory.Delete(Path, recursive: true);
    }
}