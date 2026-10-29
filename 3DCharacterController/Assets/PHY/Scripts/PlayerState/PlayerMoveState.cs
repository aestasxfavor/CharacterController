using UnityEngine;
/// <summary>
/// 플레이어가 이동 입력을 받은 지상 상태
/// 낙하, 점프, 입력 해제를 확인해 다음 상태로 전환
/// </summary>
public class PlayerMoveState : PlayerBaseState
{
    public PlayerMoveState(PlayerController _playerController, PlayerStateMachine _playerStateMachine) : base(_playerController, _playerStateMachine)
    {
    }

    public override void Enter()
    {
        // Move 진입 시 공중 / 착지 관련 Animator 값 초기화 후 이동 입력 활성화
        playerController.PlayerAnimationController.SetAirborne(false);
        playerController.PlayerAnimationController.SetLanding(false);
        playerController.PlayerAnimationController.SetMoveInput(true);
        Debug.Log("move enter");
    }

    public override void Update()
    {
        // 일정 시간 이상 지면에 지지되지 않으면 FallState로 전환
        if (playerController.ShouldStartFalling)
        {
            playerStateMachine.ChangeState(new PlayerFallState(playerController, playerStateMachine, true));
            return;
        }

        // 이동 중에도 점프 입력, 접지 상태, 이동 가능한 경사 조건을 만족하면 JumpState로 전환
        bool canJump = playerController.InputHandler.JumpPressed &&
                       playerController.GroundDetector.IsGrounded &&
                       playerController.GroundDetector.IsWalkableSlope;

        if (canJump)
        {
            Debug.Log("move -> jump 전이");

            // 점프 시작 높이 기록 후 JumpController에서 수직 속도 처리
            playerController.SetJumpStartY(playerController.transform.position.y);
            playerController.JumpController.TryJump(true);

            // 점프 입력은 1회성 입력으로 사용한 뒤 초기화
            playerController.InputHandler.ResetJumpInput();

            playerStateMachine.ChangeState(new PlayerJumpState(playerController, playerStateMachine));
            return;
        }

        // 이동 입력이 사라지면 IdleState로 복귀
        if (playerController.InputHandler.Movement.sqrMagnitude <= 0.01f)
        {
            playerStateMachine.ChangeState(new PlayerIdleState(playerController, playerStateMachine));
            return;
        }

    }


    public override void Exit()
    {
        Debug.Log("move exit");
    }
}
