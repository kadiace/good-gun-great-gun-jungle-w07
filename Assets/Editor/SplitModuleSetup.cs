using UnityEditor;

namespace WeaponWorkshop.Editor
{
    public static class SplitModuleSetup
    {
        [MenuItem("Weapon Workshop/Connect Split Modules")]
        public static void SetUp() => CubeBuildingSetup.SetUp();
    }
}