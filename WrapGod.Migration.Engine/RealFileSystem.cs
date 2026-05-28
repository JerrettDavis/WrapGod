namespace WrapGod.Migration.Engine;

/// <summary>
/// Default <see cref="IMigrationFileSystem"/> implementation backed by <see cref="System.IO.File"/>.
/// Atomic writes use a <c>.tmp</c> suffix followed by <see cref="File.Move"/>.
/// </summary>
internal sealed class RealFileSystem : IMigrationFileSystem
{
    /// <inheritdoc/>
    public bool FileExists(string path) => File.Exists(path);

    /// <inheritdoc/>
    public string ReadAllText(string path) => File.ReadAllText(path);

    /// <inheritdoc/>
    public void WriteAllTextAtomic(string path, string content)
    {
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, content);
        File.Move(tmp, path, overwrite: true);
    }
}
