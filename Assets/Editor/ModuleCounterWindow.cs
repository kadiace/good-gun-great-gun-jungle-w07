using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace WeaponWorkshop
{
    // 확인용 에디터 창(Window > Module Counter). 씬의 모든 조립체에 대해 ModuleCounter 결과를 실시간 표시.
    // 씬·프리팹·런타임 코드에 아무것도 추가하지 않으며 빌드에 포함되지 않는다.
    public sealed class ModuleCounterWindow : EditorWindow
    {
        private static readonly Dictionary<ModuleKind, string> Names = new Dictionary<ModuleKind, string>
        {
            { ModuleKind.Extension, "연장" }, { ModuleKind.SplitTwo, "2방향 분할" }, { ModuleKind.SplitFour, "4방향 분할" },
            { ModuleKind.Penetration, "관통" }, { ModuleKind.FireRate, "연사" }, { ModuleKind.Explosion, "폭발" },
            { ModuleKind.Ricochet, "반사" }, { ModuleKind.Charge, "차지" }, { ModuleKind.Gravity, "중력" },
            { ModuleKind.Heat, "발열" }, { ModuleKind.Spin, "회전" }, { ModuleKind.Water, "물" },
            { ModuleKind.Homing, "유도" }, { ModuleKind.Shotgun, "산탄" }, { ModuleKind.Scope, "스코프" }, { ModuleKind.Speed, "가속" }, { ModuleKind.Color, "컬러" }
        };

        private Vector2 scroll;

        [MenuItem("Window/Module Counter")]
        private static void Open() => GetWindow<ModuleCounterWindow>("Module Counter");

        // Refresh about 10 times per second while open.
        private void OnInspectorUpdate() => Repaint();

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("전체 = 코어에 연결된 개수 / 연속 = 실제 발사되는 경로 하나 위 최대 누적 개수\n" +
                                    "최장 연속 = 발사 경로 하나의 블록 수(연장·분할 포함), 차단 블럭 제외", MessageType.None);
            CubeAssembly[] assemblies = FindObjectsByType<CubeAssembly>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (assemblies.Length == 0)
            {
                EditorGUILayout.LabelField("씬에 조립체(CubeAssembly)가 없습니다.");
                return;
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (CubeAssembly assembly in assemblies)
            {
                EditorGUILayout.Space();
                if (GUILayout.Button(assembly.name, EditorStyles.boldLabel)) Selection.activeObject = assembly;
                EditorGUILayout.LabelField("   최장 연속 모듈 수", ModuleCounter.LongestChain(assembly).ToString());
                EditorGUILayout.LabelField("   경로 최대 스탯 모듈 수", ModuleCounter.MaxStatModulesOnPath(assembly).ToString());
                Dictionary<ModuleKind, int> totals = ModuleCounter.TotalCounts(assembly);
                Dictionary<ModuleKind, int> paths = ModuleCounter.MaxPathCounts(assembly);
                if (totals.Count == 0)
                {
                    EditorGUILayout.LabelField("   모듈 없음");
                    continue;
                }
                foreach (ModuleKind kind in System.Enum.GetValues(typeof(ModuleKind)))
                {
                    if (!totals.TryGetValue(kind, out int total)) continue;
                    paths.TryGetValue(kind, out int path);
                    EditorGUILayout.LabelField($"   {Names[kind]}", $"전체 {total}  /  연속 {path}");
                }
            }
            EditorGUILayout.EndScrollView();
        }
    }
}
