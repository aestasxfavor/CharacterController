/// <summary>
/// 파쿠르 동작(Vault, Mantle, Slide, WallRun)의 공통 부모.
/// PlayerController는 구체 동작이 아니라 이 타입만 보고 파쿠르 중인지 판단한다.
/// </summary>
public abstract class PlayerParkourState : PlayerBaseState
{
    protected PlayerParkourState(PlayerController _playerController, PlayerStateMachine _playerStateMachine) 
        : base(_playerController, _playerStateMachine)
    {
    }

    // 시작 위치/방향을 맞추는 정렬 구간인지 (정렬 가드를 넣을 때 쓸 자리)
    public virtual bool IsAligning => false;
}