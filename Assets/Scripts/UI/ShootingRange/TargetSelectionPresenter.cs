using System;
using System.Globalization;

using UnityEngine;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    /// <summary>
    /// TargetSelectionView와 TargetBell·GameManager·ShootingTestBinding 도메인을 연결해
    /// 표적 소환·시험 시작·커스텀 큐브 소환·내부 청소 요청과 표시 가공을 담당하는 Presenter입니다.
    /// </summary>
    public sealed class TargetSelectionPresenter : IDisposable
    {
        private const string PRESENTER_NAME = "TargetSelectionPresenter";
        private const float CUSTOM_PART_SIZE = .2f;
        private const int CUSTOM_MAX_PARTS = 125000;

        private readonly TargetSelectionView m_view;
        private readonly GameObject[] m_targetPrefabs;
        private readonly string[] m_targetNames;
        private readonly ShootingTestBinding[] m_timedTests;
        private readonly GameObject m_customTargetPrefab;

        private GameManager m_game;
        private TargetBell m_bell;
        private bool m_isInitialized;
        private bool m_hasLoggedDependencyError;

        public TargetSelectionPresenter(TargetSelectionView _view, GameObject[] _targetPrefabs,
            string[] _targetNames, ShootingTestBinding[] _timedTests, GameObject _customTargetPrefab)
        {
            m_view = _view;
            m_targetPrefabs = _targetPrefabs ?? Array.Empty<GameObject>();
            m_targetNames = _targetNames ?? Array.Empty<string>();
            m_timedTests = _timedTests ?? Array.Empty<ShootingTestBinding>();
            m_customTargetPrefab = _customTargetPrefab;
        }

        /// <summary>
        /// View를 초기화하고 행·효과음·게임 종료 요청 연결을 준비합니다. 중복 호출에 안전합니다.
        /// </summary>
        public bool Initialize()
        {
            if (m_isInitialized)
            {
                return true;
            }

            if (m_view == null)
            {
                if (!m_hasLoggedDependencyError)
                {
                    m_hasLoggedDependencyError = true;
                    Debug.LogError("[" + PRESENTER_NAME + "] Initialize 실패: TargetSelectionView 참조가 없습니다.");
                }

                return false;
            }

            if (!m_view.Initialize())
            {
                return false;
            }

            m_view.CloseRequested -= OnCloseRequested;
            m_view.CloseRequested += OnCloseRequested;
            m_view.TargetSelected -= OnTargetSelected;
            m_view.TargetSelected += OnTargetSelected;
            m_view.TimedTestSelected -= OnTimedTestSelected;
            m_view.TimedTestSelected += OnTimedTestSelected;
            m_view.CustomToggleRequested -= OnCustomToggleRequested;
            m_view.CustomToggleRequested += OnCustomToggleRequested;
            m_view.SpawnCustomRequested -= OnSpawnCustomRequested;
            m_view.SpawnCustomRequested += OnSpawnCustomRequested;
            m_view.ClearSpawnedRequested -= OnClearSpawnedRequested;
            m_view.ClearSpawnedRequested += OnClearSpawnedRequested;

            AttachGame();
            ValidateRequiredTests();
            m_view.EnsureRows(m_targetPrefabs.Length, m_timedTests.Length);
            RegisterRowSounds();
            m_isInitialized = true;
            return true;
        }

        /// <summary>
        /// View 이벤트와 GameManager 종료 요청 구독을 해제합니다. 중복 호출에 안전합니다.
        /// </summary>
        public void Dispose()
        {
            if (m_view != null)
            {
                m_view.CloseRequested -= OnCloseRequested;
                m_view.TargetSelected -= OnTargetSelected;
                m_view.TimedTestSelected -= OnTimedTestSelected;
                m_view.CustomToggleRequested -= OnCustomToggleRequested;
                m_view.SpawnCustomRequested -= OnSpawnCustomRequested;
                m_view.ClearSpawnedRequested -= OnClearSpawnedRequested;
                m_view.Dispose();
            }

            DetachGame();
            m_isInitialized = false;
        }

        /// <summary>
        /// 표적 선택창을 열고 주문 필수 표시·선택 가능 상태·기본 선택을 적용합니다.
        /// </summary>
        public bool Open(TargetBell _source)
        {
            if (!m_isInitialized && !Initialize())
            {
                return false;
            }

            if (_source == null || GameManager.GameplayBlocked)
            {
                return false;
            }

            GameManager game = GameManager.Instance;
            if (game == null)
            {
                return false;
            }

            AttachGame();
            m_bell = _source;

            for (int i = 0; i < m_targetPrefabs.Length; i++)
            {
                bool interactable = m_targetPrefabs[i] != null;
                string label = i < m_targetNames.Length
                    ? m_targetNames[i]
                    : m_targetPrefabs[i] != null ? m_targetPrefabs[i].name : "없음";
                m_view.SetTargetRow(i, label, interactable);
            }

            var required = _source.RequiredTestIds() ?? new System.Collections.Generic.List<string>();
            var tests = TableManager.Instance.ShootingTestTable;
            var unlocks = game.UI != null && game.UI.Player != null ? game.UI.Player.GetComponent<PlayerModuleUnlocks>() : null;
            Button selected = null;
            for (int i = 0; i < m_timedTests.Length; i++)
            {
                ShootingTestBinding data = m_timedTests[i];
                bool mandatory = data != null && required.Contains(data.Id);
                bool interactable = _source.TestController != null && data != null && data.IsAvailable && tests.CanStart(data.Id, unlocks) &&
                    OnboardingManager.AllowsShootingGuideTest(_source, data.Id);
                string label = data == null
                    ? "미설정 테스트"
                    : (mandatory ? "<color=#FFFFFF>[주문 필수]</color> " : "") + data.DisplayName + " · " +
                      data.TimeLimit.ToString("0") + "초";
                if (data != null && !tests.CanStart(data.Id, unlocks))
                    label += " · " + tests.UnlockDescription(data.Id, unlocks);
                m_view.SetTimedTestRow(i, label, interactable, mandatory);
            }

            foreach (string id in required)
            {
                int index = Array.FindIndex(m_timedTests, test => test != null && test.Id == id);
                Button button = m_view.GetTimedTestButton(index);
                if (button != null && button.interactable)
                {
                    selected = button;
                    break;
                }
            }

            m_view.ApplyNavigation();
            m_view.SetGridActive(true);
            m_view.SetCustomPanelVisible(false);
            m_view.SetStatus("");
            game.SetTargetSelection(true);
            m_view.Show();
            if (selected != null)
            {
                m_view.FocusButton(selected);
            }

            RectTransform guidedButton = null;
            for (int i = 0; i < m_timedTests.Length; i++)
            {
                var data = m_timedTests[i];
                if (data != null && OnboardingManager.IsShootingGuideActive &&
                    OnboardingManager.AllowsShootingGuideTest(_source, data.Id))
                {
                    var button = m_view.GetTimedTestButton(i);
                    if (button != null) guidedButton = button.transform as RectTransform;
                }
            }
            OnboardingManager.NotifyShootingSelectionOpened(_source, guidedButton);

            return true;
        }

        /// <summary>
        /// 선택창을 닫고 대상 표적과 게임 입력 차단 상태를 해제합니다.
        /// </summary>
        public void Close()
        {
            Close(false);
        }

        private void Close(bool startingTest)
        {
            if (!startingTest && m_bell != null && m_view != null && m_view.IsPanelSelfActive)
                OnboardingManager.NotifyShootingSelectionClosed(m_bell);
            if (m_view != null)
            {
                m_view.Hide();
                m_view.SetCustomPanelVisible(false);
            }

            m_bell = null;
            if (m_game != null)
            {
                m_game.SetTargetSelection(false);
            }
        }

        /// <summary>
        /// 표시 중 대상 종이 비활성화되면 기존처럼 선택창을 닫습니다.
        /// </summary>
        public void Tick()
        {
            if (m_view != null && m_view.IsPanelSelfActive && (m_bell == null || !m_bell.isActiveAndEnabled))
            {
                Close();
            }
        }

        private void OnCloseRequested()
        {
            Close();
        }

        private void OnTargetSelected(int _index)
        {
            if (m_bell == null || _index < 0 || _index >= m_targetPrefabs.Length)
            {
                return;
            }

            if (m_bell.Spawn(m_targetPrefabs[_index]))
            {
                Close();
            }
            else
            {
                m_view.SetStatus(m_bell.SpawnFailureReason ?? "소환할 수 없습니다.");
            }
        }

        private void OnTimedTestSelected(int _index)
        {
            if (m_bell == null || _index < 0 || _index >= m_timedTests.Length ||
                m_timedTests[_index] == null || !m_timedTests[_index].IsAvailable)
            {
                return;
            }

            TargetBell source = m_bell;
            FirstPersonPlayer player = GameManager.Instance != null && GameManager.Instance.UI != null
                ? GameManager.Instance.UI.Player
                : null;
            if (!OnboardingManager.CanPrepareShootingGuideTest(source, m_timedTests[_index].Id, player, out var guideError))
            {
                m_view.SetStatus(guideError);
                return;
            }
            if (!source.PrepareTest(m_timedTests[_index], player))
            {
                m_view.SetStatus(source.SpawnFailureReason);
                return;
            }

            Close(true);
            source.TestController.Begin();
        }

        private void OnCustomToggleRequested()
        {
            m_view.ToggleCustomPanel();
            m_view.SetStatus("");
        }

        private void OnSpawnCustomRequested()
        {
            if (m_bell == null)
            {
                return;
            }

            var values = new float[7];
            for (int i = 0; i < values.Length; i++)
            {
                if (!float.TryParse(m_view.GetCustomInputText(i), NumberStyles.Float,
                        CultureInfo.InvariantCulture, out values[i]) ||
                    float.IsNaN(values[i]) || float.IsInfinity(values[i]) || values[i] < 0f ||
                    (i < 3 && values[i] < 0.01f))
                {
                    m_view.SetStatus("크기는 0.01 이상, 저항은 0 이상의 숫자를 입력하세요.");
                    return;
                }
            }

            // 다른 사격장 표적과 같은 복셀 한도를 넘는 입력을 막는다.
            double cells = Math.Ceiling(values[0] / CUSTOM_PART_SIZE) *
                           Math.Ceiling(values[1] / CUSTOM_PART_SIZE) *
                           Math.Ceiling(values[2] / CUSTOM_PART_SIZE);
            if (cells > CUSTOM_MAX_PARTS)
            {
                m_view.SetStatus("크기가 너무 큽니다. 크기값을 줄여 주세요.");
                return;
            }

            if (m_bell.SpawnCustom(m_customTargetPrefab, new Vector3(values[0], values[2], values[1]),
                    new[] { values[3], values[4], values[5], values[6] }))
            {
                Close();
            }
            else
            {
                m_view.SetStatus(m_bell.SpawnFailureReason ?? "소환할 수 없습니다.");
            }
        }

        private void OnClearSpawnedRequested()
        {
            if (m_bell == null)
            {
                return;
            }

            m_bell.ClearSpawned();
            m_view.SetStatus("");
        }

        private void ValidateRequiredTests()
        {
            TableManager manager = TableManager.Instance;
            CustomerRequestData table = manager != null ? manager.CustomerRequest : null;
            foreach (CustomerRequestData.Request request in table?.requests ?? Array.Empty<CustomerRequestData.Request>())
            {
                foreach (string id in request?.requiredTestIds ?? Array.Empty<string>())
                {
                    if (!Array.Exists(m_timedTests, test => test != null && test.Id == id))
                    {
                        Debug.LogError("주문 필수 테스트가 선택창에 연결되지 않았습니다: " + id, m_view);
                    }
                }
            }
        }

        private void RegisterRowSounds()
        {
            for (int i = 0; i < m_targetPrefabs.Length; i++)
            {
                SoundManager.RegisterButtonSound(m_view.GetTargetButton(i), "SpawnButtonClick");
            }

            for (int i = 0; i < m_timedTests.Length; i++)
            {
                SoundManager.RegisterButtonSound(m_view.GetTimedTestButton(i), "SpawnButtonClick");
            }

            SoundManager.RegisterButtonSound(m_view.SpawnButton, "SpawnButtonClick");
        }

        private void AttachGame()
        {
            GameManager game = GameManager.Instance;
            if (m_game == game)
            {
                return;
            }

            DetachGame();
            m_game = game;
            if (m_game != null)
            {
                m_game.TargetSelectionCloseRequested += Close;
            }
        }

        private void DetachGame()
        {
            if (m_game != null)
            {
                m_game.TargetSelectionCloseRequested -= Close;
            }

            m_game = null;
        }
    }
}
