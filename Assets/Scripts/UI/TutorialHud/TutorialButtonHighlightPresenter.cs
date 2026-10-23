using System;
using System.Collections.Generic;

using UnityEngine;

namespace WeaponWorkshop
{
    /// <summary>
    /// 현재 튜토리얼 단계·열린 화면·편집 상태를 조회해 강조할 버튼을 고르고 TutorialButtonHighlightView에 전달하는 Presenter입니다.
    /// 커버 복제·그래픽 복사는 View가 맡습니다.
    /// </summary>
    public sealed class TutorialButtonHighlightPresenter : IDisposable
    {
        private const string PRESENTER_NAME = "TutorialButtonHighlightPresenter";

        private readonly TutorialButtonHighlightView m_view;
        private readonly TutorialSkipUI m_selection;
        private readonly DialogueUI m_dialogue;
        private readonly FeedbackUI m_feedback;
        private readonly WeaponRecipeUI m_recipe;
        private readonly PresetPanelUI m_presets;
        private readonly NightRaidUI m_raid;

        private readonly List<RectTransform> m_targets = new List<RectTransform>();

        private bool m_isInitialized;
        private bool m_hasLoggedDependencyError;

        public TutorialButtonHighlightPresenter(TutorialButtonHighlightView _view, TutorialSkipUI _selection,
            DialogueUI _dialogue, FeedbackUI _feedback, WeaponRecipeUI _recipe, PresetPanelUI _presets,
            NightRaidUI _raid)
        {
            m_view = _view;
            m_selection = _selection;
            m_dialogue = _dialogue;
            m_feedback = _feedback;
            m_recipe = _recipe;
            m_presets = _presets;
            m_raid = _raid;
        }

        public bool IsInitialized
        {
            get
            {
                return m_isInitialized;
            }
        }

        /// <summary>
        /// View를 초기화합니다. 필수 의존성이 없으면 화면만 초기화 실패로 남깁니다.
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
                    Debug.LogError("[" + PRESENTER_NAME + "] Initialize 실패: TutorialButtonHighlightView 참조가 없습니다.");
                }

                return false;
            }

            if (!m_view.Initialize())
            {
                return false;
            }

            m_isInitialized = true;
            return true;
        }

        /// <summary>
        /// View 표시 참조를 해제합니다. 중복 호출에 안전합니다.
        /// </summary>
        public void Dispose()
        {
            if (m_view != null)
            {
                m_view.Dispose();
            }

            m_isInitialized = false;
        }

        public void OnDisabled()
        {
            if (m_view != null)
            {
                m_view.HideAll();
            }
        }

        /// <summary>
        /// 기존 LateUpdate와 같은 우선순위로 강조 대상을 고른 뒤 View에 표시를 요청합니다.
        /// </summary>
        public void Tick()
        {
            if (!m_isInitialized && !Initialize())
            {
                return;
            }

            m_targets.Clear();
            TutorialManager manager = TutorialManager.Instance;
            TutorialStep step = manager != null ? manager.CurrentStep : null;
            if (manager != null && manager.IsTutorialActive)
            {
                if (m_feedback != null && m_feedback.IsChoiceOpen && !m_feedback.IsChoiceInputSuspended &&
                    step != null && step.buttonGuide == TutorialButtonGuide.LoanConfirm)
                {
                    Add(m_feedback.TutorialConfirmButton);
                }
                else if ((m_selection != null && m_selection.IsOpen) || (m_dialogue != null && m_dialogue.IsOpen) ||
                    (m_feedback != null && m_feedback.IsChoiceOpen))
                {
                    // 일반 선택과 확인 대화는 안내 대상 버튼이 아니다.
                }
                else if (m_presets != null && m_presets.IsOpen && step != null &&
                    step.buttonGuide == TutorialButtonGuide.PresetFirst)
                {
                    Add(m_presets.TutorialSaveButton);
                }
                else if (step != null)
                {
                    if (step.buttonGuide == TutorialButtonGuide.Recipe && manager.IsEditingTutorialWorkbench &&
                        !GameManager.GameplayBlocked && m_recipe != null && !m_recipe.IsOpen)
                    {
                        Add(m_recipe != null ? m_recipe.TutorialButton : null);
                    }

                    if (step.buttonGuide == TutorialButtonGuide.PresetFirst && manager.IsEditingTutorialWorkbench &&
                        !GameManager.GameplayBlocked)
                    {
                        Add(m_presets != null ? m_presets.TutorialFirstSlot : null);
                    }

                    if (step.buttonGuide == TutorialButtonGuide.RaidHouse)
                    {
                        Add(m_raid != null ? m_raid.TutorialHouseButton : null);
                    }
                }
            }

            m_view.ShowTargets(m_targets);
        }

        private void Add(RectTransform _target)
        {
            if (_target != null && _target.gameObject.activeInHierarchy && !m_targets.Contains(_target))
            {
                m_targets.Add(_target);
            }
        }
    }
}
