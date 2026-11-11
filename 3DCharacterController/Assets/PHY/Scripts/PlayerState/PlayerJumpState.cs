
using UnityEngine;

/// <summary>
/// 플레이어 점프 구간을 처리하는 상태
/// 제자리 점프는 IdleJumpTrigger로 실행한 단일 Jump 클립을 사용
/// 이동 중 점프는 Run_Jump를 사용하고 실제 접지 시 지상 상태로 복귀
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

    // 점프 출발 발판 높이와 구르기 전환 여부
    private float rollStartGroundY;
    private bool hasRollStartGround;
    private bool rollingTriggered;

    // 실제 플레이 환경에 맞춰 조정할 임시 기준값
    private const float MinRollHeightDifference = 2f;
    private const float RollStartGroundDistance = 0.75f;

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
        rollingTriggered = false;

        // 점프 출발 발판의 실제 지면 높이를 저장
        GroundDetector startGround = playerController.GroundDetector;

        hasRollStartGround =
            !usePreJump &&
            startGround.HasGroundHit &&
            startGround.IsWalkableSlope;

        rollStartGroundY = hasRollStartGround
            ? startGround.GroundHeightY
            : 0f;

        // 이전 공중 및 착지 관련 Animator 값 초기화
        playerController.PlayerAnimationController.SetAirborne(false);
        playerController.PlayerAnimationController.SetLanding(false);

        if (usePreJump)
        {
            // 제자리 점프는 Trigger로 단일 Jump 클립 실행
            // 준비 시간이 지난 후 실제 점프 시작
            Debug.Log("제자리 점프 Trigger 호출");

            playerController.PlayerAnimationController.TriggerIdleJump();
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

        // 이동 점프에서만 발판 높이 차이와 착지 예정 지면을 검사
        if (!usePreJump && !rollingTriggered && TryEnterRolling())
        {
            return;
        }

        // 실제 착지가 완료되지 않았다면 점프 상태 유지
        if (!hasLanded)
        {
            return;
        }

        // 제자리 점프의 클립 완료 검사는 기존 주석 상태 유지
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

        // 기존 점프 호출 방식 유지
        playerController.JumpController.TryJump(true);

        // 제자리 점프는 이미 IdleJumpTrigger로 실행됨
        // 이동 점프에서만 MotionState를 변경
        if (!usePreJump)
        {
            playerController.PlayerAnimationController.SetMotion(
                PlayerMotion.Run_Jump);
        }
    }

    /// <summary>
    /// 이동 점프의 출발 발판과 착지 예정 지면의 실제 높이 차이를 검사
    /// </summary>
    private bool TryEnterRolling()
    {
        // 점프 출발 발판을 측정하지 못했으면 일반 착지로 처리
        if (!hasRollStartGround ||
            !hasLeftGround ||
            hasLanded ||
            playerController.JumpController.VerticalVelocity >= 0f)
        {
            return false;
        }

        GroundDetector ground = playerController.GroundDetector;

        // 착지 가능한 지면이 감지되지 않았거나
        // 이미 접지했다면 Rolling으로 전환하지 않음
        if (!ground.HasGroundHit ||
            ground.IsGrounded ||
            !ground.IsWalkableSlope)
        {
            return false;
        }

        // 최고점에서 내려온 거리가 아닌
        // 출발 발판과 착지 지면의 실제 높이 차이 계산
        float heightDifference =
            rollStartGroundY - ground.GroundHeightY;

        // 기준 높이보다 낮으면 일반 Run_Jump 유지
        if (heightDifference < MinRollHeightDifference)
        {
            return false;
        }

        // 착지 예정 지면에 가까워졌을 때 Rolling 시작
        if (ground.DistanceToGround > RollStartGroundDistance)
        {
            return false;
        }

        // 같은 점프에서 중복 실행 방지
        rollingTriggered = true;

        Debug.Log(
            $"[Rolling 진입] 높이 차이: {heightDifference:F2}m, " +
            $"지면 거리: {ground.DistanceToGround:F2}m");

        playerStateMachine.ChangeState(
            new PlayerRollingState(
                playerController,
                playerStateMachine));

        return true;
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
