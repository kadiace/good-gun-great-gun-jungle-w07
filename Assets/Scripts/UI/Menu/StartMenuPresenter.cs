using System;
using UnityEngine;

namespace WeaponWorkshop
{
    /// <summary>
    /// 인트로 시작 메뉴의 동작을 담당하는 Presenter입니다. 계속(최근 저장)·새 게임·슬롯 불러오기·시작 씬 선택과
    /// 중복 시작 차단, 빌드 씬 검사, 시작 로그를 StartMenuDomainAdapter를 통해 수행하고 표시는 StartMenuView에만 맡깁니다.
    /// 기존 Start_UI의 판정 순서·문구·로그 시점을 그대로 유지합니다.
    /// </summary>
    public sealed class StartMenuPresenter : IDisposable
    {
        private const string PRESENTER_NAME = "StartMenuPresenter";
        private const string MISSING_SAVE_MESSAGE = "불러올 수 있는 저장 파일이 없습니다.";

        private readonly StartMenuView m_view;
        private readonly StartMenuDomainAdapter m_domain;
        private readonly string m_gameSceneName;

        private bool m_isInitialized;
        private bool m_isLoading;
        private bool m_hasLoggedDependencyError;

        public bool IsInitialized
        {
            get
            {
                return m_isInitialized;
            }
        }

        public bool IsLoading
        {
            get
            {
                return m_isLoading;
            }
        }

        public StartMenuPresenter(StartMenuView _view, StartMenuDomainAdapter _domain, string _gameSceneName)
        {
            m_view = _view;
            m_domain = _domain;
            m_gameSceneName = _gameSceneName;
        }

        /// <summary>
        /// View 초기화(메뉴 패널 표시 상태 포함)와 계속 버튼 노출, 기본 선택 포커스를 적용합니다.
        /// </summary>
        public bool Initialize()
        {
            if (m_isInitialized)
            {
                return true;
            }

            if (m_view == null || m_domain == null)
            {
                if (!m_hasLoggedDependencyError)
                {
                    m_hasLoggedDependencyError = true;
                    Debug.LogError(
                        $"[{PRESENTER_NAME}] Initialize 실패: 필수 의존성 누락 " +
                        $"(_view={m_view != null}, _domain={m_domain != null})");
                }

                return false;
            }

            if (!m_view.Initialize())
            {
                return false;
            }

            // View 초기화 성공 직후 초기화 플래그를 먼저 세운다. 뒤따르는 RefreshSaveAvailability가
            // 미초기화 가드에서 Initialize로 다시 진입해 무한 재귀로 번지는 것을 막는다.
            m_isInitialized = true;
            RefreshSaveAvailability();
            m_view.ApplyDefaultSelection();
            return true;
        }

        /// <summary>구독한 것이 없어 상태만 정리합니다. 중복 호출에 안전합니다.</summary>
        public void Dispose()
        {
            m_isInitialized = false;
        }

        /// <summary>최근 저장 존재 여부로 계속 버튼 노출을 갱신합니다.</summary>
        public void RefreshSaveAvailability()
        {
            if (!m_isInitialized && !Initialize())
            {
                return;
            }

            m_view.SetContinueAvailable(m_domain.LatestSlot() >= 0);
        }

        /// <summary>계속(이어하기). 최근 저장 슬롯을 찾고 없으면 기존과 같이 정리·알림으로 끝납니다.</summary>
        public void ContinueGame()
        {
            if (!m_isInitialized && !Initialize())
            {
                return;
            }

            if (m_isLoading)
            {
                return;
            }

            int slot = m_domain.LatestSlot();
            if (slot < 0)
            {
                m_domain.ClearPendingLoad();
                RefreshSaveAvailability();
                m_domain.NotifyMissingSave(MISSING_SAVE_MESSAGE);
                return;
            }

            LoadSlot(slot);
        }

        /// <summary>저장 슬롯 패널을 엽니다. 기존과 같이 시작 중 가드 없이 그대로 엽니다.</summary>
        public void OpenLoadPanel()
        {
            if (!m_isInitialized && !Initialize())
            {
                return;
            }

            m_domain.OpenLoadPanel();
        }

        /// <summary>선택한 슬롯의 저장을 읽어 예약하고 저장된 씬(없으면 기본 씬)으로 시작합니다.</summary>
        public void LoadSlot(int _slot)
        {
            if (!m_isInitialized && !Initialize())
            {
                return;
            }

            if (m_isLoading)
            {
                return;
            }

            if (!m_domain.TryReadSlot(_slot, out GameSaveData data))
            {
                return;
            }

            m_domain.PendingLoad = data;
            m_domain.AdoptSaveId(data.saveId);
            BeginGame(string.IsNullOrEmpty(data.sceneName) ? m_gameSceneName : data.sceneName);
        }

        /// <summary>새 게임. 예약된 불러오기와 습격 복구를 지우고 새 saveId로 시작합니다.</summary>
        public void StartNewGame()
        {
            if (!m_isInitialized && !Initialize())
            {
                return;
            }

            if (m_isLoading)
            {
                return;
            }

            m_domain.ClearPendingLoad();
            m_domain.ClearRaidRecovery();
            m_domain.BeginNewSave();
            BeginGame(null);
        }

        private void BeginGame(string _savedScene)
        {
            if (m_isLoading)
            {
                return;
            }

            string sceneToLoad = string.IsNullOrEmpty(_savedScene) ? m_gameSceneName : _savedScene;
            if (!m_domain.CanLoadScene(sceneToLoad))
            {
                m_domain.ClearPendingLoad();
                m_domain.LogError($"시작할 씬을 빌드 목록에 등록하세요: {m_gameSceneName}");
                return;
            }

            m_isLoading = true;
            bool newGame = string.IsNullOrEmpty(_savedScene);
            string message = newGame ? "새 게임 시작" : $"저장 게임 시작({_savedScene})";
            m_domain.LogGameStart(message, new GameStartLog
            {
                installId = m_domain.InstallId,
                saveId = m_domain.SaveId,
                savedScene = _savedScene ?? "",
                newGame = newGame,
                retired = m_domain.IsPendingLoadRetired,
                endChestOpened = m_domain.IsPendingLoadEndChestOpened
            });
            m_domain.LoadScene(sceneToLoad);
        }
    }
}
