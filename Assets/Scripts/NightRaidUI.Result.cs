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
        [SerializeField] private ResultModuleRowView m_resultModuleRow;
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

        // 생존 결과에는 이번 습격에서 얻은 모듈을 아이콘 줄로 보여 주고, 아이콘에 포인터를 올리면 모듈 상세를 띄운다.
        public void ShowRaidResult(bool _died, string _summary, System.Collections.Generic.IReadOnlyList<int> _modules, Action _confirmed)
        {
            ShowRaidResult(_died, _summary, () =>
            {
                if (m_resultModuleRow != null)
                {
                    m_resultModuleRow.Hide();
                }

                _confirmed?.Invoke();
            });
            if (m_resultPresenter == null || m_resultModuleRow == null)
            {
                return;
            }

            WorkshopTagsUI icons = GetComponentInParent<WorkshopTagsUI>(true);
            if (icons == null)
            {
                icons = transform.root.GetComponentInChildren<WorkshopTagsUI>(true);
            }

            HoveringUI tooltip = GetComponentInParent<HoveringUI>(true);
            if (tooltip == null)
            {
                tooltip = transform.root.GetComponentInChildren<HoveringUI>(true);
            }

            m_resultModuleRow.Show(
                _died ? null : _modules,
                _id => icons != null ? icons.GetRaidModuleIcon(_id) : null,
                _id => moduleCatalog != null && moduleCatalog.TryGet(_id, out ModuleCatalog.Entry entry) ? entry.data : null,
                tooltip);
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
