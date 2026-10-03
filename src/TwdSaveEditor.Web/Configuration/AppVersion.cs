using System.Reflection;

namespace TwdSaveEditor.Web.Configuration;

public static class AppVersion
{
    public const string DevelopmentVersion = "dev";

    public static string Read()
    {
        var version = typeof(AppVersion).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        return string.IsNullOrWhiteSpace(version) ? DevelopmentVersion : version;
    }
}
