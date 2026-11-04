
using UnityEngine;

/// <summary>
/// 플레이어가 이동 입력이 없는 지상 상태
/// 낙하, 점프, 이동 입력을 확인해 다음 상태로 전환
/// </summary>
public class PlayerIdleState : PlayerBaseState
{
    public PlayerIdleState(
        PlayerController _playerController,
        PlayerStateMachine _playerStateMachine)
        : base(_playerController, _playerStateMachine) { }

    public override void Enter()
    {
        // Idle 진입 시 공중 / 착지 관련 Animator 값 초기화
        playerController.PlayerAnimationController.SetAirborne(false);
        playerController.PlayerAnimationController.SetLanding(false);
        playerController.PlayerAnimationController.SetMoveInput(false);
        playerController.PlayerAnimationController.SetMotion(PlayerMotion.Locomotion);

        Debug.Log("idle enter");
    }

    public override void Update()
    {
        // 일정 시간 이상 지면에 지지되지 않으면 FallState로 전환
        if (playerController.ShouldStartFalling)
        {
            playerStateMachine.ChangeState(
                new PlayerFallState(playerController, playerStateMachine, false));
            return;
        }

        // 점프 입력, 접지 상태, 이동 가능한 경사 조건 확인
        bool canJump =
            playerController.InputHandler.JumpPressed &&
            playerController.GroundDetector.IsGrounded &&
            playerController.GroundDetector.IsWalkableSlope;

        if (canJump)
        {
            playerController.InputHandler.ResetJumpInput();

            // 이동 입력 유무에 따라 점프 모션 분기
            bool hasMoveInput =
                playerController.InputHandler.Movement.sqrMagnitude > 0.01f;

            playerStateMachine.ChangeState(
                new PlayerJumpState(
                    playerController,
                    playerStateMachine,
                    !hasMoveInput));

            return;
        }

        // 이동 입력 시 MoveState로 전환
        if (playerController.InputHandler.Movement.sqrMagnitude > 0.01f)
        {
            playerStateMachine.ChangeState(
                new PlayerMoveState(playerController, playerStateMachine));
            return;
        }
    }

    public override void Exit()
    {
        Debug.Log("idle exit");
    }
}
