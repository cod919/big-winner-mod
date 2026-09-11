using System.Runtime.CompilerServices;

internal static class StartupHook
{
    public static void Initialize()
    {
        try { Install(); }
        catch (Exception ex) { BigWinnerMod.Mod.Log("LOAD FAILED: " + ex); }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Install() => BigWinnerMod.Mod.Install();
}
