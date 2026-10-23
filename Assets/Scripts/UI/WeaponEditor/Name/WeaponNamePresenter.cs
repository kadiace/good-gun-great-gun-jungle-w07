using System;

using UnityEngine;

namespace WeaponWorkshop
{
    /// <summary>
    /// 무기 이름 화면의 저장 대상, 이름 정리·저장 버튼 정책, 튜토리얼 승인, 대화 소유와
    /// 기존 UIManager 알림을 담당하는 Presenter입니다. 프레임 진입은 브리지가 UpdateTarget으로 전달합니다.
    /// </summary>
    public sealed class WeaponNamePresenter : IDisposable
    {
        private const string DEFAULT_NAME = "나만의 총";
        private const string SAVE_MESSAGE = "무기 이름을 저장했습니다.";
        private const string TUTORIAL_ACTION = "rename";
        private const string WEAPON_RENAMED_SIGNAL = "weapon-renamed";
        private const int MAX_NAME_LENGTH = 40;

        private readonly WeaponNameView m_view;
        private readonly UIManager m_uiManager;

        private EquipableWeapon m_weapon;
        private bool m_ownsDialogue;
        private bool m_isInitialized;
        private bool m_hasLoggedDependencyError;

        public WeaponNamePresenter(WeaponNameView _view, UIManager _uiManager)
        {
            m_view = _view;
            m_uiManager = _uiManager;
        }

        /// <summary>
        /// View 초기화와 입력·저장 이벤트 구독을 연결합니다.
        /// 필수 의존성이 누락되면 오류를 기록하고 해당 화면 초기화만 실패로 처리합니다.
        /// </summary>
        public bool Initialize()
        {
            if (m_isInitialized)
            {
                return true;
            }

            if (m_view == null || m_uiManager == null)
            {
                if (!m_hasLoggedDependencyError)
                {
                    m_hasLoggedDependencyError = true;
                    Debug.LogError(
                        "[WeaponNamePresenter] Initialize 실패: 필수 의존성 누락 " +
                        $"(_view={m_view != null}, _uiManager={m_uiManager != null})");
                }

                return false;
            }

            if (!m_view.Initialize())
            {
                return false;
            }

            m_view.SaveRequested -= OnSaveRequested;
            m_view.SaveRequested += OnSaveRequested;
            m_view.NameChanged -= OnNameChanged;
            m_view.NameChanged += OnNameChanged;
            m_view.InputSelected -= OnInputSelected;
            m_view.InputSelected += OnInputSelected;
            m_view.InputDeselected -= OnInputDeselected;
            m_view.InputDeselected += OnInputDeselected;

            m_isInitialized = true;
            return true;
        }

        /// <summary>
        /// View 이벤트 구독과 대화 소유를 해제합니다. 중복 호출에 안전합니다.
        /// </summary>
        public void Dispose()
        {
            if (m_view != null)
            {
                m_view.SaveRequested -= OnSaveRequested;
                m_view.NameChanged -= OnNameChanged;
                m_view.InputSelected -= OnInputSelected;
                m_view.InputDeselected -= OnInputDeselected;
                m_view.Dispose();
            }

            ReleaseFocus();
            m_weapon = null;
            m_isInitialized = false;
        }

        /// <summary>
        /// 브리지가 해석한 현재 편집 무기를 반영합니다. 같은 대상이면 저장 버튼만 갱신하고,
        /// 대상이 바뀌면 포커스를 해제한 뒤 표시 이름을 새 대상으로 맞춥니다.
        /// </summary>
        public void UpdateTarget(EquipableWeapon _target)
        {
            if (!m_isInitialized && !Initialize())
            {
                return;
            }

            m_view.SetVisible(_target != null);
            if (m_weapon == _target)
            {
                RefreshSaveButton();
                return;
            }

            ReleaseFocus();
            m_weapon = _target;
            RefreshName();
        }

        /// <summary>
        /// 현재 무기의 확정 이름으로 입력칸을 되돌리고 저장 버튼 상태를 갱신합니다.
        /// </summary>
        public void RefreshName()
        {
            if (!m_isInitialized && !Initialize())
            {
                return;
            }

            m_view.SetName(m_weapon != null ? m_weapon.WeaponName : "");
            RefreshSaveButton();
        }

        /// <summary>
        /// 입력 중 취소 요청을 처리합니다. 확정 이름 복원·입력 해제·포커스 정리 후 true를 반환합니다.
        /// </summary>
        public bool CancelEditing()
        {
            if (!m_isInitialized && !Initialize())
            {
                return false;
            }

            if (!m_view.IsInputFocused)
            {
                return false;
            }

            RefreshName();
            m_view.DeactivateInput();
            m_view.ClearSelection();
            ReleaseFocus();
            return true;
        }

        /// <summary>
        /// 비활성화 시 기존 화면처럼 패널을 숨기고 대상·저장 버튼·대화 소유를 초기화합니다.
        /// </summary>
        public void HandleDisabled()
        {
            if (m_view != null)
            {
                m_view.SetVisible(false);
            }

            ReleaseFocus();
            m_weapon = null;

            if (m_view != null)
            {
                m_view.SetSaveInteractable(false);
            }
        }

        private string PendingName()
        {
            string value = (m_view.InputText ?? "").Trim().Replace("\n", " ").Replace("\r", " ");
            if (string.IsNullOrWhiteSpace(value))
            {
                return DEFAULT_NAME;
            }

            return value.Substring(0, Mathf.Min(MAX_NAME_LENGTH, value.Length));
        }

        private void RefreshSaveButton()
        {
            m_view.SetSaveInteractable(m_weapon != null && PendingName() != m_weapon.WeaponName);
        }

        private void OnNameChanged(string _value)
        {
            RefreshSaveButton();
        }

        private void OnSaveRequested()
        {
            if (!TutorialManager.TryAction(TUTORIAL_ACTION))
            {
                return;
            }

            if (m_weapon == null || PendingName() == m_weapon.WeaponName)
            {
                return;
            }

            m_weapon.SetWeaponName(m_view.InputText);
            RefreshName();
            ReleaseFocus();
            m_view.ClearSelection();
            m_uiManager.ShowNotification(SAVE_MESSAGE);
            if (TutorialManager.Instance != null)
            {
                TutorialManager.Instance.NotifySignal(WEAPON_RENAMED_SIGNAL);
            }
        }

        private void OnInputSelected(string _value)
        {
            if (!TutorialManager.TryAction(TUTORIAL_ACTION))
            {
                m_view.DeactivateInput();
                return;
            }

            // 입력칸 자신이 선택된 상태는 막힘으로 보지 않는다.
            if (GameManager.ModalBlocked || GameManager.Instance == null)
            {
                return;
            }

            m_ownsDialogue = true;
            GameManager.Instance.SetDialogue(true);
        }

        private void OnInputDeselected(string _value)
        {
            ReleaseFocus();
        }

        private void ReleaseFocus()
        {
            if (m_ownsDialogue && GameManager.Instance != null)
            {
                GameManager.Instance.SetDialogue(false);
            }

            m_ownsDialogue = false;
        }
    }
}
