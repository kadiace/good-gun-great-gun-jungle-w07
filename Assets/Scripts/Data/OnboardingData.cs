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

        [Serializable]
        public sealed class ShootingGuide
        {
            public string[] testIds = Array.Empty<string>();
            public string preferredTestId;
            public float minimumPenetration;
            public string introduction, prepareObjective, equipObjective, openObjective;
            public string hitCountIntroduction, hitCountPrepareObjective;
            public string selectObjective, fireObjective, passed, failed;
        }

        public string[] customRequestIds = Array.Empty<string>();
        public Module[] modules = Array.Empty<Module>();
        public CustomStep[] customSteps = Array.Empty<CustomStep>();
        public ShootingGuide shootingGuide = new ShootingGuide();
    }
}
