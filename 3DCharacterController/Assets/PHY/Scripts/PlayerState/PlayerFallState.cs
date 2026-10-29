using UnityEngine;

/// <summary>
/// 공중에서 하강 중인 상태
/// Airborne 모션 시작 시점과 착지 연출 여부를 판단
/// </summary>
public class PlayerFallState : PlayerBaseState
{
    private const float AirborneDelay = 0.12f;   // 접지가 끊긴 뒤 공중 모션까지 유예
    private const float LandCheckDelay = 0.05f;  // 진입 직후 이전 프레임 접지 정보 무시

    private bool landingDone;
    private bool hasShownAirborne;
    private float fallTimer;

    private float debugTimer;   // 밑 디버그에 dist가 infinity로 나옴 추후에 해결해야할 또 다른 문제
    // 세 번째 인자는 기존 호출부(Idle/Move/Jump) 호환용, 현재 사용하지 않음
    public PlayerFallState(PlayerController _playerController, PlayerStateMachine _playerStateMachine, bool _startFallMotion)
        : base(_playerController, _playerStateMachine) { }

    public override void Enter()
    {
        landingDone = false;
        hasShownAirborne = false;
        fallTimer = 0f;

        debugTimer = 0f;

        playerController.PlayerAnimationController.SetLanding(false);
        playerController.PlayerAnimationController.SetAirborne(false);

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
            playerController.PlayerAnimationController.SetAirborne(true);
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

    private void Land()
    {
        if (landingDone) return;
        landingDone = true;

        bool wasHighFall = playerController.HighFallDetector.GetResultAndReset();
        bool playLandMotion = wasHighFall || hasShownAirborne;

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