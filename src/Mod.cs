using HarmonyLib;
using iF2D;
using iF2D.Graphics;
using iFActionScript;

namespace BigWinnerMod;

public static class Mod
{
    internal static double Speed = 1;
    internal static Panel? Window;
    internal static (int map, int x, int y, int dir)? Mark;
    private static GMain? owner;
    private static ISprite? badge;
    private static bool failed;

    public static void Install()
    {
        Log("Loading prototype 0.3.1");
        var harmony = new Harmony("local.bigwinner.convenience");
        harmony.Patch(AccessTools.Method(typeof(GameRoot), "update"),
            postfix: new HarmonyMethod(typeof(Mod), nameof(Tick)));
        harmony.Patch(AccessTools.Method(typeof(LActorEx), "bSpeed"),
            postfix: new HarmonyMethod(typeof(Mod), nameof(ApplySpeed)));
        Log("PATCHED: GameRoot.update, LActorEx.bSpeed");
        PokerReroll.Install(harmony);
    }

    internal static void Log(string text)
    {
        try { File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "BigWinnerMod", "mod.log"),
            DateTime.Now.ToString("HH:mm:ss ") + text + Environment.NewLine); }
        catch { }
    }

    private static void ApplySpeed(LActorEx __instance, ref double __result)
    {
        if (RV.NowMap?.actor == __instance && !RV.isStory && !RV.lockActor && !__instance.doAction)
            __result *= Speed;
    }

    private static void Tick()
    {
        PokerReroll.Hide();
        if (failed || IVal.NowScene is SStart || IVal.NowScene == null) return;
        try
        {
            if (!ReferenceEquals(owner, RV.GameData))
            {
                owner = RV.GameData;
                Mark = null;
            }
            if (badge == null)
            {
                badge = new ISprite(148, 36, new IColor(28, 38, 49)) { z = 90000 };
                badge.drawTextQ("MOD  [F1]", 18, 7, new IColor(236, 205, 139), 20);
                Log("UI badge created");
            }
            badge.x = IScreen.GWidth - 168;
            badge.y = 104;
            badge.visible = Window == null;
            bool click = Window == null && IInput.IsUp && IInput.OnMouseButton == IInput.MouseButton.Left
                && IInput.GetGameMouseX() >= badge.x && IInput.GetGameMouseX() < badge.x + 148
                && IInput.GetGameMouseY() >= badge.y && IInput.GetGameMouseY() < badge.y + 36;
            if (IInput.IsKeyDown(112) || click)
            {
                IInput.IsUp = false;
                if (Window != null) Window.Close();
                else if (IUtility.WindowCount() == 0 && !RV.isMoveing)
                {
                    Window = new Panel();
                    badge.visible = false;
                    Log("Panel opened; scene=" + IVal.NowScene.GetType().Name);
                }
            }
        }
        catch (Exception ex)
        {
            Log("UI DISABLED: " + ex);
            Window?.Close();
            failed = true;
        }
    }

    internal static bool Ready => IVal.NowScene is SMain && RV.GameData?.actor != null
        && RV.NowMap?.actor?.characters != null && !RV.isStory && !RV.isMoveing
        && !RV.lockActor && !RV.NowMap.actor.doAction && IUtility.WindowCount() <= 1;

    internal static void Teleport(int map, int x, int y, int dir)
    {
        if (!Ready) throw new InvalidOperationException("请在自由行走时传送");
        if (map <= 0 || !File.Exists(Path.Combine(IVal.GamePath, "Data", $"map{map}.ifMap")))
            throw new InvalidOperationException("目标地图不可用");
        Log($"Teleport requested: {RV.NowMap.data.id} -> {map} ({x},{y})");
        Window?.Close();
        RV.NowMap.reMoveMap(map, x, y, dir);
    }
}

internal sealed class Panel : IWindowBase
{
    private const int Width = 460, Height = 624;
    private readonly ISprite canvas;
    private readonly List<(int x, int y, int w, int h, string label, Action action, bool needsReady)> buttons = new();
    private string status = "选择需要的操作；F1 / Esc 关闭";
    private bool closed;
    private bool mapsMenu;
    private int mapPage;
    private const int MapsPerPage = 10;

    public Panel()
    {
        canvas = new ISprite(Width, Height) { z = 90010 };
        sizeChange();
        ShowMain();
    }

    private void ShowMain()
    {
        mapsMenu = false;
        buttons.Clear();
        Add(366, 14, 74, 34, "关闭", Close, false);
        foreach (var (n, x) in new[] { (1.0, 20), (1.5, 125), (2.0, 230), (3.0, 335) })
            Add(x, 119, 95, 38, $"{n:0.#} 倍", () => { Mod.Speed = n; status = $"移速已设为 {n:0.#} 倍"; Mod.Log(status); });
        Add(20, 210, 420, 40, "传送菜单  >  主要地点 / 记录点 / 住处", ShowMaps, false);
        Add(20, 319, 200, 40, "金钱 +1,000", () => AddMoney(1000));
        Add(240, 319, 200, 40, "金钱 +10,000", () => AddMoney(10000));
        Add(20, 372, 200, 40, "抽卡券 +10", () => { RV.GameData.ticket = checked(RV.GameData.ticket + 10); status = "抽卡券已增加 10 张"; });
        Add(240, 372, 200, 40, "天赋点 +10", () => { RV.GameData.talentValue = checked(RV.GameData.talentValue + 10); status = "天赋点已增加 10"; });
        Add(20, 483, 130, 40, "恢复精力", () => { RV.GameData.actor.Energy = RV.GameData.actor.EnergyMax; status = "精力已回满"; });
        Add(165, 483, 130, 40, "恢复饱腹", () => { RV.GameData.actor.Hunger = RV.GameData.actor.HungerMax; status = "饱腹已回满"; });
        Add(310, 483, 130, 40, "恢复幸福", () => { status = $"幸福度已恢复至 {WorldActions.RestoreHappiness():P0}"; });
        Draw();
    }

    private void ShowMaps()
    {
        var maps = WorldActions.Maps;
        mapsMenu = true;
        buttons.Clear();
        Add(366, 14, 74, 34, "关闭", Close, false);
        Add(20, 57, 135, 31, "< 返回主菜单", ShowMain, false);
        Add(20, 98, 130, 34, "记录位置", () =>
        {
            var a = RV.NowMap.actor;
            Mod.Mark = (RV.NowMap.data.id, (int)a.characters.x, (int)a.characters.y, a.dir);
            status = $"已记录：地图 {Mod.Mark.Value.map}";
            Mod.Log(status + $" ({Mod.Mark.Value.x},{Mod.Mark.Value.y})");
        });
        Add(165, 98, 130, 34, "返回记录点", () =>
        {
            var m = Mod.Mark ?? throw new InvalidOperationException("请先记录一个位置");
            Mod.Teleport(m.map, m.x, m.y, m.dir);
        });
        Add(310, 98, 130, 34, "回住处", () =>
        {
            var a = RV.GameData.actor;
            Mod.Teleport(a.sleepMapId, a.sleepX, a.sleepY, 0);
        });
        for (int i = 0; i < MapsPerPage && mapPage * MapsPerPage + i < maps.Count; i++)
        {
            var map = maps[mapPage * MapsPerPage + i];
            string name = map.Name.Split('_')[0];
            if (name.Length > 17) name = name[..17] + "…";
            string marker = map.Origin == "地图中心附近" ? " *" : "";
            Add(20, 147 + i * 35, 420, 31, $"{map.Id:000}  {name}{marker}", () =>
            {
                if (!map.Available) throw new InvalidOperationException("这张地图没有可用落点");
                Mod.Log($"Map destination: {map.Id} {map.Name}; {map.Origin}");
                Mod.Teleport(map.Id, map.X, map.Y, map.Dir);
            });
        }
        Add(20, 512, 115, 34, "< 上一页", () => ChangePage(-1), false);
        Add(325, 512, 115, 34, "下一页 >", () => ChangePage(1), false);
        Draw();
    }

    private void ChangePage(int delta)
    {
        mapPage = Math.Clamp(mapPage + delta, 0, Math.Max(0, (WorldActions.Maps.Count - 1) / MapsPerPage));
        ShowMaps();
    }

    private void Add(int x, int y, int w, int h, string label, Action action, bool needsReady = true) => buttons.Add((x, y, w, h, label, action, needsReady));

    private void AddMoney(int amount)
    {
        // The game's save format stores money * 100 in a signed 32-bit integer.
        if (RV.GameData.money > 20000000 - amount)
            throw new InvalidOperationException("本原型最多将金钱增加到 2 千万");
        RV.GameData.money += amount;
        status = $"金钱已增加 {amount:N0}";
    }

    public override bool update()
    {
        if (closed) return true;
        try
        {
            if (IInput.IsKeyDown(27)) { if (mapsMenu) ShowMain(); else Close(); return true; }
            if (mapsMenu)
            {
                if (IInput.IsKeyDown(37)) ChangePage(-1);
                if (IInput.IsKeyDown(39)) ChangePage(1);
                if (IInput.MouseWheel != 0) { ChangePage(IInput.MouseWheel > 0 ? -1 : 1); IInput.MouseWheel = 0; }
            }
            sizeChange();
            if (IInput.IsUp && IInput.OnMouseButton == IInput.MouseButton.Left)
            {
                double x = IInput.GetGameMouseX() - canvas.x, y = IInput.GetGameMouseY() - canvas.y;
                IInput.IsUp = false;
                foreach (var b in buttons)
                {
                    if (x < b.x || x >= b.x + b.w || y < b.y || y >= b.y + b.h) continue;
                    if (b.needsReady && !Mod.Ready) status = "请先进入存档，并结束剧情或其他操作";
                    else
                    {
                        b.action();
                        Mod.Log("Action: " + b.label);
                    }
                    if (!closed) Draw();
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            status = ex is InvalidOperationException ? ex.Message : "本次操作失败，请查看 mod.log";
            Mod.Log("Action failed: " + ex);
            if (!closed) Draw();
        }
        return true;
    }

    private void Text(string text, int x, int y, int size = 20, IColor? color = null) =>
        canvas.drawTextQ(text, x, y, color ?? new IColor(227, 232, 236), size);

    private void Draw()
    {
        canvas.clearBitmap();
        canvas.drawRect(new IRect(0, 0, Width, Height), new IColor(25, 33, 44, 250));
        canvas.drawRect(new IRect(0, 0, Width, 4), new IColor(215, 180, 109));
        Text(mapsMenu ? "传送 · 主要地点" : "江城大赢家 · 便捷面板", 20, 21, 23, new IColor(241, 209, 151));
        if (mapsMenu)
        {
            Text($"共 {WorldActions.Maps.Count} 处 · 点击地点传送", 177, 66, 17);
            int pages = Math.Max(1, (WorldActions.Maps.Count + MapsPerPage - 1) / MapsPerPage);
            Text($"第 {mapPage + 1} / {pages} 页", 162, 522, 18);
            Text("传送到各区域入口；Esc 返回主菜单", 20, 582, 17, new IColor(149, 168, 186));
        }
        else
        {
            Text("原型 0.3.1   ·   面板打开时暂停场景", 20, 62, 17, new IColor(149, 168, 186));
            Text($"移动速度   当前 {Mod.Speed:0.#} 倍", 20, 91);
            Text("传送", 20, 180);
            Text(Mod.Mark is { } m ? $"记录点：地图 {m.map}（本次游戏有效）" : "记录点：尚未记录", 20, 259, 17);
            Text("资源", 20, 290);
            if (RV.GameData != null)
                Text($"金钱 {RV.GameData.money:N0}  券 {RV.GameData.ticket}  天赋 {RV.GameData.talentValue}", 20, 423, 17);
            Text("状态恢复", 20, 455);
            Text("资源随游戏正常存档；移速不写入存档", 20, 584, 17, new IColor(149, 168, 186));
        }
        foreach (var b in buttons)
        {
            bool enabled = !b.needsReady || Mod.Ready;
            canvas.drawRect(new IRect(b.x, b.y, b.w, b.h), enabled ? new IColor(54, 72, 91) : new IColor(43, 49, 58));
            Text(b.label, b.x + 12, b.y + (b.h <= 34 ? 6 : 9), 18, enabled ? new IColor(235, 239, 242) : new IColor(133, 142, 154));
        }
        Text(status, 20, 550, 17, new IColor(241, 209, 151));
    }

    public override void sizeChange()
    {
        if (canvas == null) return;
        canvas.x = Math.Max(12, IScreen.GWidth - Width - 24);
        canvas.y = Math.Max(12, (IScreen.GHeight - Height) / 2);
    }

    internal void Close()
    {
        if (closed) return;
        closed = true;
        canvas.dispose();
        base.dispose();
        Mod.Window = null;
    }

    public override void dispose() => Close();
}
