
using UnityEngine;

/// <summary>
/// 플레이어 점프 구간을 처리하는 상태
/// 제자리 점프는 Jump → InAir → Land를 유지한다.
/// 이동 중 점프는 Run_Jump를 유지하고 실제 접지 시 지상 상태로 복귀한다.
/// </summary>
public class PlayerJumpState : PlayerBaseState
{
    // 웅크리기 시작해서 실제로 땅을 박차기까지의 시간
    private const float PreJumpDuration = 0.15f;

    private readonly bool usePreJump;

    // 실제로 점프를 시작했는지 여부
    private bool jumpStarted;
    private float preJumpTimer;

    // 점프 후 지면을 벗어났는지 여부
    private bool hasLeftGround;

    public PlayerJumpState(
        PlayerController _playerController,
        PlayerStateMachine _playerStateMachine,
        bool _usePreJump)
        : base(_playerController, _playerStateMachine)
    {
        usePreJump = _usePreJump;
    }

    public override void Enter()
    {
        hasLeftGround = false;
        jumpStarted = false;
        preJumpTimer = 0f;

        if (usePreJump)
        {
            playerController.PlayerAnimationController.SetMotion(PlayerMotion.Jump);
        }
        else
        {
            StartJump();
        }
    }

    public override void Update()
    {
        // 웅크리는 동안 처리
        if (!jumpStarted)
        {
            if (playerController.ShouldStartFalling)
            {
                playerStateMachine.ChangeState(
                    new PlayerFallState(playerController, playerStateMachine, false));
                return;
            }

            preJumpTimer += Time.deltaTime;

            if (preJumpTimer >= PreJumpDuration)
            {
                StartJump();
            }

            return;
        }

        bool isGrounded = playerController.GroundDetector.IsGrounded;
        float ySpeed = playerController.JumpController.VerticalVelocity;

        // 실제 지면 이탈 여부 확인
        if (!isGrounded)
        {
            hasLeftGround = true;
        }

        // 이동 중 점프는 공중에서 Run_Jump 유지
        if (!usePreJump && hasLeftGround && !isGrounded)
        {
            return;
        }

        // 제자리 점프는 정점에서 FallState로 전환
        if (hasLeftGround && !isGrounded && ySpeed <= 0f)
        {
            playerStateMachine.ChangeState(
                new PlayerFallState(playerController, playerStateMachine, true));
            return;
        }

        // 실제 착지 시 지상 상태로 복귀
        if (hasLeftGround && isGrounded)
        {
            if (playerController.InputHandler.Movement.sqrMagnitude > 0.01f)
            {
                playerStateMachine.ChangeState(
                    new PlayerMoveState(playerController, playerStateMachine));
            }
            else
            {
                playerStateMachine.ChangeState(
                    new PlayerIdleState(playerController, playerStateMachine));
            }

            return;
        }
    }

    // 수직 속도 적용 및 점프 모션 선택
    private void StartJump()
    {
        jumpStarted = true;

        playerController.SetJumpStartY(
            playerController.transform.position.y);

        playerController.JumpController.TryJump(true);

        playerController.PlayerAnimationController.SetMotion(
            usePreJump ? PlayerMotion.Jump : PlayerMotion.Run_Jump
        );
    }

    public override void Exit()
    {

    }
}
