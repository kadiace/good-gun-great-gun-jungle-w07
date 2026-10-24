using UnityEngine;

namespace WeaponWorkshop
{
    // 컬러 모듈을 고르면 뜨는 색 선택 창의 브리지다. 화면 요소는 UIManager.prefab에 저작돼 있고,
    // 열림 여부·HSV 계산·확인/취소 전달은 ModuleColorPickerPresenter가 담당한다.
    // 확인 = CubeBuilder.ConfirmModuleColor, 취소 = CancelModuleColor. 열림 여부는 CubeBuilder.ColorPickerOpen을 따른다.
    public sealed class ColorPickerUI : MonoBehaviour
    {
        [SerializeField, Tooltip("비우면 씬에서 찾음(공방 카메라의 CubeBuilder).")]
        private CubeBuilder builder;
        [SerializeField] private TMPro.TMP_FontAsset font;
        [SerializeField] private ModuleColorPickerView view;

        private ModuleColorPickerPresenter presenter;

        private void Awake()
        {
            if (builder == null)
            {
                builder = FindAnyObjectByType<CubeBuilder>(FindObjectsInactive.Include);
            }

            if (font == null)
            {
                font = TMPro.TMP_Settings.defaultFontAsset;
            }

            if (view == null)
            {
                view = GetComponent<ModuleColorPickerView>();
            }

            if (view == null)
            {
                Debug.LogError("[ColorPickerUI] ModuleColorPickerView 참조가 없습니다. " +
                    "Tools/UI/Authoring/Customization/Author를 실행하세요.");
                enabled = false;
                return;
            }

            if (!view.Initialize())
            {
                Debug.LogError("[ColorPickerUI] ModuleColorPickerView 초기화에 실패했습니다.");
                enabled = false;
                return;
            }

            presenter = new ModuleColorPickerPresenter(view, builder);
            if (!presenter.Initialize())
            {
                Debug.LogError("[ColorPickerUI] ModuleColorPickerPresenter 초기화에 실패했습니다.");
                enabled = false;
            }
        }

        private void OnDisable()
        {
            view?.SetOpen(false);
        }

        private void OnDestroy()
        {
            presenter?.Dispose();
        }

        private void Update()
        {
            if (presenter == null)
            {
                return;
            }

            presenter.Bind(builder);
            presenter.Tick();
        }
    }
}
