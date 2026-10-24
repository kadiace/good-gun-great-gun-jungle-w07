using System;

namespace WeaponWorkshop
{
    [Serializable]
    public sealed class OnboardingData
    {
        [Serializable]
        public sealed class Module
        {
            public int moduleId;
            public string title;
            public string description;
        }

        [Serializable]
        public sealed class CustomStep
        {
            public string id;
            public int order;
            public string title;
            public string description;
            public string targetPath;
        }

        public string[] customRequestIds = Array.Empty<string>();
        public Module[] modules = Array.Empty<Module>();
        public CustomStep[] customSteps = Array.Empty<CustomStep>();
    }
}
