#if DEBUG
namespace Nop.Plugin.ExternalAuth.SSO.Constants;

/// <summary>
/// Allowed values for the scenario radio on the mock IdP login page.
/// Wrapped in <c>#if DEBUG</c> — does not exist in Release builds.
/// </summary>
public static class SsoMockScenarios
{
    /// <summary>The asserted email matches an existing customer; provisioning links the SSO identity to that account.</summary>
    public const string SuccessExisting = "success-existing";

    /// <summary>No matching customer; provisioning auto-registers when <c>AutoRegister=true</c>.</summary>
    public const string SuccessNew = "success-new";

    /// <summary>Asserted user carries one or more group GUIDs in the configured groups claim — exercises the group→role mapping pipeline.</summary>
    public const string SuccessWithGroups = "success-with-groups";

    /// <summary>Mock IdP rejects the user; provisioning is never invoked, audit row + redirect back to /login.</summary>
    public const string Failure = "failure";
}
#endif
