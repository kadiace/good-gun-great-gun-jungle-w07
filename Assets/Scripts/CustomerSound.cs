using System;
using UnityEngine;

namespace WeaponWorkshop
{
    [DisallowMultipleComponent]
    public sealed class CustomerSound : MonoBehaviour
    {
        private const string RICOCHET_PROFILE_ID = "e8726eef5e281a241935acf0d3110b60";
        private const string LAKE_PARK_PROFILE_ID = "f2b056418a5de7349942f08879971a6f";
        private const string CHRIST_PROFILE_ID = "038a4f4f9f79c204a9087f7ce969d7f5";
        private const string ROBBER_PROFILE_ID = "f88c397ed1f539f4a961604044e93feb";
        private const string RICS_MONKEY_PROFILE_ID = "bb35f40bd3aa17e4f831ca8946978d4b";
        private const string ROBBER_AGAIN_PROFILE_ID = "4dbebd0100c6bc24db1270af6d2e8600";

        private static readonly System.Random _choiceRandom = new System.Random();

        [SerializeField, Header("공통 비명"), InspectorName("공통 비명 음원 목록")] private AudioClip[] commonScreams = Array.Empty<AudioClip>();
        [SerializeField, Range(0f, 1f), InspectorName("공통 비명 음량")] private float commonVolume = 1f;
        [SerializeField, Header("man 그룹 비명"), InspectorName("man 그룹 음원 목록")] private AudioClip[] _manScreams = Array.Empty<AudioClip>();
        [SerializeField, Range(0f, 1f), InspectorName("man 그룹 음량")] private float _manVolume = 1f;
        [SerializeField, Header("D2_7_Ricochet 1"), InspectorName("비명 클립")] private AudioClip _d2_7Ricochet1Clip;
        [SerializeField, Range(0f, 1f), InspectorName("음량")] private float _d2_7Ricochet1Volume = 1f;
        [SerializeField, Header("D3_5_LakePark"), InspectorName("비명 클립")] private AudioClip _d3_5LakeParkClip;
        [SerializeField, Range(0f, 1f), InspectorName("음량")] private float _d3_5LakeParkVolume = 1f;
        [SerializeField, Header("D3_9_christ"), InspectorName("비명 클립")] private AudioClip _d3_9ChristClip;
        [SerializeField, Range(0f, 1f), InspectorName("음량")] private float _d3_9ChristVolume = 1f;
        [SerializeField, Header("D4_3_Robber"), InspectorName("비명 클립")] private AudioClip _d4_3RobberClip;
        [SerializeField, Range(0f, 1f), InspectorName("음량")] private float _d4_3RobberVolume = 1f;
        [SerializeField, Header("D5_11_RicsMonkey"), InspectorName("비명 클립")] private AudioClip _d5_11RicsMonkeyClip;
        [SerializeField, Range(0f, 1f), InspectorName("음량")] private float _d5_11RicsMonkeyVolume = 1f;
        [SerializeField, Header("D6_7_RobberAgain"), InspectorName("비명 클립")] private AudioClip _d6_7RobberAgainClip;
        [SerializeField, Range(0f, 1f), InspectorName("음량")] private float _d6_7RobberAgainVolume = 1f;

        private SoundManager soundManager;
        private AudioClip _lastCommonClip;
        private AudioClip _lastManClip;

        private void OnEnable()
        {
            soundManager = GetComponentInParent<SoundManager>();
            Customer.ScreamRequested += OnCustomerScreamRequested;
        }

        private void OnDisable() => Customer.ScreamRequested -= OnCustomerScreamRequested;

        /// <summary>보호 피격 또는 사망 비명 요청을 profileId 분류에 따라 적절한 음원으로 재생한다.</summary>
        private void OnCustomerScreamRequested(string profileId, Vector3 position)
        {
            if (soundManager == null || !soundManager.IsActiveInstance) return;
            CustomerSoundData table = TableManager.Instance.CustomerSound;
            CustomerSoundData.FixedGuest fixedGuest = table.FindFixedGuest(profileId);
            if (fixedGuest != null)
            {
                if (fixedGuest.classification == "common") PlayCommon(position);
                else if (fixedGuest.classification == CustomerSoundInfo.CLASSIFICATION_MAN) PlayMan(position);
                else if (fixedGuest.classification == "individual") PlayIndividual(profileId, position);
                return;
            }

            // 조합 생성 손님은 런타임 profileId가 비어 있다. 명시된 풀 정책에서만 공통 비명을 낸다.
            if (string.IsNullOrEmpty(profileId) && table.generatedGuests != null && table.generatedGuests.commonScreamEnabled)
                PlayCommon(position);
        }

        /// <summary>공통 손님에게 공통 클립을 재생하며 직전 공통 클립의 연속 재생을 피한다.</summary>
        private void PlayCommon(Vector3 position)
        {
            PlayRandomGroupClip(commonScreams, position, commonVolume, ref _lastCommonClip);
        }

        /// <summary>man 분류 손님에게 전용 그룹 클립을 재생하며 직전 man 클립의 연속 재생을 피한다.</summary>
        private void PlayMan(Vector3 position)
        {
            PlayRandomGroupClip(_manScreams, position, _manVolume, ref _lastManClip);
        }

        /// <summary>배열의 유효 슬롯 중 직전 클립과 다른 슬롯을 균등 추첨하고, 대안이 없으면 반복을 허용해 재생한다.</summary>
        private void PlayRandomGroupClip(AudioClip[] clips, Vector3 position, float volume, ref AudioClip lastClip)
        {
            AudioClip[] availableClips = clips ?? Array.Empty<AudioClip>();
            int availableCount = 0;
            int withoutLastCount = 0;
            foreach (AudioClip clip in availableClips)
            {
                if (clip == null) continue;
                availableCount++;
                if (clip != lastClip) withoutLastCount++;
            }

            if (availableCount == 0) return;

            bool avoidLastClip = withoutLastCount > 0;
            int selectionCount = avoidLastClip ? withoutLastCount : availableCount;
            int selectedIndex;
            lock (_choiceRandom) selectedIndex = _choiceRandom.Next(selectionCount);

            foreach (AudioClip clip in availableClips)
            {
                if (clip == null || (avoidLastClip && clip == lastClip)) continue;
                if (selectedIndex-- != 0) continue;

                lastClip = clip;
                soundManager.PlayClip(clip, position, volume);
                return;
            }
        }

        /// <summary>profileId와 일치하는 개별 손님의 단일 클립을 직접 재생하고 클립이 없으면 무음으로 둔다.</summary>
        private void PlayIndividual(string profileId, Vector3 position)
        {
            AudioClip clip = null;
            float volume = 1f;
            if (profileId == RICOCHET_PROFILE_ID)
            {
                clip = _d2_7Ricochet1Clip;
                volume = _d2_7Ricochet1Volume;
            }
            else if (profileId == LAKE_PARK_PROFILE_ID)
            {
                clip = _d3_5LakeParkClip;
                volume = _d3_5LakeParkVolume;
            }
            else if (profileId == CHRIST_PROFILE_ID)
            {
                clip = _d3_9ChristClip;
                volume = _d3_9ChristVolume;
            }
            else if (profileId == ROBBER_PROFILE_ID)
            {
                clip = _d4_3RobberClip;
                volume = _d4_3RobberVolume;
            }
            else if (profileId == RICS_MONKEY_PROFILE_ID)
            {
                clip = _d5_11RicsMonkeyClip;
                volume = _d5_11RicsMonkeyVolume;
            }
            else if (profileId == ROBBER_AGAIN_PROFILE_ID)
            {
                clip = _d6_7RobberAgainClip;
                volume = _d6_7RobberAgainVolume;
            }

            if (clip != null) soundManager.PlayClip(clip, position, volume);
        }

        private void OnValidate()
        {
            commonVolume = Mathf.Clamp01(commonVolume);
            _manVolume = Mathf.Clamp01(_manVolume);
            _d2_7Ricochet1Volume = Mathf.Clamp01(_d2_7Ricochet1Volume);
            _d3_5LakeParkVolume = Mathf.Clamp01(_d3_5LakeParkVolume);
            _d3_9ChristVolume = Mathf.Clamp01(_d3_9ChristVolume);
            _d4_3RobberVolume = Mathf.Clamp01(_d4_3RobberVolume);
            _d5_11RicsMonkeyVolume = Mathf.Clamp01(_d5_11RicsMonkeyVolume);
            _d6_7RobberAgainVolume = Mathf.Clamp01(_d6_7RobberAgainVolume);
        }
    }
}
