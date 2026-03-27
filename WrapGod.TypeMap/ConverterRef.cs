namespace WrapGod.TypeMap;

/// <summary>
/// Reference to a user-defined converter method or class that handles
/// custom type conversion during code generation.
/// </summary>
public sealed class ConverterRef
{
    /// <summary>
    /// Fully-qualified name of the converter type (e.g. "MyApp.Converters.DateConverter").
    /// </summary>
    public string TypeName { get; set; } = string.Empty;

    /// <summary>
    /// Optional method name on the converter type. When null, the generator
    /// will look for a conventional <c>Convert</c> method.
    /// </summary>
    public string? MethodName { get; set; }
}
