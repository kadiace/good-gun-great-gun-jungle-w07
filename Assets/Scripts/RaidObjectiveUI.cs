using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace WeaponWorkshop
{
    public sealed class RaidObjectiveUI : MonoBehaviour
    {
        private static readonly Color EnemyMarkerColor = new Color(1f, 0.06f, 0.08f, 1f);
        [SerializeField] private FirstPersonPlayer player;
        [SerializeField] private Camera viewCamera;
        [SerializeField] private RectTransform panel;
        [SerializeField] private TextMeshProUGUI status;
        [SerializeField] private TextMeshProUGUI markerTemplate;
        [SerializeField] private NightRaidBoundary[] exits;
        private readonly List<RaidCrateSpawner.Objective> objectives = new List<RaidCrateSpawner.Objective>();
        private readonly List<TextMeshProUGUI> markers = new List<TextMeshProUGUI>();
        private readonly List<TextMeshProUGUI> enemyMarkers = new List<TextMeshProUGUI>();
        private TextMeshProUGUI exitMarker;
        private PlayerModuleUnlocks unlocks;
        private RaidCrateSpawner[] spawners;
        private NightRaidBoundary nearestExit;
        private bool ready;
        private float nextRefresh;

        private void Awake()
        {
            ready = false;
            Hide();
            if (markerTemplate != null) markerTemplate.gameObject.SetActive(false);
        }

        private IEnumerator Start()
        {
            if (player == null || viewCamera == null || panel == null || status == null || markerTemplate == null) yield break;
            unlocks = player.GetComponent<PlayerModuleUnlocks>();
            if (unlocks == null) yield break;
            // NightRaidController가 입장 상태를 복원한 뒤 상자를 만들므로 생성이 끝날 때까지 기다린다.
            spawners = Object.FindObjectsByType<RaidCrateSpawner>(FindObjectsSortMode.None);
            foreach (var spawner in spawners)
            {
                if (spawner == null || !spawner.isActiveAndEnabled) continue;
                while (spawner != null && spawner.isActiveAndEnabled && !spawner.IsReady) yield return null;
                if (spawner == null) continue;
                foreach (var objective in spawner.Objectives)
                {
                    objectives.Add(objective);
                    markers.Add(Instantiate(markerTemplate, panel));
                }
            }
            exitMarker = Instantiate(markerTemplate, panel);
            exitMarker.color = new Color(.35f, 1f, .6f);
            ready = true;
        }

        private void LateUpdate()
        {
            if (!ready || player == null || viewCamera == null || NightRaidController.Instance == null ||
                !NightRaidController.Instance.IsInRaid || GameManager.GameplayBlocked)
            { Hide(); return; }
            panel.gameObject.SetActive(true);
            bool refreshText = Time.unscaledTime >= nextRefresh;
            if (refreshText) nextRefresh = Time.unscaledTime + .15f;
            int remaining = 0;
            for (int i = 0; i < objectives.Count; i++)
            {
                var objective = objectives[i];
                bool pending = (TutorialManager.TrainingRaidPending || !unlocks.IsUnlocked(objective.ModuleId)) &&
                    !NightRaidController.Instance.HasEarnedModule(objective.ModuleId);
                if (pending) remaining++;
                bool visible = pending && objective.Target != null && objective.Target.gameObject.activeInHierarchy;
                markers[i].gameObject.SetActive(visible);
                if (visible) PositionMarker(markers[i], objective.Target.position + Vector3.up,
                    "모듈: " + unlocks.GetModuleName(objective.ModuleId), refreshText);
            }
            if (refreshText)
            {
                nearestExit = null;
                float nearestDistance = float.PositiveInfinity;
                if (remaining == 0 && exits != null)
                    foreach (var exit in exits)
                    {
                        if (exit == null || !exit.isActiveAndEnabled) continue;
                        float distance = (exit.transform.position - player.transform.position).sqrMagnitude;
                        if (distance >= nearestDistance) continue;
                        nearestDistance = distance;
                        nearestExit = exit;
                    }
                int remainingCrates = 0;
                if (spawners != null)
                    foreach (var spawner in spawners)
                        if (spawner != null && spawner.isActiveAndEnabled)
                            remainingCrates += spawner.RemainingMaterialCrateCount;
                string objectiveStatus = remaining > 0 ? $"남은 모듈 {remaining}개" :
                    objectives.Count > 0 ? "모듈 획득 완료 · 가까운 탈출구로 이동하세요" : "남은 모듈 없음 · 가까운 탈출구로 이동하세요";
                status.text = $"남은 부품 상자 {remainingCrates}개\n{objectiveStatus}";
            }
            bool showExit = remaining == 0 && nearestExit != null && !NightRaidController.Instance.IsExtracting;
            exitMarker.gameObject.SetActive(showExit);
            if (showExit) PositionMarker(exitMarker, nearestExit.transform.position,
                "가장 가까운 " + nearestExit.ExitName, refreshText);
            UpdateEnemyMarkers(refreshText);
        }

        private void UpdateEnemyMarkers(bool refreshText)
        {
            int shown = 0;
            for (int i = 0; i < RaidEnemyCore.ActiveCount; i++)
            {
                RaidEnemyCore enemy = RaidEnemyCore.ActiveAt(i);
                if (enemy == null || !enemy.isActiveAndEnabled || enemy.IsDestroyed || !enemy.IsRaidEnemy ||
                    enemy.gameObject.scene != gameObject.scene) continue;

                while (enemyMarkers.Count <= shown)
                {
                    TextMeshProUGUI marker = Instantiate(markerTemplate, panel);
                    marker.color = EnemyMarkerColor;
                    marker.gameObject.SetActive(false);
                    enemyMarkers.Add(marker);
                }

                TextMeshProUGUI activeMarker = enemyMarkers[shown++];
                activeMarker.gameObject.SetActive(true);
                string title = refreshText ? "적: " + enemy.MarkerName : null;
                PositionMarker(activeMarker, enemy.MarkerPosition + Vector3.up,
                    title, refreshText);
            }

            for (int i = shown; i < enemyMarkers.Count; i++)
                enemyMarkers[i].gameObject.SetActive(false);
        }

        private void PositionMarker(TextMeshProUGUI marker, Vector3 world, string title, bool refreshText)
        {
            Vector3 viewport = viewCamera.WorldToViewportPoint(world);
            Vector2 direction = new Vector2(viewport.x - .5f, viewport.y - .5f);
            bool behind = viewport.z <= 0f;
            if (behind)
            {
                direction = -direction;
                if (direction.sqrMagnitude < .001f) direction = Vector2.down;
            }
            Vector2 half = panel.rect.size * .5f;
            Vector2 limit = new Vector2(Mathf.Max(1f, half.x - 155f), Mathf.Max(1f, half.y - 105f));
            Vector2 point = Vector2.Scale(direction, panel.rect.size);
            float edge = Mathf.Max(Mathf.Abs(point.x) / limit.x, Mathf.Abs(point.y) / limit.y);
            bool offscreen = behind || edge > 1f;
            if (offscreen) point /= Mathf.Max(edge, .001f);
            marker.rectTransform.anchoredPosition = point;
            if (!refreshText) return;
            float distance = Vector3.Distance(player.transform.position, world);
            float height = world.y - player.transform.position.y;
            string elevation = height > 3f ? $" · 위 {height:0}m" : height < -3f ? $" · 아래 {-height:0}m" : "";
            string arrow = offscreen ? (Mathf.Abs(direction.x) > Mathf.Abs(direction.y)
                ? (direction.x > 0 ? "→ " : "← ") : (direction.y > 0 ? "↑ " : "↓ ")) : "";
            marker.text = $"{arrow}{title}\n{distance:0}m{elevation}";
        }

        private void Hide() { if (panel != null) panel.gameObject.SetActive(false); }
        private void OnDisable() => Hide();
    }
}
