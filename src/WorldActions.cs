using System.Text.Json;
using iF2D;
using iFActionScript;

namespace BigWinnerMod;

public sealed record MapDestination(int Id, string Name, int X, int Y, int Dir, string Origin, bool Available);

internal static class WorldActions
{
    // Major region labels on the in-game city map.
    private static readonly Dictionary<int, string> MainPlaces = new()
    {
        [2] = "江城古城区", [4] = "北商业街", [6] = "胜利路", [7] = "东商业街",
        [8] = "旧雨巷", [9] = "旧雨巷市集", [10] = "旧雨路", [11] = "工坊路",
        [12] = "鲁氏制造集团", [13] = "长空电子厂", [14] = "江凌路", [15] = "加油站",
        [16] = "爱家家具", [17] = "凌江公园", [19] = "纵云家园", [20] = "4s店",
        [21] = "天机路", [23] = "别墅区", [24] = "垃圾场", [25] = "大赢家中心"
    };
    private static List<MapDestination>? maps;
    internal static IReadOnlyList<MapDestination> Maps => maps ??= (JsonSerializer.Deserialize<List<MapDestination>>(
        File.ReadAllText(Path.Combine(IVal.GamePath, "BigWinnerMod", "maps.json")))
        ?? throw new InvalidOperationException("地图清单未能读取"))
        .Where(map => MainPlaces.ContainsKey(map.Id))
        .Select(map => map with { Name = MainPlaces[map.Id] }).ToList();

    internal static double RestoreHappiness()
    {
        var data = RV.GameData;
        int need = RV.NowData.setCitizen[data.cityValue].happy;
        var actor = data.actor;
        // Happiness is derived from fun + food + comfortable; only fill the missing amount.
        long targetFun = Math.Max(actor.fun, (long)need - actor.food - actor.comfortable);
        actor.fun = checked((int)targetFun);
        RV.HUD?.status.updateHappy();
        return actor.Happiness;
    }
}
