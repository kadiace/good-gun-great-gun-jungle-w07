using System;
using UnityEngine;

namespace WeaponWorkshop
{
    // 작업대 이벤트 식별자는 음원 파일명과 분리한다.
    internal enum WorkbenchSoundCue { Install, Remove, PlaceWeapon, AssemblyComplete, Paper, LimitReached }

    [DisallowMultipleComponent]
    [AddComponentMenu("Weapon Workshop/Workbench Sound Settings")]
    public sealed class WorkbenchSoundSettings : MonoBehaviour
    {
        [Serializable]
        private sealed class SoundEntry
        {
            [InspectorName("음원 목록"), Tooltip("한 개면 그대로 재생하고 여러 개면 무작위로 하나를 재생합니다. 빈 목록은 이 조건의 소리를 끕니다.")]
            public AudioClip[] clips = Array.Empty<AudioClip>();
            [Range(0f, 1f), InspectorName("음량")] public float volume = .65f;
        }

        // 조건마다 음원 목록과 기본 음량을 따로 관리한다. 빈 목록은 해당 소리를 끈다.
        [SerializeField, InspectorName("모듈 설치")] private SoundEntry installation = new SoundEntry { volume = .65f };
        [SerializeField, InspectorName("설치 추가음 (계산대)")] private SoundEntry installationExtra = new SoundEntry { volume = .65f };
        [SerializeField, InspectorName("모듈 제거")] private SoundEntry removal = new SoundEntry { volume = .65f };
        [SerializeField, InspectorName("작업대에 총 배치")] private SoundEntry weaponPlacement = new SoundEntry { volume = .8f };
        [SerializeField, InspectorName("조립 완료")] private SoundEntry assemblyComplete = new SoundEntry { volume = .8f };
        [SerializeField, InspectorName("설명서 펼치기 / 닫기")] private SoundEntry paper = new SoundEntry { volume = .8f };
        [SerializeField, InspectorName("커스텀 제한 오류")] private SoundEntry limitReached = new SoundEntry { volume = .85f };

        // 소리 선택이 게임의 보상/스폰 난수 순서에 영향을 주지 않게 별도 난수를 쓴다.
        private readonly System.Random clipRandom = new System.Random();

        // 이 컴포넌트는 선택과 설정만 담당하고 실제 AudioSource 재생은 SoundManager에 위임한다.
        internal void Play(SoundManager manager, WorkbenchSoundCue cue, Vector3 position)
        {
            if (!isActiveAndEnabled || manager == null) return;
            SoundEntry entry = cue switch
            {
                WorkbenchSoundCue.Install => installation,
                WorkbenchSoundCue.Remove => removal,
                WorkbenchSoundCue.PlaceWeapon => weaponPlacement,
                WorkbenchSoundCue.AssemblyComplete => assemblyComplete,
                WorkbenchSoundCue.Paper => paper,
                WorkbenchSoundCue.LimitReached => limitReached,
                _ => null
            };
            PlayEntry(manager, entry, position);
            if (cue == WorkbenchSoundCue.Install) PlayEntry(manager, installationExtra, position);
        }

        private void PlayEntry(SoundManager manager, SoundEntry entry, Vector3 position)
        {
            if (entry?.clips == null || entry.volume <= 0f) return;
            int count = 0;
            foreach (var clip in entry.clips) if (clip != null) count++;
            if (count == 0) return;
            // 여러 음원이 연결된 조건은 재생할 때마다 유효한 음원 하나를 무작위로 고른다.
            int selected = count == 1 ? 0 : clipRandom.Next(count);
            foreach (var clip in entry.clips)
            {
                if (clip == null) continue;
                if (selected-- != 0) continue;
                manager.PlayClip(clip, position, entry.volume);
                return;
            }
        }
    }
}
