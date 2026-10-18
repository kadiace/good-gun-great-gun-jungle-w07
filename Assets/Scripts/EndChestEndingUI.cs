using UnityEngine;
using UnityEngine.Serialization;

using TMPro;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    [DisallowMultipleComponent]
    public sealed class EndChestEndingUI : MonoBehaviour
    {
        private const string DISCOVERY_TEXT = "지식의 요람을 찾았습니다!! 그리고 그 선물은...";

        [Header("안내 UI")]
        [FormerlySerializedAs("chest"), SerializeField] private EndChest _chest;
        [FormerlySerializedAs("whiteGroup"), SerializeField] private CanvasGroup _whiteGroup;
        [FormerlySerializedAs("endGroup"), SerializeField] private CanvasGroup _endGroup;
        [FormerlySerializedAs("buttonsGroup"), SerializeField] private CanvasGroup _buttonsGroup;
        [FormerlySerializedAs("continueButton"), SerializeField] private Button _findGiftButton;
        [SerializeField] private TMP_Text _discoveryText;

        private bool _submitting;

        private void Awake()
        {
            _submitting = false;
            if (_discoveryText != null) _discoveryText.text = DISCOVERY_TEXT;
            SetWhite(0f);
            SetEnd(0f);
            if (_findGiftButton != null) _findGiftButton.onClick.AddListener(FindGift);
        }

        private void OnDestroy()
        {
            if (_findGiftButton != null) _findGiftButton.onClick.RemoveListener(FindGift);
        }

        /// <summary>Sets the white overlay opacity during the chest transition.</summary>
        /// <param name="alpha">Requested opacity, clamped to the CanvasGroup range.</param>
        public void SetWhite(float alpha)
        {
            if (_whiteGroup != null) _whiteGroup.alpha = Mathf.Clamp01(alpha);
        }

        /// <summary>Fades the discovery message and enables its button when fully visible.</summary>
        /// <param name="alpha">Requested visibility, clamped to the CanvasGroup range.</param>
        public void SetEnd(float alpha)
        {
            alpha = Mathf.Clamp01(alpha);
            if (_endGroup != null) _endGroup.alpha = alpha;
            if (_buttonsGroup == null) return;
            _buttonsGroup.alpha = alpha;
            _buttonsGroup.interactable = _buttonsGroup.blocksRaycasts = alpha >= 1f && !_submitting;
        }

        /// <summary>Shows a recoverable save error and re-enables the Find Gift action.</summary>
        /// <param name="error">Reason the durable return could not be completed.</param>
        public void ShowSaveError(string error)
        {
            _submitting = false;
            if (_discoveryText != null)
            {
                _discoveryText.text = "저장하지 못했습니다. 다시 시도하세요.\n" + error;
                _discoveryText.fontSize = 34f;
            }
            if (_findGiftButton != null) _findGiftButton.interactable = true;
            if (_buttonsGroup != null) _buttonsGroup.interactable = _buttonsGroup.blocksRaycasts = true;
        }

        /// <summary>Submits one Find Gift action and restores the button if the durable return fails.</summary>
        private void FindGift()
        {
            if (_submitting || _chest == null) return;
            _submitting = true;
            if (_discoveryText != null)
            {
                _discoveryText.text = DISCOVERY_TEXT;
                _discoveryText.fontSize = 52f;
            }
            if (_findGiftButton != null) _findGiftButton.interactable = false;
            if (_buttonsGroup != null) _buttonsGroup.interactable = _buttonsGroup.blocksRaycasts = false;
            if (_chest.TryFindGift(out string error)) return;
            ShowSaveError(error);
        }
    }
}
