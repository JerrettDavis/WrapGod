namespace WrapGod.Migration.Engine;

/// <summary>
/// Abstracts file I/O for the migration engine, enabling in-memory substitution in tests.
/// </summary>
internal interface IMigrationFileSystem
{
    /// <summary>Returns <see langword="true"/> when the file at <paramref name="path"/> exists.</summary>
    bool FileExists(string path);

    /// <summary>Reads the entire contents of the file at <paramref name="path"/>.</summary>
    /// <exception cref="IOException">Thrown when the file cannot be read.</exception>
    string ReadAllText(string path);

    /// <summary>
    /// Writes <paramref name="content"/> to <paramref name="path"/> atomically
    /// (write to a temp file, then rename).
    /// </summary>
    /// <exception cref="IOException">Thrown when the file cannot be written.</exception>
    void WriteAllTextAtomic(string path, string content);
}
