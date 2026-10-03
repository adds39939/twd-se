using Microsoft.AspNetCore.Components;

namespace TwdSaveEditor.UI.Components;

public partial class CloudflareAnalytics
{
    private const string LiveHost = "twd-se.app";
    private const string BeaconConfig = """{"token": "e779e861ea124231a161f1b0c3cf8cbc"}""";

    [Inject]
    public NavigationManager Navigation { get; set; } = default!;

    private bool IsLiveSite => new Uri(Navigation.BaseUri).Host == LiveHost;
}
