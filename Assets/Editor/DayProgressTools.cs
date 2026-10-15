using UnityEditor;
using UnityEngine;

internal static class DayProgressTools
{
    private const string MenuRoot = "Tools/Weapon Workshop/Go To Day/";

    [MenuItem(MenuRoot + "Day 1/Day (Before Opening)")]
    private static void Day1Morning() => Jump(1, false);
    [MenuItem(MenuRoot + "Day 1/Night (After Closing)")]
    private static void Day1Night() => Jump(1, true);
    [MenuItem(MenuRoot + "Day 2/Day (Before Opening)")]
    private static void Day2Morning() => Jump(2, false);
    [MenuItem(MenuRoot + "Day 2/Night (After Closing)")]
    private static void Day2Night() => Jump(2, true);
    [MenuItem(MenuRoot + "Day 3/Day (Before Opening)")]
    private static void Day3Morning() => Jump(3, false);
    [MenuItem(MenuRoot + "Day 3/Night (After Closing)")]
    private static void Day3Night() => Jump(3, true);
    [MenuItem(MenuRoot + "Day 4/Day (Before Opening)")]
    private static void Day4Morning() => Jump(4, false);
    [MenuItem(MenuRoot + "Day 4/Night (After Closing)")]
    private static void Day4Night() => Jump(4, true);
    [MenuItem(MenuRoot + "Day 5/Day (Before Opening)")]
    private static void Day5Morning() => Jump(5, false);
    [MenuItem(MenuRoot + "Day 5/Night (After Closing)")]
    private static void Day5Night() => Jump(5, true);
    [MenuItem(MenuRoot + "Day 6/Day (Before Opening)")]
    private static void Day6Morning() => Jump(6, false);
    [MenuItem(MenuRoot + "Day 6/Night (After Closing)")]
    private static void Day6Night() => Jump(6, true);
    [MenuItem(MenuRoot + "Day 7/Day (Before Opening)")]
    private static void Day7Morning() => Jump(7, false);
    [MenuItem(MenuRoot + "Day 7/Night (After Closing)")]
    private static void Day7Night() => Jump(7, true);

    [MenuItem(MenuRoot + "Day 1/Day (Before Opening)", true)]
    [MenuItem(MenuRoot + "Day 1/Night (After Closing)", true)]
    [MenuItem(MenuRoot + "Day 2/Day (Before Opening)", true)]
    [MenuItem(MenuRoot + "Day 2/Night (After Closing)", true)]
    [MenuItem(MenuRoot + "Day 3/Day (Before Opening)", true)]
    [MenuItem(MenuRoot + "Day 3/Night (After Closing)", true)]
    [MenuItem(MenuRoot + "Day 4/Day (Before Opening)", true)]
    [MenuItem(MenuRoot + "Day 4/Night (After Closing)", true)]
    [MenuItem(MenuRoot + "Day 5/Day (Before Opening)", true)]
    [MenuItem(MenuRoot + "Day 5/Night (After Closing)", true)]
    [MenuItem(MenuRoot + "Day 6/Day (Before Opening)", true)]
    [MenuItem(MenuRoot + "Day 6/Night (After Closing)", true)]
    [MenuItem(MenuRoot + "Day 7/Day (Before Opening)", true)]
    [MenuItem(MenuRoot + "Day 7/Night (After Closing)", true)]
    private static bool CanJump() => EditorApplication.isPlaying;

    private static void Jump(int day, bool night)
    {
        if (!CanJump()) return;
        _DayManager[] managers = Object.FindObjectsByType<_DayManager>(FindObjectsSortMode.None);
        if (managers.Length != 1 || !managers[0].DebugJumpToDay(day, night))
        {
            Debug.LogWarning("진행상태를 이동할 수 없습니다. 공방 씬의 활성 날짜 관리자와 전환 상태를 확인하세요.");
            return;
        }
        Debug.Log($"진행상태 이동: {day}일차 {(night ? "밤 (마감 후)" : "낮 (개점 전)")}");
    }
}
