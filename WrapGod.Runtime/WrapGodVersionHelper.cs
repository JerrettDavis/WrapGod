using System;
using System.Collections.Concurrent;

namespace WrapGod.Runtime;

/// <summary>
/// Provides runtime version-availability checks for adaptive compatibility mode.
/// Generated facades call <see cref="IsMemberAvailable"/> to guard version-specific
/// members before forwarding to the inner (real) implementation.
/// </summary>
public static class WrapGodVersionHelper
{
    private static readonly ConcurrentDictionary<(string IntroducedIn, string? RemovedIn), bool> Cache = new();

    private static Version? _currentVersion;

    /// <summary>
    /// Gets or sets the version of the wrapped library that is loaded at runtime.
    /// Must be set before the first availability check; typically during application startup.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when reading <see cref="CurrentVersion"/> before it has been set.
    /// </exception>
    public static Version CurrentVersion
    {
        get => _currentVersion ?? throw new InvalidOperationException(
            "WrapGodVersionHelper.CurrentVersion has not been configured. " +
            "Set it during application startup before using adaptive facades.");
        set
        {
            _currentVersion = value ?? throw new ArgumentNullException(nameof(value));
            // Clear the cache when the version changes so results are recalculated.
            Cache.Clear();
        }
    }

    /// <summary>
    /// Determines whether a member is available in the current runtime version.
    /// </summary>
    /// <param name="introducedIn">
    /// The version label when the member was first introduced (e.g. "2.0.0").
    /// </param>
    /// <param name="removedIn">
    /// The version label when the member was removed, or <c>null</c> if it is
    /// still present in the latest version.
    /// </param>
    /// <returns>
    /// <c>true</c> if the member is available in <see cref="CurrentVersion"/>;
    /// <c>false</c> otherwise.
    /// </returns>
    public static bool IsMemberAvailable(string introducedIn, string? removedIn)
    {
        return Cache.GetOrAdd((introducedIn, removedIn), static key => Evaluate(key.IntroducedIn, key.RemovedIn));
    }

    /// <summary>
    /// Resets the helper to its initial state. Useful for testing.
    /// </summary>
    public static void Reset()
    {
        _currentVersion = null;
        Cache.Clear();
    }

    private static bool Evaluate(string introducedIn, string? removedIn)
    {
        var current = CurrentVersion;
        var introduced = ParseVersion(introducedIn);

        if (current < introduced)
            return false;

        if (removedIn != null)
        {
            var removed = ParseVersion(removedIn);
            if (current >= removed)
                return false;
        }

        return true;
    }

    private static Version ParseVersion(string version)
    {
        // System.Version requires at least major.minor; pad single-component versions.
        return Version.Parse(version.Contains('.') ? version : version + ".0");
    }
}
