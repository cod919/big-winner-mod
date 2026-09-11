using System.Reflection;
using HarmonyLib;
using iF2D;
using iF2D.Graphics;
using iFActionScript;

namespace BigWinnerMod;

internal static class PokerReroll
{
    private static FieldInfo deckField = null!;
    private static FieldInfo handField = null!;
    private static FieldInfo windowsField = null!;
    private static MethodInfo refreshHand = null!;
    private static ISprite? button;
    private static long nextClick;
    private static bool failed;

    internal static void Install(Harmony harmony)
    {
        // Names verified against this installed version of the game's 熔炼 (card 96) implementation.
        deckField = AccessTools.Field(typeof(PKMain), "YKqWigmfii");
        handField = AccessTools.Field(typeof(PKMain), "V01WaVxmb7");
        windowsField = AccessTools.Field(typeof(IVal), "WindowBases");
        refreshHand = AccessTools.Method(typeof(PKMain), "Onl5aHAsPm", new[] { typeof(bool) });
        if (deckField?.FieldType != typeof(List<(int, int)>) || handField?.FieldType != typeof(List<LPoker>)
            || windowsField?.FieldType != typeof(List<IWindowBase>) || refreshHand?.ReturnType != typeof(void))
            throw new InvalidOperationException("游戏的熔炼入口与当前 Mod 不匹配");
        harmony.Patch(AccessTools.Method(typeof(LCardRun), "update"),
            prefix: new HarmonyMethod(typeof(PokerReroll), nameof(Update)));
        Mod.Log("PATCHED: poker reroll; native card 96 deck and hand refresh verified");
    }

    internal static void Hide()
    {
        if (button != null) button.visible = false;
    }

    // Called from the game's own card-input update, after its dealing/dialog/selection checks.
    private static bool Update(LCardRun __instance, ref bool __result)
    {
        if (failed || Mod.Window != null || __instance.isUsering) return true;
        try
        {
            var windows = (List<IWindowBase>)windowsField.GetValue(null)!;
            if (windows.Count == 0 || windows[0] is not PKMain pk) return true;
            var deck = (List<(int c, int n)>?)deckField.GetValue(pk);
            var hand = (List<LPoker>?)handField.GetValue(pk);
            if (deck == null || deck.Count < 3 || hand == null || hand.Count != 3) return true;
            if (button == null)
            {
                button = new ISprite(208, 44, new IColor(54, 72, 91)) { z = 90000 };
                button.drawTextQ("整手换牌 [F2]", 20, 12, new IColor(241, 209, 151), 20);
            }
            button.x = IScreen.GWidth - 232;
            button.y = 154;
            button.visible = true;
            bool click = IInput.IsUp && IInput.OnMouseButton == IInput.MouseButton.Left
                && IInput.GetGameMouseX() >= button.x && IInput.GetGameMouseX() < button.x + 208
                && IInput.GetGameMouseY() >= button.y && IInput.GetGameMouseY() < button.y + 44;
            if (!click && !IInput.IsKeyDown(113)) return true;
            IInput.IsUp = false;
            __result = true;
            if (Environment.TickCount64 < nextClick) return false;
            nextClick = Environment.TickCount64 + 1000;

            // Same exchange as 熔炼: take three cards from the existing shuffled deck;
            // return each old card to its tail, then use the native hand/animation refresh.
            var before = string.Join(",", hand.Select(p => $"{p.data.p}:{p.data.n}"));
            for (int i = 0; i < 3; i++)
            {
                var replacement = deck[0];
                deck.RemoveAt(0);
                deck.Add(hand[i].data);
                hand[i].changeData(replacement);
            }
            refreshHand.Invoke(pk, new object[] { true });
            Mod.Log($"Poker reroll: {before} -> {string.Join(",", hand.Select(p => $"{p.data.p}:{p.data.n}"))}; deck={deck.Count}");
            return false;
        }
        catch (Exception ex)
        {
            failed = true;
            Hide();
            Mod.Log("POKER REROLL DISABLED: " + ex);
            return true;
        }
    }
}
