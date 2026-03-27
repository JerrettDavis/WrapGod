using Microsoft.CodeAnalysis;

namespace WrapGod.Analyzers;

/// <summary>
/// Diagnostic descriptors for WrapGod analyzers.
/// </summary>
internal static class DiagnosticDescriptors
{
    private const string Category = "WrapGod.Usage";

    /// <summary>
    /// WG2001: Direct usage of a third-party type that has a generated wrapper interface.
    /// </summary>
    public static readonly DiagnosticDescriptor DirectTypeUsage = new(
        id: "WG2001",
        title: "Direct third-party type usage",
        messageFormat: "Type '{0}' has a generated wrapper interface; use '{1}' instead",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description:
            "Direct usage of a wrapped third-party type bypasses the abstraction layer. " +
            "Depend on the generated wrapper interface to keep your code decoupled.");

    /// <summary>
    /// WG2002: Direct method call on a third-party type that has a generated facade.
    /// </summary>
    public static readonly DiagnosticDescriptor DirectMethodCall = new(
        id: "WG2002",
        title: "Direct third-party method call",
        messageFormat: "Method '{0}' on type '{1}' should be called through the facade '{2}'",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description:
            "Calling methods directly on a wrapped third-party type bypasses the facade layer. " +
            "Use the generated facade to keep your code decoupled.");
}
