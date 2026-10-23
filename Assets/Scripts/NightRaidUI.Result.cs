using System;

using UnityEngine;

namespace WeaponWorkshop
{
    public sealed partial class NightRaidUI
    {
        [SerializeField] private CanvasGroup resultBackdrop;
        [SerializeField] private CanvasGroup resultContent;
        [SerializeField] private TMPro.TextMeshProUGUI resultTitle;
        [SerializeField] private TMPro.TextMeshProUGUI resultBody;
        [SerializeField] private UnityEngine.UI.Button resultConfirm;
        [SerializeField] private ResultView m_resultView;
        private ResultPresenter m_resultPresenter;
        private bool m_hasLoggedResultReferenceError;

        private void InitializeResult()
        {
            if (m_resultPresenter != null)
            {
                return;
            }

            if (m_resultView == null)
            {
                if (!m_hasLoggedResultReferenceError)
                {
                    m_hasLoggedResultReferenceError = true;
                    Debug.LogError(
                        "[NightRaidUI] 결과 화면 초기화 실패: m_resultView 참조가 없습니다. " +
                        "ResultView 컴포넌트를 NightRaidUI 프리팹에 추가하고 연결하세요.");
                }

                return;
            }

            m_resultView.Configure(transform, resultBackdrop, resultContent, resultTitle, resultBody, resultConfirm);
            ResultPresenter presenter = new ResultPresenter(
                m_resultView,
                () => GameManager.Instance,
                HideResultExtraction);
            if (!presenter.Initialize())
            {
                presenter.Dispose();
                return;
            }

            m_resultPresenter = presenter;
        }

        private void DisposeResult()
        {
            m_resultPresenter?.Dispose();
            m_resultPresenter = null;
            if (m_resultView != null)
            {
                m_resultView.Dispose();
            }
        }

        private void HideResultExtraction()
        {
            SetExtraction(false, 0f, 0f);
        }

        public void ShowRaidResult(bool _died, string _summary, Action _confirmed)
        {
            Initialize();
            InitializeResult();
            if (m_resultPresenter == null)
            {
                return;
            }

            m_resultPresenter.ShowRaidResult(_died, _summary, _confirmed);
        }

        public void ShowBankruptcyResult(Action _confirmed)
        {
            Initialize();
            InitializeResult();
            if (m_resultPresenter == null)
            {
                return;
            }

            m_resultPresenter.ShowBankruptcyResult(_confirmed);
        }
    }
}
