using UnityEngine;

/// <summary>
/// 공중에서 하강 중인 상태
/// Airborne 모션 시작 시점과 착지 연출 여부를 판단
/// </summary>
public class PlayerFallState : PlayerBaseState
{
    private const float AirborneDelay = 0.12f;   // 접지가 끊긴 뒤 공중 모션까지 유예
    private const float LandCheckDelay = 0.05f;  // 진입 직후 이전 프레임 접지 정보 무시
    private const float PreLandLeadTime = 0.25f; // 착지까지 남은 예상 시간이 이 값 이하면 PreLand 시작

    private bool landingDone;
    private bool hasShownAirborne;
    private bool inAirActive;      // InAir 모션이 시작된 상태 (점프에서 왔거나, 높이 조건을 만족한 낙하)
    private bool preLandShown;     // PreLand 모션을 이미 시작했는지
    private float fallTimer;

    private float debugTimer;   // 밑 디버그에 dist가 infinity로 나옴 추후에 해결해야할 또 다른 문제
    // 점프 정점에서 넘어온 낙하면 true: InAir 모션을 바로 시작
    // 걷다가 떨어진 낙하면 false: 유예 시간과 지면 거리 조건을 만족할 때 InAir 시작 (계단, 낮은 턱에서 포즈가 튀지 않게)
    private readonly bool fromJump;

    public PlayerFallState(PlayerController _playerController, PlayerStateMachine _playerStateMachine, bool _fromJump)
        : base(_playerController, _playerStateMachine)
    {
        fromJump = _fromJump;
    }

    public override void Enter()
    {
        landingDone = false;
        hasShownAirborne = false;
        inAirActive = fromJump;
        preLandShown = false;
        fallTimer = 0f;

        debugTimer = 0f;

        playerController.PlayerAnimationController.SetLanding(false);
        playerController.PlayerAnimationController.SetAirborne(false);

        // 점프에서 넘어온 경우에만 즉시 InAir. 걷다 떨어질 때는 Airborne 시작 시점에 호출한다
        if (fromJump)
        {
            playerController.PlayerAnimationController.SetMotion(PlayerMotion.InAir);
        }

        playerController.HighFallDetector.StartFall(playerController.transform.position.y);
    }

    public override void Update()
    {
        fallTimer += Time.deltaTime;

        if (!hasShownAirborne)
        {
            debugTimer += Time.deltaTime;
            if (debugTimer >= 0.1f)
            {
                debugTimer = 0f;
                var g = playerController.GroundDetector;
                Debug.Log($"[FallDebug] t={fallTimer:F2} dist={g.DistanceToGround:F2} hit={g.HasGroundHit} dropped={playerController.HighFallDetector.FallDistance:F2} input={playerController.InputHandler.Movement}");
            }
        }

        playerController.HighFallDetector.UpdateFall(playerController.transform.position.y);

        if (!hasShownAirborne && ShouldShowAirborne())
        {
            hasShownAirborne = true;
            inAirActive = true;
            playerController.PlayerAnimationController.SetAirborne(true);
            playerController.PlayerAnimationController.SetMotion(PlayerMotion.InAir);
        }

        // InAir 중 착지가 가까워지면 PreLand(착지 준비) 모션 시작
        if (inAirActive && !preLandShown && ShouldShowPreLand())
        {
            preLandShown = true;
            playerController.PlayerAnimationController.SetMotion(PlayerMotion.PreLand);
        }

        if (fallTimer < LandCheckDelay) return;

        if (playerController.ControllerGroundedAfterMove)
        {
            Land();
        }
    }

    // 접지가 끊긴 뒤 일정 시간이 지났고, 아래 땅이 충분히 멀 때만 공중 모션 시작
    private bool ShouldShowAirborne()
    {
        if (fallTimer < AirborneDelay) return false;

        return playerController.GroundDetector.DistanceToGround
               > playerController.HighFallDetector.AirborneGroundDistance;
    }

    // 하강 중이고, 지면까지 거리 / 낙하 속도(= 착지까지 남은 시간)가 PreLandLeadTime 이하일 때 true
    // DistanceToGround가 Infinity면 아직 감지 범위 밖이므로 false
    private bool ShouldShowPreLand()
    {
        float verticalSpeed = playerController.JumpController.VerticalVelocity;
        if (verticalSpeed >= -0.1f) return false;

        float distance = playerController.GroundDetector.DistanceToGround;
        if (float.IsInfinity(distance)) return false;

        float timeToGround = distance / -verticalSpeed;
        if (timeToGround > PreLandLeadTime) return false;

        Debug.Log($"[PreLand] dist={distance:F2} vy={verticalSpeed:F2} time={timeToGround:F2}");
        return true;
    }

    private void Land()
    {
        if (landingDone) return;
        landingDone = true;

        bool wasHighFall = playerController.HighFallDetector.GetResultAndReset();
        bool playLandMotion = wasHighFall || inAirActive;

        playerController.PlayerAnimationController.SetAirborne(false);

        if (playLandMotion)
        {
            playerStateMachine.ChangeState(new PlayerLandState(playerController, playerStateMachine));
            return;
        }

        playerController.PlayerAnimationController.SetLanding(false);
        ChangeToIdleOrMove();
    }

    private void ChangeToIdleOrMove()
    {
        if (playerController.InputHandler.Movement.sqrMagnitude > 0.01f)
            playerStateMachine.ChangeState(new PlayerMoveState(playerController, playerStateMachine));
        else
            playerStateMachine.ChangeState(new PlayerIdleState(playerController, playerStateMachine));
    }

    public override void Exit() { }
}