using UnityEngine;

/// <summary>
/// 높은 낙하 후 착지 연출을 처리하는 상태
/// 입력 여부에 따라 착지 유지 시간을 다르게 적용한 뒤 IdleState 또는 MoveState로 복귀
/// </summary>
public class PlayerLandState : PlayerBaseState
{
    private float landingTimer;

    // 제자리 착지는 착지 모션을 조금 더 보여주고
    // 이동 입력이 있으면 빠르게 이동 상태로 복귀
    private float idleLandingDuration = 0.35f;
    private float moveLandingDuration = 0.2f;

    public PlayerLandState(PlayerController _playerController, PlayerStateMachine _playerStateMachine)
        : base(_playerController, _playerStateMachine)
    {
    }

    public override void Enter()
    {
        landingTimer = 0f;

        // 착지 상태 진입 시 Falling은 끄고 Landing 모션 활성화
        playerController.PlayerAnimationController.SetFalling(false);
        playerController.PlayerAnimationController.SetLanding(true);

        Debug.Log("landing enter");
    }

    public override void Update()
    {
        landingTimer += Time.deltaTime;

        bool hasMoveInput = playerController.InputHandler.Movement.sqrMagnitude > 0.01f;

        // 착지 중 이동 입력 여부를 Animator에 전달해 착지 후 전환 흐름 보조
        playerController.PlayerAnimationController.SetMoveInput(hasMoveInput);

        // 이동 입력이 있으면 착지 연출을 짧게 유지해 조작 반응성을 우선
        float targetDuration = hasMoveInput ? moveLandingDuration : idleLandingDuration;

        if (landingTimer < targetDuration) return;

        if (hasMoveInput)
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
        // 착지 상태 종료 시 공중 / 착지 애니메이션 값 정리
        playerController.PlayerAnimationController.SetLanding(false);
        playerController.PlayerAnimationController.SetFalling(false);

        Debug.Log("landing exit");
    }
}