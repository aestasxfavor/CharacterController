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
        playerController.PlayerAnimationController.SetFalling(false);
        playerController.PlayerAnimationController.SetLanding(false);
        playerController.PlayerAnimationController.SetMoveInput(true);
        Debug.Log("move enter");
    }

    public override void Update()
    {
        // 이동 중 접지가 끊기고 하강 속도 조건을 만족하면 FallState로 전환
        if (ShouldFall())
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

    /// <summary>
    /// 이전 프레임에는 지상에 있었지만 현재 접지가 끊긴 상황인지 확인
    /// 점프 중이 아닌 상태에서 하강 속도가 기준 이하일 때 낙하로 판단
    /// </summary>
    private bool ShouldFall()
    {
        return playerController.WasGrounded
            && !playerController.GroundDetector.IsGrounded
            && !playerController.JumpController.IsJumping
            && playerController.JumpController.VerticalVelocity <= playerController.FallThreshold;
    }

    public override void Exit()
    {
        Debug.Log("move exit");
    }
}
