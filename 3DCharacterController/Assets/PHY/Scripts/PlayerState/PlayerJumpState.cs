
using UnityEngine;

/// <summary>
/// 플레이어 점프 구간을 처리하는 상태
/// 제자리 점프는 IdleJumpTrigger로 실행한 단일 Jump 클립을 유지한다.
/// 이동 중 점프는 Run_Jump를 사용하고 실제 접지 시 지상 상태로 복귀한다.
/// </summary>
public class PlayerJumpState : PlayerBaseState
{
    // 제자리 점프 애니메이션 시작 후 실제 도약까지의 시간
    private const float PreJumpDuration = 0.15f;

    private readonly bool usePreJump;

    // 실제 점프 시작 여부
    private bool jumpStarted;
    private float preJumpTimer;

    // 점프 후 지면 이탈 및 착지 여부
    private bool hasLeftGround;
    private bool hasLanded;

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
        hasLanded = false;
        jumpStarted = false;
        preJumpTimer = 0f;

        // 이전 공중 및 착지 관련 Animator 값 초기화
        playerController.PlayerAnimationController.SetAirborne(false);
        playerController.PlayerAnimationController.SetLanding(false);

        if (usePreJump)
        {
            // 제자리 점프 애니메이션은 IdleState에서 Trigger로 실행
            // 이 상태에서는 준비 시간이 지난 후 실제 점프만 시작
            Debug.Log("제자리 점프 Trigger 호출");
            playerController.PlayerAnimationController.TriggerIdleJump();
            //playerController.PlayerAnimationController.SetMotion(PlayerMotion.Jump);
        }
        else
        {
            // 이동 중 점프는 준비 시간 없이 바로 시작
            StartJump();
        }
    }

    public override void Update()
    {
        // 제자리 점프의 도약 준비 시간 처리
        if (!jumpStarted)
        {
            if (playerController.ShouldStartFalling)
            {
                playerStateMachine.ChangeState(
                    new PlayerFallState(
                        playerController,
                        playerStateMachine,
                        false));
                return;
            }

            preJumpTimer += Time.deltaTime;

            if (preJumpTimer >= PreJumpDuration)
            {
                StartJump();
            }

            return;
        }

        bool isGrounded =
            playerController.GroundDetector.IsGrounded;

        // 점프 후 실제 지면 이탈 확인
        if (!isGrounded)
        {
            hasLeftGround = true;
        }

        // 지면을 벗어났다가 다시 접지한 경우 착지 완료
        if (hasLeftGround && isGrounded)
        {
            hasLanded = true;
        }

        // 실제 착지가 완료되지 않았다면 점프 상태 유지
        if (!hasLanded)
        {
            return;
        }

        // 제자리 점프는 단일 Jump 클립의 재생 완료까지 유지
        //if (usePreJump)
        //{
        //    if (!playerController.PlayerAnimationController.IsMotionComplete("Jump"))
        //    {
        //        return;
        //    }
        //}

        // 착지 후 이동 입력 여부에 따라 상태 복귀
        ChangeToIdleOrMove();
    }

    // 실제 수직 점프 처리
    private void StartJump()
    {
        jumpStarted = true;

        playerController.SetJumpStartY(
            playerController.transform.position.y);

        playerController.JumpController.TryJump(true);

        // 제자리 점프는 이미 IdleJumpTrigger로 실행됨
        // 이동 점프에서만 MotionState를 변경
        if (!usePreJump)
        {
            playerController.PlayerAnimationController.SetMotion(
                PlayerMotion.Run_Jump);
        }
    }

    // 이동 입력 여부에 따라 지상 상태 복귀
    private void ChangeToIdleOrMove()
    {
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

    }
}
