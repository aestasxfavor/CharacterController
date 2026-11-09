
using UnityEngine;

/// <summary>
/// 공중에서 하강 중인 상태
/// 현재는 낙하 및 실제 착지 판정만 처리한다.
/// Falling과 FallingToRolling 모션은 추후 추가한다.
/// </summary>
public class PlayerFallState : PlayerBaseState
{
    // 진입 직후 이전 프레임 접지 정보 무시
    private const float LandCheckDelay = 0.05f;

    private bool landingDone;
    private float fallTimer;

    // 기존 생성자 호출과의 호환성을 위해 유지
    private readonly bool fromJump;

    public PlayerFallState(
        PlayerController _playerController,
        PlayerStateMachine _playerStateMachine,
        bool _fromJump)
        : base(_playerController, _playerStateMachine)
    {
        fromJump = _fromJump;
    }

    public override void Enter()
    {
        landingDone = false;
        fallTimer = 0f;

        playerController.PlayerAnimationController.SetLanding(false);
        playerController.PlayerAnimationController.SetAirborne(false);

        // 기존 InAir, Land 모션을 사용하지 않으므로
        // 낙하 중에는 임시로 Locomotion 유지
        playerController.PlayerAnimationController.SetMotion(
            PlayerMotion.Locomotion);

        // 기존 낙하 거리 측정 기능 유지
        playerController.HighFallDetector.StartFall(
            playerController.transform.position.y);
    }

    public override void Update()
    {
        fallTimer += Time.deltaTime;

        // 낙하 거리 갱신
        playerController.HighFallDetector.UpdateFall(
            playerController.transform.position.y);

        if (fallTimer < LandCheckDelay)
        {
            return;
        }

        // CharacterController 이동 이후 실제 접지 여부 확인
        if (playerController.ControllerGroundedAfterMove)
        {
            Land();
        }
    }

    // 실제 착지 처리
    private void Land()
    {
        if (landingDone) return;

        landingDone = true;

        // 낙하 거리 측정 결과 정리
        // 추후 FallingToRolling 적용 시 활용 예정
        playerController.HighFallDetector.GetResultAndReset();

        playerController.PlayerAnimationController.SetAirborne(false);
        playerController.PlayerAnimationController.SetLanding(false);

        ChangeToIdleOrMove();
    }

    // 이동 입력 여부에 따라 지상 상태 복귀
    private void ChangeToIdleOrMove()
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
    }

    public override void Exit()
    {

    }
}
