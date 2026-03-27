using System.Text;

namespace WrapGod.TypeMap.Generation;

/// <summary>
/// StringBuilder-based helper for emitting well-formatted C# source code.
/// Tracks indentation level and provides convenience methods for common
/// source constructs (blocks, lines, blank lines).
/// </summary>
public sealed class MapperSourceBuilder
{
    private readonly StringBuilder _sb = new StringBuilder();
    private int _indent;

    /// <summary>Current indentation depth (number of 4-space levels).</summary>
    public int IndentLevel => _indent;

    /// <summary>Increase indentation by one level.</summary>
    public MapperSourceBuilder Indent()
    {
        _indent++;
        return this;
    }

    /// <summary>Decrease indentation by one level.</summary>
    public MapperSourceBuilder Outdent()
    {
        if (_indent > 0) _indent--;
        return this;
    }

    /// <summary>Append a line at the current indentation level.</summary>
    public MapperSourceBuilder AppendLine(string line)
    {
        _sb.Append(new string(' ', _indent * 4));
        _sb.AppendLine(line);
        return this;
    }

    /// <summary>Append a blank line.</summary>
    public MapperSourceBuilder BlankLine()
    {
        _sb.AppendLine();
        return this;
    }

    /// <summary>Open a brace block: writes "{" and increases indent.</summary>
    public MapperSourceBuilder OpenBrace()
    {
        AppendLine("{");
        _indent++;
        return this;
    }

    /// <summary>Close a brace block: decreases indent and writes "}".</summary>
    public MapperSourceBuilder CloseBrace()
    {
        if (_indent > 0) _indent--;
        AppendLine("}");
        return this;
    }

    /// <summary>Returns the accumulated source text.</summary>
    public override string ToString() => _sb.ToString();
}
