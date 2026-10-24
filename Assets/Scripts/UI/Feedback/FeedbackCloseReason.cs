namespace WeaponWorkshop
{
    // 확인창이 어떤 경로로 닫혔는지 전달한다. Forced는 확인·취소 콜백을 실행하지 않는다.
    public enum FeedbackCloseReason
    {
        Confirmed,
        Cancelled,
        Forced
    }
}
