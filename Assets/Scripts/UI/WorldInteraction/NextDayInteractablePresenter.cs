using System;
using TMPro;
using UnityEngine;

namespace WeaponWorkshop
{
    /// <summary>
    /// NextDayInteractable의 상태 라벨 표시와 야간 습격 스킵 확인 다이얼로그 요청을 전담하는 Presenter입니다.
    /// </summary>
    public sealed class NextDayInteractablePresenter
    {
        public const string SKIP_NIGHT_RAID_MESSAGE = "야간 습격을 하지 않고 다음 날로 넘어가시겠습니까?";
        public const string CONFIRM_TEXT = "예";
        public const string CANCEL_TEXT = "아니오";
        public const string TITLE_OPEN_SHOP = "영업시작";
        public const string TITLE_NEXT_DAY = "다음날로";
        public const string STATUS_OPEN = "OPEN";
        public const string STATUS_CLOSE = "CLOSE";

        public void UpdateLabels(
            TMP_Text _statusLabel,
            InteractionLabel _interactionLabel,
            bool _closed,
            bool _hasStatusPanel,
            bool _isAwaitingOpening)
        {
            if (_statusLabel != null)
            {
                _statusLabel.text = _hasStatusPanel
                    ? STATUS_CLOSE
                    : (_closed ? STATUS_CLOSE : STATUS_OPEN);
            }

            if (_interactionLabel != null)
            {
                _interactionLabel.SetTitle(_isAwaitingOpening ? TITLE_OPEN_SHOP : TITLE_NEXT_DAY);
            }
        }

        public bool RequestSkipNightRaid(
            Component _owner,
            DayTransitionUI _transition,
            Action _onConfirm)
        {
            if (_transition == null)
            {
                return false;
            }

            var feedback = FeedbackUI.For(_owner);
            if (feedback == null)
            {
                return false;
            }

            return feedback.ShowChoice(
                _owner,
                SKIP_NIGHT_RAID_MESSAGE,
                _onConfirm,
                null,
                CONFIRM_TEXT,
                CANCEL_TEXT) == true;
        }

        public void CancelChoice(Component _owner)
        {
            FeedbackUI.For(_owner)?.CancelChoice(_owner);
        }
    }
}
