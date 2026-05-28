namespace WrapGod.Migration.Generation;

/// <summary>
/// String similarity algorithms used by rename detection.
/// </summary>
internal static class Similarity
{
    /// <summary>
    /// Computes the Jaro-Winkler similarity between two strings (case-insensitive).
    /// Returns a value in [0.0, 1.0] where 1.0 is an exact match.
    /// </summary>
    public static double JaroWinkler(string a, string b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        // Normalise to lower-case for case-insensitive comparison
        a = a.ToLowerInvariant();
        b = b.ToLowerInvariant();

        if (a == b) return 1.0;
        if (a.Length == 0 || b.Length == 0) return 0.0;

        double jaro = Jaro(a, b);

        // Count common prefix (up to 4 characters)
        int prefixLen = 0;
        int maxPrefix = Math.Min(4, Math.Min(a.Length, b.Length));
        for (int i = 0; i < maxPrefix; i++)
        {
            if (a[i] == b[i])
                prefixLen++;
            else
                break;
        }

        const double p = 0.1; // Winkler scaling factor
        return jaro + prefixLen * p * (1.0 - jaro);
    }

    private static double Jaro(string a, string b)
    {
        int len1 = a.Length;
        int len2 = b.Length;

        int matchWindow = Math.Max(len1, len2) / 2 - 1;
        if (matchWindow < 0) matchWindow = 0;

        bool[] matched1 = new bool[len1];
        bool[] matched2 = new bool[len2];

        int matches = 0;
        for (int i = 0; i < len1; i++)
        {
            int lo = Math.Max(0, i - matchWindow);
            int hi = Math.Min(i + matchWindow + 1, len2);
            for (int j = lo; j < hi; j++)
            {
                if (!matched2[j] && a[i] == b[j])
                {
                    matched1[i] = true;
                    matched2[j] = true;
                    matches++;
                    break;
                }
            }
        }

        if (matches == 0) return 0.0;

        // Count transpositions
        int transpositions = 0;
        int k = 0;
        for (int i = 0; i < len1; i++)
        {
            if (!matched1[i]) continue;
            while (!matched2[k]) k++;
            if (a[i] != b[k]) transpositions++;
            k++;
        }

        return (matches / (double)len1 +
                matches / (double)len2 +
                (matches - transpositions / 2.0) / matches) / 3.0;
    }

    /// <summary>
    /// Extracts the short (unqualified) name from a fully-qualified type or member name.
    /// </summary>
    public static string ShortName(string fullName)
    {
        if (string.IsNullOrEmpty(fullName)) return fullName ?? string.Empty;
        int dot = fullName.LastIndexOf('.');
        return dot >= 0 ? fullName.Substring(dot + 1) : fullName;
    }

    /// <summary>
    /// Extracts the namespace from a fully-qualified type name (everything before the last dot).
    /// </summary>
    public static string Namespace(string fullName)
    {
        if (string.IsNullOrEmpty(fullName)) return string.Empty;
        int dot = fullName.LastIndexOf('.');
        return dot >= 0 ? fullName.Substring(0, dot) : string.Empty;
    }
}
