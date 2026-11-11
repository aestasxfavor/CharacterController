
using UnityEngine;

/// <summary>
/// 높은 이동 점프 후 구르기 착지를 관리하는 FSM 상태
/// 하강 관성을 유지하고 실제 착지 후 정해진 재생 시간이 지나면 지상 상태로 복귀
/// </summary>
public class PlayerRollingState : PlayerBaseState
{
    private float rollingTimer;
    private bool hasTouchedGround;

    public PlayerRollingState(
        PlayerController _playerController,
        PlayerStateMachine _playerStateMachine)
        : base(_playerController, _playerStateMachine)
    {
    }

    public override void Enter()
    {
        rollingTimer = 0f;
        hasTouchedGround = false;

        Debug.Log("Rolling Enter");

        // 구르기 도중 현재 수평 속도를 유지하고 새 이동 입력을 차단
        playerController.MovementController.SetFallMovementLocked(true);
        playerController.PlayerAnimationController.SetAirborne(false);
        playerController.PlayerAnimationController.SetLanding(false);
        playerController.PlayerAnimationController.SetMotion(PlayerMotion.FallingToRoll);
    }

    public override void Update()
    {
        rollingTimer += Time.deltaTime;

        // CharacterController가 실제 바닥과 충돌한 이후에만 복귀 허용
        if (playerController.ControllerGroundedAfterMove)
        {
            hasTouchedGround = true;
        }

        if (!hasTouchedGround || rollingTimer < playerController.RollingDuration)
        {
            return;
        }

        // 구르기가 끝나면 현재 입력에 따라 기존 지상 상태로 복귀
        if (playerController.InputHandler.Movement.sqrMagnitude > 0.01f)
        {
            playerStateMachine.ChangeState(
                new PlayerMoveState(
                    playerController,
                    playerStateMachine));
        }
        else
        {
            playerStateMachine.ChangeState(
                new PlayerIdleState(
                    playerController,
                    playerStateMachine));
        }
    }

    public override void Exit()
    {
        // 구르기 종료 후 현재 이동 입력을 다시 반영
        playerController.MovementController.SetFallMovementLocked(false);
        Debug.Log("Lolling Exit");
    }
}
