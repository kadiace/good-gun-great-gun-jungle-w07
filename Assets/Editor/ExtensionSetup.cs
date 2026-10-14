using UnityEditor;

namespace WeaponWorkshop.Editor
{
    public static class ExtensionSetup
    {
        [MenuItem("Weapon Workshop/Connect Extension Module")]
        public static void SetUp() => CubeBuildingSetup.SetUp();
    }
}