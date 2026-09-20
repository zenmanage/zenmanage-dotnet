using System.Reflection;

namespace Zenmanage.Internal;

internal static class SdkInfo
{
    private const string FallbackVersion = "1.0.0";

    public const string ClientAgent = "zenmanage-dotnet";

    /// <summary>
    /// The SDK's release version, read from this assembly's informational version (which MSBuild
    /// derives from the &lt;Version&gt; set in Zenmanage.csproj) rather than a hand-maintained
    /// constant, so it can't drift from the version actually shipped on each release.
    /// </summary>
    public static readonly string Version = ResolveVersion();

    private static string ResolveVersion()
    {
        var assembly = typeof(SdkInfo).Assembly;
        var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        if (!string.IsNullOrEmpty(informationalVersion))
        {
            // Deterministic builds append a source revision suffix (e.g. "1.0.0+abcdef1234");
            // strip it so the reported version matches the package version exactly.
            var plusIndex = informationalVersion.IndexOf('+');
            return plusIndex >= 0 ? informationalVersion[..plusIndex] : informationalVersion;
        }

        return assembly.GetName().Version?.ToString(3) ?? FallbackVersion;
    }
}
