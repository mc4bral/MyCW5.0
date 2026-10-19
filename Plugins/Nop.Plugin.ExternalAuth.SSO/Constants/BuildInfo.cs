namespace Nop.Plugin.ExternalAuth.SSO.Constants;

/// <summary>
/// Compile-time build metadata exposed as runtime constants. Lets Razor views and other
/// runtime code branch on the build configuration without re-implementing the <c>#if DEBUG</c>
/// dance in every file.
/// </summary>
/// <remarks>
/// Constant folding in the C# compiler eliminates the dead branch — an
/// <c>if (BuildInfo.IsDebugBuild)</c> block compiles to dead code in Release and the JIT
/// drops it entirely. Mock-mode UI gates against this constant.
/// </remarks>
public static class BuildInfo
{
    /// <summary>
    /// <c>true</c> when the plugin assembly was built with the <c>DEBUG</c> symbol defined,
    /// <c>false</c> otherwise. Used to render dev-only UI (Mock Mode card) only on dev builds.
    /// </summary>
    public const bool IsDebugBuild =
#if DEBUG
        true;
#else
        false;
#endif
}
