using System;

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace WeaponWorkshop
{
    /// <summary>
    /// 커서 잠금·표시, Time.timeScale, 게임 액션 맵의 활성/비활성 정책을 단일화하여 관리하는 정책 클래스입니다.
    /// 개별 UI 화면과 도메인 흐름은 Time.timeScale이나 Cursor를 직접 쓰지 않고 이 정책 클래스를 통해 요청합니다.
    /// </summary>
    public static class UIInputPolicy
    {
        private static float m_savedTimeScale = 1f;
        private static bool m_isLobby = false;
        private static bool m_isPaused = false;
        private static bool m_isNight = false;
        private static bool m_inDialogue = false;
        private static bool m_isDialogueInputBlocked = false;
        private static bool m_inTargetSelection = false;
        private static bool m_inManual = false;
        private static bool m_isChoiceOpen = false;
        private static bool m_isChoiceSuspended = false;
        private static bool m_isDayTransitioning = false;
        private static bool m_isInWorkshop = false;
        private static bool m_isWorkshopEditing = false;

        private static InputActionMap m_gameMap;
        private static InputActionMap m_workshopMap;
        private static InputActionMap m_buildMap;
        private static InputActionMap m_dialogueMap;
        private static InputActionMap m_choiceMap;

        public static bool IsLobby
        {
            get
            {
                return m_isLobby;
            }
        }

        public static bool IsPaused
        {
            get
            {
                return m_isPaused;
            }
        }

        public static bool IsNight
        {
            get
            {
                return m_isNight;
            }
        }

        public static bool InDialogue
        {
            get
            {
                return m_inDialogue;
            }
        }

        public static bool InTargetSelection
        {
            get
            {
                return m_inTargetSelection;
            }
        }

        public static bool InManual
        {
            get
            {
                return m_inManual;
            }
        }

        public static bool IsChoiceOpen
        {
            get
            {
                return ResolveChoiceOpen();
            }
        }

        public static bool IsChoiceSuspended
        {
            get
            {
                return ResolveChoiceSuspended();
            }
        }

        public static bool IsInWorkshop
        {
            get
            {
                return m_isInWorkshop;
            }
        }

        public static bool IsWorkshopEditing
        {
            get
            {
                return m_isWorkshopEditing;
            }
        }

        public static float SavedTimeScale
        {
            get
            {
                return m_savedTimeScale;
            }
        }

        /// <summary>
        /// 습격 진입 오버레이 또는 날짜 전환 등 전역 전환 가림막 차단 여부입니다.
        /// </summary>
        public static bool IsOverlayBlocked
        {
            get
            {
                return NightRaidEntryOverlay.InputBlocked || m_isDayTransitioning;
            }
        }

        /// <summary>
        /// 창·대화·일시정지·야간 등 화면 상태에 따른 게임 조작 차단 여부입니다.
        /// </summary>
        public static bool IsModalBlocked
        {
            get
            {
                return m_isPaused ||
                       m_inDialogue ||
                       m_isNight ||
                       m_inTargetSelection ||
                       m_inManual ||
                       ResolveChoiceOpen();
            }
        }

        /// <summary>
        /// 텍스트 입력 필드(TMP_InputField)에 포커스가 있는지 여부입니다.
        /// </summary>
        public static bool IsTextInputFocused
        {
            get
            {
                if (EventSystem.current == null)
                {
                    return false;
                }

                GameObject selected = EventSystem.current.currentSelectedGameObject;
                if (selected == null)
                {
                    return false;
                }

                return selected.TryGetComponent<TMPro.TMP_InputField>(out var field) && field.isFocused;
            }
        }

        /// <summary>
        /// 전역 게임플레이 입력 차단 여부입니다. 진입 오버레이, 모달 창, 텍스트 포커스 중 하나라도 활성이면 차단됩니다.
        /// </summary>
        public static bool IsGameplayBlocked
        {
            get
            {
                return IsOverlayBlocked || IsModalBlocked || IsTextInputFocused;
            }
        }

        /// <summary>
        /// 인트로 및 로비 진입 시 정책을 적용합니다. 시간 배율 1, 커서 표시/잠금 해제, 게임플레이 모달을 초기화합니다.
        /// </summary>
        public static void ApplyLobbyPolicy()
        {
            m_isLobby = true;
            m_isPaused = false;
            m_isNight = false;
            m_inDialogue = false;
            m_isDialogueInputBlocked = false;
            m_inTargetSelection = false;
            m_inManual = false;
            m_isChoiceOpen = false;
            m_isChoiceSuspended = false;
            m_isDayTransitioning = false;
            m_isInWorkshop = false;
            m_isWorkshopEditing = false;
            m_savedTimeScale = 1f;

            Time.timeScale = 1f;

            if (Cursor.lockState != CursorLockMode.None)
            {
                Cursor.lockState = CursorLockMode.None;
            }

            if (!Cursor.visible)
            {
                Cursor.visible = true;
            }

        }

        /// <summary>
        /// 게임플레이 시작 또는 재시작 시 정책을 기본 상태로 초기화합니다.
        /// </summary>
        public static void ResetToGameplay()
        {
            m_isLobby = false;
            m_isPaused = false;
            m_isNight = false;
            m_inDialogue = false;
            m_isDialogueInputBlocked = false;
            m_inTargetSelection = false;
            m_inManual = false;
            m_isChoiceOpen = false;
            m_isChoiceSuspended = false;
            m_isDayTransitioning = false;
            m_isInWorkshop = false;
            m_isWorkshopEditing = false;
            m_savedTimeScale = 1f;

            Time.timeScale = 1f;
            ApplyPolicy();
        }

        /// <summary>
        /// 일시정지 상태를 설정하고 정책을 적용합니다. 진입 전 유효한 시간 배율을 기억합니다.
        /// </summary>
        public static void SetPaused(bool _paused)
        {
            if (m_isPaused == _paused)
            {
                return;
            }

            if (_paused)
            {
                if (!m_isNight && Time.timeScale > 0f)
                {
                    m_savedTimeScale = Time.timeScale;
                }
            }

            m_isPaused = _paused;
            ApplyTimeScalePolicy();
            ApplyPolicy();
        }

        /// <summary>
        /// 야간 상태를 설정하고 정책을 적용합니다. 야간에는 시간 배율 0을 유지합니다.
        /// </summary>
        public static void SetNight(bool _night)
        {
            m_isNight = _night;
            m_isPaused = false;
            if (!_night)
            {
                // 기존 동작: 야간 해제 시 시간 배율을 1로 되돌린다.
                m_savedTimeScale = 1f;
            }

            ApplyTimeScalePolicy();
            ApplyPolicy();
        }

        /// <summary>
        /// 대화 화면 열림/닫힘 상태를 설정하고 정책을 적용합니다.
        /// </summary>
        public static void SetDialogue(bool _open)
        {
            m_inDialogue = _open;
            ApplyPolicy();
        }

        /// <summary>
        /// 상위 UI(일시정지 등)로 인한 대화 입력 차단 상태를 설정합니다.
        /// </summary>
        public static void SetDialogueInputBlocked(bool _blocked)
        {
            m_isDialogueInputBlocked = _blocked;
            ApplyPolicy();
        }

        /// <summary>
        /// 타깃 선택 화면 상태를 설정하고 정책을 적용합니다.
        /// </summary>
        public static void SetTargetSelection(bool _open)
        {
            m_inTargetSelection = _open;
            ApplyPolicy();
        }

        /// <summary>
        /// 매뉴얼 화면 상태를 설정하고 정책을 적용합니다.
        /// </summary>
        public static void SetManual(bool _open)
        {
            m_inManual = _open;
            ApplyPolicy();
        }

        /// <summary>
        /// 확인/선택 팝업 열림 및 입력 중단(Suspend) 상태를 설정합니다.
        /// </summary>
        public static void SetChoiceOpen(bool _open, bool _suspended = false)
        {
            m_isChoiceOpen = _open;
            m_isChoiceSuspended = _suspended;
            ApplyPolicy();
        }

        /// <summary>
        /// 확인/선택 팝업의 입력 중단(Suspend) 상태만 갱신합니다.
        /// </summary>
        public static void SetChoiceSuspended(bool _suspended)
        {
            m_isChoiceSuspended = _suspended;
            ApplyPolicy();
        }

        /// <summary>
        /// 날짜 전환 페이드/연출 상태를 설정하고 정책을 적용합니다.
        /// </summary>
        public static void SetDayTransitioning(bool _transitioning)
        {
            m_isDayTransitioning = _transitioning;
            ApplyPolicy();
        }

        /// <summary>
        /// 작업대 진입/퇴장 상태를 설정하고 정책을 적용합니다.
        /// </summary>
        public static void SetInWorkshop(bool _inWorkshop)
        {
            m_isInWorkshop = _inWorkshop;
            ApplyPolicy();
        }

        /// <summary>
        /// 작업대 내부 편집(큐브 설치/삭제/회전) 모드 활성 상태를 설정하고 정책을 적용합니다.
        /// </summary>
        public static void SetWorkshopEditing(bool _isEditing)
        {
            m_isWorkshopEditing = _isEditing;
            ApplyPolicy();
        }

        /// <summary>
        /// 현재 상태 조합을 바탕으로 Time.timeScale, 커서, 액션 맵 전체 정책을 한 번에 계산하고 적용합니다.
        /// </summary>
        // 시간 배율은 일시정지·야간·초기화 전환 때만 바꾼다. 대화·매뉴얼 등 다른 화면 열림이 시간 배율을 덮어쓰지 않게 한다.
        public static void ApplyPolicy()
        {
            ApplyCursorPolicy();
            ApplyActionMapPolicy();
        }

        /// <summary>
        /// 현재 정책에 따라 커서 잠금 상태와 표시 여부만 다시 적용합니다.
        /// </summary>
        public static void RefreshCursor()
        {
            ApplyCursorPolicy();
        }

        /// <summary>
        /// Game 액션 맵(ESC 일시정지)을 등록합니다.
        /// </summary>
        public static void RegisterGameMap(InputActionMap _map)
        {
            m_gameMap = _map;
            ApplyPolicy();
        }

        /// <summary>
        /// Game 액션 맵 등록을 해제합니다.
        /// </summary>
        public static void UnregisterGameMap(InputActionMap _map)
        {
            if (m_gameMap == _map)
            {
                m_gameMap = null;
            }
        }

        /// <summary>
        /// Workshop 및 Build(편집) 액션 맵을 등록합니다.
        /// </summary>
        public static void RegisterWorkshopMaps(InputActionMap _workshopMap, InputActionMap _buildMap)
        {
            m_workshopMap = _workshopMap;
            m_buildMap = _buildMap;
            ApplyPolicy();
        }

        /// <summary>
        /// Workshop 및 Build 액션 맵 등록을 해제합니다.
        /// </summary>
        public static void UnregisterWorkshopMaps(InputActionMap _workshopMap, InputActionMap _buildMap)
        {
            if (m_workshopMap == _workshopMap)
            {
                m_workshopMap = null;
            }

            if (m_buildMap == _buildMap)
            {
                m_buildMap = null;
            }
        }

        /// <summary>
        /// Dialogue 액션 맵(대화 진행)을 등록합니다.
        /// </summary>
        public static void RegisterDialogueMap(InputActionMap _map)
        {
            m_dialogueMap = _map;
            ApplyPolicy();
        }

        /// <summary>
        /// Dialogue 액션 맵 등록을 해제합니다.
        /// </summary>
        public static void UnregisterDialogueMap(InputActionMap _map)
        {
            if (m_dialogueMap == _map)
            {
                m_dialogueMap = null;
            }
        }

        /// <summary>
        /// Choice 액션 맵(확인창 조작)을 등록합니다.
        /// </summary>
        public static void RegisterChoiceMap(InputActionMap _map)
        {
            m_choiceMap = _map;
            ApplyPolicy();
        }

        /// <summary>
        /// Choice 액션 맵 등록을 해제합니다.
        /// </summary>
        public static void UnregisterChoiceMap(InputActionMap _map)
        {
            if (m_choiceMap == _map)
            {
                m_choiceMap = null;
            }
        }

        /// <summary>
        /// 지정한 액션 맵의 활성 상태를 요청합니다.
        /// </summary>
        public static void RequestActionMapEnabled(InputActionMap _map, bool _enable)
        {
            if (_map == null)
            {
                return;
            }

            if (_enable && !_map.enabled)
            {
                _map.Enable();
            }
            else if (!_enable && _map.enabled)
            {
                _map.Disable();
            }
        }

        private static void ApplyTimeScalePolicy()
        {
            if (m_isLobby)
            {
                Time.timeScale = 1f;
                return;
            }

            if (m_isNight)
            {
                Time.timeScale = 0f;
                return;
            }

            if (m_isPaused)
            {
                Time.timeScale = 0f;
                return;
            }

            Time.timeScale = m_savedTimeScale > 0f ? m_savedTimeScale : 1f;
        }

        private static void ApplyCursorPolicy()
        {
            if (m_isLobby)
            {
                if (Cursor.lockState != CursorLockMode.None)
                {
                    Cursor.lockState = CursorLockMode.None;
                }

                if (!Cursor.visible)
                {
                    Cursor.visible = true;
                }

                return;
            }

            bool shouldShowCursor = IsGameplayBlocked || m_isInWorkshop;
            CursorLockMode desiredLock = shouldShowCursor ? CursorLockMode.None : CursorLockMode.Locked;
            bool desiredVisible = shouldShowCursor;

            if (Cursor.lockState != desiredLock)
            {
                Cursor.lockState = desiredLock;
            }

            if (Cursor.visible != desiredVisible)
            {
                Cursor.visible = desiredVisible;
            }
        }

        private static void ApplyActionMapPolicy()
        {
            if (m_isLobby)
            {
                return;
            }

            // Game 맵: 게임플레이 및 일시정지 중 항상 활성화(ESC 일시정지/재개 수신)
            if (m_gameMap != null && !m_gameMap.enabled)
            {
                m_gameMap.Enable();
            }

            // Workshop 맵: 게임플레이 중 활성화
            if (m_workshopMap != null && !m_workshopMap.enabled)
            {
                m_workshopMap.Enable();
            }

            // Build·Dialogue·Choice 맵은 기존처럼 각 화면(WorkshopSession.SetEditing, DialogueView, FeedbackChoiceView)이 직접 켜고 끈다.
            // 일시정지 위 이탈 확인창(D03)·대출 안내 Suspend·팝업 닫힘 뒤 편집 입력 복귀를 정책이 덮어쓰지 않게 한다.
        }

        // 기존 GameManager.ModalBlocked처럼 실제 UI 상태만 읽는다. 별도 플래그가 닫힘 경로에서 남아 조작이 영구 차단되는 일을 막는다.
        private static bool ResolveChoiceOpen()
        {
            if (GameManager.Instance != null && GameManager.Instance.UI != null)
            {
                return GameManager.Instance.UI.IsChoiceOpen;
            }

            return false;
        }

        private static bool ResolveChoiceSuspended()
        {
            if (GameManager.Instance != null && GameManager.Instance.UI != null && GameManager.Instance.UI.Feedback != null)
            {
                return GameManager.Instance.UI.Feedback.IsChoiceInputSuspended;
            }

            return false;
        }

        private static void DisableMapIfActive(InputActionMap _map)
        {
            if (_map != null && _map.enabled)
            {
                _map.Disable();
            }
        }
    }
}
