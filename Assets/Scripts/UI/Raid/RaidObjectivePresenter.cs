using System.Collections.Generic;

using UnityEngine;

namespace WeaponWorkshop
{
    // 습격 목표·상자·탈출구·적 상태를 기존 도메인에서 읽어 View 표시 함수로 전달한다.
    public sealed class RaidObjectivePresenter
    {
        private readonly RaidObjectiveUI m_view;
        private readonly FirstPersonPlayer m_player;
        private readonly NightRaidBoundary[] m_exits;
        private readonly List<RaidCrateSpawner> m_spawners = new List<RaidCrateSpawner>();
        private readonly List<RaidCrateSpawner.Objective> m_objectives = new List<RaidCrateSpawner.Objective>();

        private PlayerModuleUnlocks m_unlocks;
        private NightRaidBoundary m_nearestExit;
        private bool m_spawnersScanned;
        private bool m_ready;
        private float m_nextRefresh;

        public RaidObjectivePresenter(RaidObjectiveUI _view, FirstPersonPlayer _player, NightRaidBoundary[] _exits)
        {
            m_view = _view;
            m_player = _player;
            m_exits = _exits;
        }

        public void Tick()
        {
            if (!m_ready)
            {
                if (!TryInitialize())
                {
                    m_view.HidePanel();
                    return;
                }
            }

            NightRaidController controller = NightRaidController.Instance;
            if (m_player == null || controller == null || !controller.IsInRaid || GameManager.GameplayBlocked)
            {
                m_view.HidePanel();
                return;
            }

            m_view.ShowPanel();
            bool refreshText = Time.unscaledTime >= m_nextRefresh;
            if (refreshText)
            {
                m_nextRefresh = Time.unscaledTime + .15f;
            }

            int remaining = 0;
            for (int i = 0; i < m_objectives.Count; i++)
            {
                RaidCrateSpawner.Objective objective = m_objectives[i];
                bool pending = (TutorialManager.TrainingRaidPending || !m_unlocks.IsUnlocked(objective.ModuleId)) &&
                    !controller.HasEarnedModule(objective.ModuleId);
                if (pending)
                {
                    remaining++;
                }

                bool visible = pending && objective.Target != null && objective.Target.gameObject.activeInHierarchy;
                string title = refreshText && visible ? "모듈: " + m_unlocks.GetModuleName(objective.ModuleId) : null;
                Vector3 world = visible ? objective.Target.position + Vector3.up : Vector3.zero;
                m_view.ShowObjectiveMarker(i, visible, world, title, refreshText && visible);
            }

            if (refreshText)
            {
                RefreshStatus(remaining);
            }

            bool showExit = remaining == 0 && m_nearestExit != null && !controller.IsExtracting;
            Vector3 exitWorld = showExit ? m_nearestExit.transform.position : Vector3.zero;
            string exitTitle = showExit && refreshText ? "가장 가까운 " + m_nearestExit.ExitName : null;
            m_view.ShowExitMarker(showExit, exitWorld, exitTitle, refreshText && showExit);
            UpdateEnemyMarkers(refreshText);
        }

        private bool TryInitialize()
        {
            if (m_unlocks == null)
            {
                m_unlocks = m_player != null ? m_player.GetComponent<PlayerModuleUnlocks>() : null;
                if (m_unlocks == null)
                {
                    return false;
                }
            }

            // NightRaidController가 입장 상태를 복원한 뒤 상자를 만들므로 생성이 끝날 때까지 기다린다.
            if (!m_spawnersScanned)
            {
                m_spawnersScanned = true;
                RaidCrateSpawner[] found = UnityEngine.Object.FindObjectsByType<RaidCrateSpawner>(
                    FindObjectsSortMode.None);
                foreach (RaidCrateSpawner spawner in found)
                {
                    if (spawner != null)
                    {
                        m_spawners.Add(spawner);
                    }
                }
            }

            foreach (RaidCrateSpawner spawner in m_spawners)
            {
                if (spawner != null && spawner.isActiveAndEnabled && !spawner.IsReady)
                {
                    return false;
                }
            }

            m_objectives.Clear();
            foreach (RaidCrateSpawner spawner in m_spawners)
            {
                if (spawner == null || !spawner.isActiveAndEnabled)
                {
                    continue;
                }

                foreach (RaidCrateSpawner.Objective objective in spawner.Objectives)
                {
                    m_objectives.Add(objective);
                }
            }

            m_view.EnsureObjectiveMarkers(m_objectives.Count);
            m_view.EnsureExitMarker();
            m_ready = true;
            return true;
        }

        private void RefreshStatus(int _remaining)
        {
            m_nearestExit = null;
            float nearestDistance = float.PositiveInfinity;
            if (_remaining == 0 && m_exits != null)
            {
                foreach (NightRaidBoundary exit in m_exits)
                {
                    if (exit == null || !exit.isActiveAndEnabled)
                    {
                        continue;
                    }

                    float distance = (exit.transform.position - m_player.transform.position).sqrMagnitude;
                    if (distance >= nearestDistance)
                    {
                        continue;
                    }

                    nearestDistance = distance;
                    m_nearestExit = exit;
                }
            }

            int remainingCrates = 0;
            foreach (RaidCrateSpawner spawner in m_spawners)
            {
                if (spawner != null && spawner.isActiveAndEnabled)
                {
                    remainingCrates += spawner.RemainingMaterialCrateCount;
                }
            }

            string objectiveStatus = _remaining > 0 ? $"남은 모듈 {_remaining}개" :
                m_objectives.Count > 0 ? "모듈 획득 완료 · 가까운 탈출구로 이동하세요" :
                "남은 모듈 없음 · 가까운 탈출구로 이동하세요";
            m_view.SetStatus($"남은 부품 상자 {remainingCrates}개\n{objectiveStatus}");
        }

        private void UpdateEnemyMarkers(bool _refreshText)
        {
            int shown = 0;
            for (int i = 0; i < RaidEnemyCore.ActiveCount; i++)
            {
                RaidEnemyCore enemy = RaidEnemyCore.ActiveAt(i);
                if (enemy == null || !enemy.isActiveAndEnabled || enemy.IsDestroyed || !enemy.IsRaidEnemy ||
                    enemy.gameObject.scene != m_view.gameObject.scene)
                {
                    continue;
                }

                string title = _refreshText ? "적: " + enemy.MarkerName : null;
                m_view.ShowEnemyMarker(shown++, enemy.MarkerPosition + Vector3.up, title, _refreshText);
            }

            m_view.HideEnemyMarkersFrom(shown);
        }
    }
}
