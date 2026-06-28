using System;
using System.Reflection;
using System.Runtime.InteropServices;

namespace GeFeSLE;

public static class AppMetadata
{
    public const string AppName = "GeFeSLE-systray";

    public static string Version => _version.Value;
    public static string UserAgent => _userAgent.Value;
    public static string WindowTitlePrefix => $"{AppName} v{Version}";

    private static readonly Lazy<string> _version = new(() =>
    {
        var versionAttribute = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        return NormalizeVersion(versionAttribute?.InformationalVersion ?? "0.0.0");
    });

    private static string NormalizeVersion(string version)
    {
        var trimmed = version.Trim();
        var plusIndex = trimmed.IndexOf('+');

        if (plusIndex < 0)
        {
            return trimmed;
        }

        var baseVersion = trimmed[..plusIndex];
        var gitHash = trimmed[(plusIndex + 1)..];

        if (gitHash.Length > 7)
        {
            gitHash = gitHash[..7];
        }

        return $"{baseVersion}+{gitHash}";
    }

    private static readonly Lazy<string> _userAgent = new(() =>
    {
        var os = RuntimeInformation.OSDescription.Trim();
        var architecture = RuntimeInformation.OSArchitecture.ToString();
        var dotnetVersion = Environment.Version;
        return $"{AppName}/{Version} ({os}; {architecture}) .NET/{dotnetVersion.Major}.{dotnetVersion.Minor}";
    });
}