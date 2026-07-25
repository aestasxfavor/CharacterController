using UnityEngine;

/// <summary>
/// 플레이어가 공중에서 하강 중일 때의 상태
/// Falling 모션 시작 여부와 착지 후 LandingState 진입 여부를 판단
/// </summary>
public class PlayerFallState : PlayerBaseState
{
    private bool LandingDone;
    private bool hasPlayedFalling;

    // 높은 낙하 진입 시 Falling 모션을 즉시 시작할지 판단하기 위한 값
    // 현재는 낮은 단차 오작동 가능성 때문에 즉시 재생 구조는 보류
    private bool startFallMotion;

    // 낙하 지속 시간과 낙하 중 가장 낮았던 Y속도 기록
    private float fallTimer;
    private float lowYSpeed;

    // 높은 낙하 판정 기준
    // 현재 값은 테스트 기준이며 최종 감각 조정 여지 있음 => 보류 변수
    //private const float HighFallTime = 0.35f;
    //private const float HighFallSpeed = -4f;

    // Falling 모션 시작 기준
    // FallState 진입 직후 바로 재생하지 않고 일정 시간과 하강 속도를 확인
    private float FallMotionDelay = 0.12f;
    private float FallMotionSpeed = -2.5f;

    public PlayerFallState(PlayerController _playerController, PlayerStateMachine _playerStateMachine, bool _startFallMotion)
        : base(_playerController, _playerStateMachine)
    {
        startFallMotion = _startFallMotion;
    }

    public override void Enter()
    {
        LandingDone = false;
        hasPlayedFalling = false;

        fallTimer = 0f;
        lowYSpeed = playerController.JumpController.VerticalVelocity;

        // 이전 상태에서 남아 있을 수 있는 공중 / 착지 애니메이션 값 초기화
        playerController.PlayerAnimationController.SetLanding(false);
        playerController.PlayerAnimationController.SetFalling(false);

        Debug.Log("fall enter");

        // Falling 모션 즉시 재생은 낮은 단차에서도 발동될 수 있어 현재 비활성화
        // 추후 낙하 높이 / Y속도 / 지면 거리 기준으로 재검토
        //if (startFallMotion)
        //{
        //    StartFallingAnimation();
        //}

        // 높은 낙하 판정을 위해 FallState 진입 시점의 Y속도 기록 시작
        playerController.HighFallDetector.StartFall(playerController.JumpController.VerticalVelocity);
    }

    public override void Update()
    {
        fallTimer += Time.deltaTime;

        // 낙하 중 가장 낮은 Y속도 기록
        lowYSpeed = Mathf.Min(lowYSpeed, playerController.JumpController.VerticalVelocity);

        // 낙하 시간과 Y속도를 기준으로 높은 낙하 여부 갱신
        playerController.HighFallDetector.CheckHighFall(Time.deltaTime, playerController.JumpController.VerticalVelocity);

        // Falling 모션이 아직 재생되지 않았고 조건을 만족하면 Falling 모션 시작
        if (!hasPlayedFalling && ShouldStartFallingMotion())
        {
            StartFallingAnimation();
        }

        // FallState 진입 직후에는 이전 프레임의 접지 정보가 남을 수 있어 짧게 대기
        if (fallTimer < 0.05f) return;

        // CharacterController.Move 이후 바닥 충돌이 있었으면 착지 처리
        if (playerController.ControllerGroundedAfterMove)
        {
            Land();
            return;
        }
    }

    private void StartFallingAnimation()
    {
        hasPlayedFalling = true;

        playerController.PlayerAnimationController.SetFalling(true);
    }

    /// <summary>
    /// Falling 모션을 시작할지 판단
    /// FallState 진입 직후, 지면과 너무 가까운 상황, 하강 속도가 부족한 상황에서는 재생하지 않음
    /// </summary>
    private bool ShouldStartFallingMotion()
    {
        if (fallTimer < FallMotionDelay) return false;

        // TODO: HasGroundHit 기준이 넓으면 높은 낙하 중 Falling 모션이 막힐 수 있음
        // 추후 DistanceToGround 기준으로 교체 검토
        if (playerController.GroundDetector.HasGroundHit) return false;

        return lowYSpeed <= FallMotionSpeed;
    }

    private void Land()
    {
        if (LandingDone) return;

        LandingDone = true;

        // HighFallDetector 결과와 실제 Falling 모션 재생 여부를 함께 사용해 착지 연출 판단
        bool wasHighFall = playerController.HighFallDetector.GetResultAndReset();
        bool shouldPlayLandingMotion = wasHighFall || hasPlayedFalling;

        playerController.PlayerAnimationController.SetFalling(false);

        // 높은 낙하 또는 Falling 모션이 재생된 경우에만 LandingState로 진입
        if (shouldPlayLandingMotion)
        {
            playerStateMachine.ChangeState(new PlayerLandState(playerController, playerStateMachine));

            return;
        }

        // 낮은 단차나 짧은 하강은 착지 연출 없이 바로 지상 상태로 복귀
        playerController.PlayerAnimationController.SetLanding(false);
        ChangeToIdleOrMove();
    }

    private void ChangeToIdleOrMove()
    {
        // 착지 후 이동 입력 여부에 따라 Idle 또는 Move로 복귀
        if (playerController.InputHandler.Movement.sqrMagnitude > 0.01f)
        {
            playerStateMachine.ChangeState(new PlayerMoveState(playerController, playerStateMachine));
        }
        else
        {
            playerStateMachine.ChangeState(new PlayerIdleState(playerController, playerStateMachine));
        }
    }

    public override void Exit()
    {
        Debug.Log("falling exit");
    }
}