using UnityEngine;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    // 저작된 검은 전환 가림막의 Canvas·Raycast·알파 표시만 담당한다.
    public sealed class RaidTransitionView : MonoBehaviour
    {
        private static GameObject s_prefab;

        [SerializeField] private Canvas m_canvas;
        [SerializeField] private CanvasGroup m_group;
        [SerializeField] private GraphicRaycaster m_raycaster;

        private bool m_isInitialized;
        private bool m_hasLoggedReferenceError;

        public static void RegisterPrefab(GameObject _prefab)
        {
            if (_prefab != null)
            {
                s_prefab = _prefab;
            }
        }

        // 씬 전환을 넘겨 살아남을 루트를 저작 프리팹에서 만든다. 문자열 경로 로더는 쓰지 않는다.
        public static GameObject InstantiateCover(string _name)
        {
            if (s_prefab == null)
            {
                return null;
            }

            GameObject root = Object.Instantiate(s_prefab);
            root.name = _name;
            return root;
        }

        private void Awake()
        {
            Initialize();
        }

        public bool Initialize()
        {
            if (m_isInitialized)
            {
                return true;
            }

            if (!ValidateReferences())
            {
                return false;
            }

            ApplyHiddenState();
            m_isInitialized = true;
            return true;
        }

        public void SetVisible(bool _visible)
        {
            if (!m_isInitialized && !Initialize())
            {
                return;
            }

            m_canvas.enabled = _visible;
            m_raycaster.enabled = _visible;
        }

        public void SetAlpha(float _alpha)
        {
            if (!m_isInitialized && !Initialize())
            {
                return;
            }

            m_group.alpha = Mathf.Clamp01(_alpha);
        }

        private void ApplyHiddenState()
        {
            m_group.alpha = 0f;
            m_group.blocksRaycasts = true;
            m_group.interactable = false;
            m_canvas.enabled = false;
            m_raycaster.enabled = false;
        }

        private bool ValidateReferences()
        {
            bool isValid = m_canvas != null && m_group != null && m_raycaster != null;
            if (!isValid && !m_hasLoggedReferenceError)
            {
                m_hasLoggedReferenceError = true;
                Debug.LogError(
                    $"[RaidTransitionView] 필수 참조 누락: canvas={m_canvas != null}, " +
                    $"group={m_group != null}, raycaster={m_raycaster != null}. " +
                    "Tools/UI/Authoring/Raid/Transition을 실행하세요.", this);
            }

            return isValid;
        }
    }
}
