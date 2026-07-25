using UnityEngine;
/// <summary>
/// 플레이어의 현재 상태를 보관하고 상태 전환 흐름을 관리하는 상태 머신
/// 각 상태의 전환 조건 판단은 State에서 처리함
/// StateMachine은 Exit → Enter 순서의 전환 처리만 담당
/// </summary>
public class PlayerStateMachine : MonoBehaviour
{
    /// <summary>
    /// 현재 실행 중인 플레이어 상태
    /// </summary>
    public PlayerBaseState CurrentState { get; private set; }

    /// <summary>
    /// 현재 상태를 종료하고 새 상태로 전환
    /// 같은 상태로의 반복 전환은 방지
    /// </summary>
    public void ChangeState(PlayerBaseState newState)
    {
        if (newState == null) return;

        // 같은 상태로 반복 전환되는 것을 방지
        // Enter / Exit가 매 프레임 다시 호출되는 문제를 막기 위한 방어 코드
        if (CurrentState != null && CurrentState.GetType() == newState.GetType())
        {
            return;
        }

        Debug.Log($"ChangeState: {CurrentState?.GetType().Name} -> {newState.GetType().Name}");

        CurrentState?.Exit();
        CurrentState = newState;
        CurrentState?.Enter();
    }

    /// <summary>
    /// 현재 상태의 Update 흐름 실행
    /// </summary>
    public void UpdateState()
    {
        CurrentState?.Update();
    }
}
