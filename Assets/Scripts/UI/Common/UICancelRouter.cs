using UnityEngine;

namespace WeaponWorkshop
{
    // ESC 한 번의 화면별 취소 우선순위를 한 곳에서 결정한다.
    // GameManager는 입력 구독과 일시정지 상태 변경만 맡는다.
    public sealed class UICancelRouter
    {
        private readonly GameManager m_gameManager;
        private readonly UIManager m_uiManager;
        private readonly WorkshopSession m_workshopSession;
        private readonly System.Action m_togglePause;
        private readonly System.Action m_resume;
        private readonly System.Action m_manualClose;
        private readonly System.Action m_targetSelectionClose;

        private readonly RetireUI m_retireUI;
        private readonly TutorialSkipUI m_tutorialSkipUI;
        private readonly DayTransitionUI m_dayTransitionUI;
        private readonly SaveSlotsUI[] m_saveSlotsUIs;
        private readonly SoundSettingsUI[] m_soundSettingsUIs;
        private readonly ShootingTestHUD m_shootingTestHUD;
        private readonly WeaponRecipeUI m_recipeUI;
        private readonly PresetPanelUI m_presetPanelUI;
        private readonly WeaponCodeUI m_weaponCodeUI;
        private readonly WeaponNameUI m_weaponNameUI;
        private readonly ModulePaletteUI m_paletteUI;
        private readonly CubeBuilder m_cubeBuilder;

        private int m_lastCancelFrame = -1;

        public UICancelRouter(GameManager _gameManager, UIManager _uiManager, WorkshopSession _workshopSession,
            System.Action _togglePause, System.Action _resume, System.Action _manualClose,
            System.Action _targetSelectionClose)
        {
            m_gameManager = _gameManager;
            m_uiManager = _uiManager;
            m_workshopSession = _workshopSession;
            m_togglePause = _togglePause;
            m_resume = _resume;
            m_manualClose = _manualClose;
            m_targetSelectionClose = _targetSelectionClose;

            if (_uiManager == null)
            {
                return;
            }

            // 기존 GameManager가 쓰던 좁은 타입 탐색과 같은 범위만 사용한다.
            m_retireUI = _uiManager.Screen<RetireUI>();
            m_tutorialSkipUI = _uiManager.Screen<TutorialSkipUI>();
            m_dayTransitionUI = _uiManager.Screen<DayTransitionUI>();
            m_saveSlotsUIs = _uiManager.Screens<SaveSlotsUI>();
            m_soundSettingsUIs = _uiManager.Screens<SoundSettingsUI>();
            m_shootingTestHUD = _uiManager.Screen<ShootingTestHUD>();
            m_recipeUI = _uiManager.Screen<WeaponRecipeUI>();
            m_presetPanelUI = _uiManager.Screen<PresetPanelUI>();
            m_weaponCodeUI = _uiManager.Screen<WeaponCodeUI>();
            m_weaponNameUI = _uiManager.Screen<WeaponNameUI>();
            m_paletteUI = _uiManager.Screen<ModulePaletteUI>();
            FirstPersonPlayer player = _gameManager != null ? _gameManager.Player : GameManager.Instance?.Player;
            m_cubeBuilder = player != null ? player.GetComponentInChildren<CubeBuilder>(true) : null;
        }

        public bool RouteCancel()
        {
            if (m_gameManager == null)
            {
                return false;
            }

            // 같은 프레임에 중복 전달된 취소는 한 단계만 처리한다.
            if (m_lastCancelFrame == Time.frameCount)
            {
                return true;
            }
            m_lastCancelFrame = Time.frameCount;

            // 진입 커버 아래에서 열린 확인·선택과 날짜 전환은 먼저 소비한다.
            if (m_retireUI != null && m_retireUI.IsOpen)
            {
                return true;
            }
            if (m_tutorialSkipUI != null && m_tutorialSkipUI.IsOpen)
            {
                return true;
            }
            if (m_dayTransitionUI != null && m_dayTransitionUI.IsTransitioning)
            {
                return true;
            }

            // 일반 확인창은 취소하고, 대출 안내처럼 입력이 Suspend된 선택은 소비만 한다.
            if (m_uiManager != null && (m_uiManager.Screen<FeedbackUI>()?.IsChoiceOpen ?? false))
            {
                if (m_uiManager.Screen<FeedbackUI>() != null && !m_uiManager.Screen<FeedbackUI>().IsChoiceInputSuspended)
                {
                    m_uiManager.Screen<FeedbackUI>().Cancel();
                }
                return true;
            }

            // 일시정지 중에는 하위 저장·설정 화면만 닫고, 없으면 하위 화면을 건드리지 않고 재개한다.
            if (m_gameManager.IsPaused)
            {
                if (TryClosePausedChild())
                {
                    return true;
                }
                m_resume?.Invoke();
                return true;
            }

            // 야간 정산·지도·습격 결과 화면은 하위 화면을 닫거나 일시정지로 넘어가지 않는다.
            if (m_gameManager.IsNight)
            {
                return true;
            }

            // 기존 화면별 닫기 우선순위를 그대로 보존한다. 재시도 안내는 레시피보다 먼저 처리한다.
            if (!GameManager.ModalBlocked && m_shootingTestHUD != null && m_shootingTestHUD.CloseRetry())
            {
                return true;
            }
            if (m_recipeUI != null && m_recipeUI.IsOpen)
            {
                m_recipeUI.Close();
                return true;
            }
            if (m_gameManager.InManual)
            {
                m_manualClose?.Invoke();
                return true;
            }
            if (m_presetPanelUI != null && m_presetPanelUI.IsOpen)
            {
                m_presetPanelUI.Close();
                return true;
            }
            if (m_weaponCodeUI != null && m_weaponCodeUI.IsOpen)
            {
                m_weaponCodeUI.Close();
                return true;
            }
            if (m_weaponNameUI != null && m_weaponNameUI.CancelEditing())
            {
                return true;
            }
            if (m_gameManager.InTargetSelection)
            {
                m_targetSelectionClose?.Invoke();
                return true;
            }

            // D01: 슬롯 드래그·연속 배치를 먼저 취소하고, 그다음 목록·작업대 퇴장 순서로 처리한다.
            if (!GameManager.ModalBlocked)
            {
                if (m_paletteUI != null && m_paletteUI.TryCancelSlotDrag())
                {
                    return true;
                }
                if (m_cubeBuilder != null && m_cubeBuilder.TryCancelPlanarStroke())
                {
                    return true;
                }
                if (m_cubeBuilder != null && m_cubeBuilder.PaletteOpen)
                {
                    m_cubeBuilder.ClosePalette();
                    return true;
                }
                WorkshopSession session = ResolveSession();
                if (session != null && session.IsInWorkshop)
                {
                    if (!session.IsTransitioning)
                    {
                        session.Leave();
                    }
                    return true;
                }
            }

            m_togglePause?.Invoke();
            return true;
        }

        private bool TryClosePausedChild()
        {
            if (m_saveSlotsUIs != null)
            {
                for (int i = 0; i < m_saveSlotsUIs.Length; i++)
                {
                    if (m_saveSlotsUIs[i] != null && m_saveSlotsUIs[i].TryCloseForCancel())
                    {
                        return true;
                    }
                }
            }

            if (m_soundSettingsUIs != null)
            {
                for (int i = 0; i < m_soundSettingsUIs.Length; i++)
                {
                    if (m_soundSettingsUIs[i] != null && m_soundSettingsUIs[i].TryCloseForCancel())
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private WorkshopSession ResolveSession()
        {
            if (m_workshopSession != null)
            {
                return m_workshopSession;
            }

            FirstPersonPlayer player = m_gameManager != null ? m_gameManager.Player : null;
            return player != null ? player.GetComponent<WorkshopSession>() : null;
        }
    }
}
