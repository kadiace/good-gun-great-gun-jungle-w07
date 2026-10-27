namespace WeaponWorkshop.EditorTools
{
    /// <summary>
    /// UI 저작 명령이 공유하는 프리팹 경로. UIManager.prefab은 레이어 아래 화면 묶음을 중첩 프리팹으로 갖는다.
    /// </summary>
    public static class UIAuthoringPaths
    {
        public const string UI_MANAGER_PREFAB = "Assets/Prefabs/Managers/UIManager.prefab";
        /// <summary>습격 씬용 UIManager 변형. 공방 편집·사격장 화면을 뺀다.</summary>
        public const string UI_MANAGER_RAID_PREFAB = "Assets/Prefabs/Managers/UIManager_Raid.prefab";
        public const string START_UI_PREFAB = "Assets/Prefabs/UI/StartUI_Manager.prefab";

        public const string HUD_FOLDER = "Assets/Prefabs/UI/Hud";
        public const string SCREENS_FOLDER = "Assets/Prefabs/UI/Screens";
        public const string POPUPS_FOLDER = "Assets/Prefabs/UI/Popups";
        public const string TRANSITIONS_FOLDER = "Assets/Prefabs/UI/Transitions";
        public const string WORKBENCH_FOLDER = "Assets/Prefabs/UI/Workbench";

        public const string HUD_LAYER = "HudLayer";
        public const string SCREEN_LAYER = "ScreenLayer";
        public const string POPUP_LAYER = "PopupLayer";
        public const string TRANSITION_LAYER = "TransitionLayer";

        /// <summary>레이어 이름에 맞는 중첩 프리팹 폴더를 돌려준다.</summary>
        public static string FolderForLayer(string _layerName)
        {
            switch (_layerName)
            {
                case HUD_LAYER:
                    return HUD_FOLDER;
                case SCREEN_LAYER:
                    return SCREENS_FOLDER;
                case POPUP_LAYER:
                    return POPUPS_FOLDER;
                case TRANSITION_LAYER:
                    return TRANSITIONS_FOLDER;
                default:
                    return null;
            }
        }

        /// <summary>레이어 바로 아래 화면 묶음의 중첩 프리팹 경로를 돌려준다.</summary>
        public static string ScreenPrefab(string _layerName, string _screenName)
        {
            // 폴더는 기능 소속, 레이어는 그리기 순서다. 작업대 전용 화면은 함께 보관한다.
            if ((_layerName == HUD_LAYER && (_screenName == "PresetPanelScreen" || _screenName == "WeaponNameScreen")) ||
                (_layerName == POPUP_LAYER && _screenName == "PresetChoicePanel"))
            {
                return WORKBENCH_FOLDER + "/" + _screenName + ".prefab";
            }
            string folder = FolderForLayer(_layerName);
            return folder != null ? folder + "/" + _screenName + ".prefab" : null;
        }
    }
}
