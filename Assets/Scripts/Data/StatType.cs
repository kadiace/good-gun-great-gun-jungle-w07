namespace WeaponWorkshop
{
    // 공용 타입: 수정 전 팀 합의 필요. 직렬화 값은 바꾸지 말고 뒤에 추가한다.
    public enum StatType
    {
        Penetration = 0, // 관통력
        FireRate = 1,    // 연사력
        Explosion = 2,   // 폭발력
        Ricochet = 3,    // 반사 횟수
        Charge = 4,      // 차지
        Gravity = 5,     // 중력
        Heat = 6,        // 발열(화염 속성)
        Spin = 7,        // 회전
        Water = 8,       // 물(데미지 0)
        Homing = 9,      // 유도
        Shotgun = 10,    // 산탄
        Weight = 11,     // 무게
        Scope = 12,      // 스코프
        Speed = 13,      // 가속(탄 기본 속도 배율)
        Color = 14,      // 컬러(개수; 실제 색은 블록의 ColorModule)
        Spread = 15      // 탄퍼짐(도). 모듈 보너스로 더하며 음수면 정확해짐
    }
}
