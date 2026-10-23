using UnityEngine;

namespace WeaponWorkshop
{
    // 탄색 모듈 색 선택 창의 열림 상태와 HSV 값을 소유한다.
    // 색 결정의 원본은 CubeBuilder이며, Presenter는 확인·취소 요청만 전달한다.
    public sealed class ModuleColorPickerPresenter
    {
        private readonly ModuleColorPickerView m_view;

        private CubeBuilder m_builder;
        private bool m_isInitialized;
        private float m_hue;
        private float m_saturation = 1f;
        private float m_value = 1f;

        public ModuleColorPickerPresenter(ModuleColorPickerView _view, CubeBuilder _builder)
        {
            m_view = _view;
            m_builder = _builder;
        }

        // View 이벤트를 구독한다. 중복 호출에 안전하다.
        public bool Initialize()
        {
            if (m_isInitialized)
            {
                return true;
            }

            if (m_view == null)
            {
                Debug.LogError("[ModuleColorPickerPresenter] Initialize 실패: View 참조가 비어 있습니다.");
                return false;
            }

            m_view.SquarePicked += OnSquarePicked;
            m_view.HuePicked += OnHuePicked;
            m_view.ConfirmRequested += OnConfirm;
            m_view.CancelRequested += OnCancel;
            m_isInitialized = true;
            return true;
        }

        public void Dispose()
        {
            if (!m_isInitialized)
            {
                return;
            }

            m_view.SquarePicked -= OnSquarePicked;
            m_view.HuePicked -= OnHuePicked;
            m_view.ConfirmRequested -= OnConfirm;
            m_view.CancelRequested -= OnCancel;
            m_isInitialized = false;
        }

        public void Bind(CubeBuilder _builder)
        {
            if (_builder != null)
            {
                m_builder = _builder;
            }
        }

        // 열림 여부는 CubeBuilder.ColorPickerOpen을 따른다. 열릴 때 현재 모듈 색에서 HSV를 다시 읽는다.
        public void Tick()
        {
            if (!m_isInitialized || m_builder == null)
            {
                return;
            }

            bool open = m_builder.ColorPickerOpen;
            if (open == m_view.IsOpen)
            {
                return;
            }

            if (open)
            {
                Color.RGBToHSV(m_builder.ModuleColor, out m_hue, out m_saturation, out m_value);
                m_view.Render(m_hue, m_saturation, m_value);
            }

            m_view.SetOpen(open);
        }

        private void OnSquarePicked(Vector2 _point)
        {
            m_saturation = _point.x;
            m_value = _point.y;
            m_view.Render(m_hue, m_saturation, m_value);
        }

        private void OnHuePicked(Vector2 _point)
        {
            m_hue = Mathf.Min(_point.y, 0.9999f);
            m_view.Render(m_hue, m_saturation, m_value);
        }

        private void OnConfirm()
        {
            m_builder?.ConfirmModuleColor(Color.HSVToRGB(m_hue, m_saturation, m_value));
        }

        private void OnCancel()
        {
            m_builder?.CancelModuleColor();
        }
    }
}
