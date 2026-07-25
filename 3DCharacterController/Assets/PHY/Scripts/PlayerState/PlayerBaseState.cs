using UnityEngine;
/// <summary>
/// 플레이어 상태들의 공통 기반 클래스
/// 각 상태가 PlayerController와 PlayerStateMachine 참조를 공유함
/// Enter, Update, Exit 흐름을 통해 상태별 동작을 구현함
/// </summary>
public abstract class PlayerBaseState
{
    protected PlayerController playerController;
    protected PlayerStateMachine playerStateMachine;

    protected PlayerBaseState(PlayerController _playerController, PlayerStateMachine _playerStateMachine)
    {
        playerController = _playerController;
        playerStateMachine = _playerStateMachine;
    }

    /// <summary>
    /// 현재 상태에서 매 프레임 실행할 동작
    /// </summary>
    public abstract void Update();

    /// <summary>
    /// 상태 진입 시 실행할 동작
    /// </summary>
    public virtual void Enter() { }

    /// <summary>
    /// 상태 종료시 실행할 동작
    /// </summary>
    public virtual void Exit() { }
}
