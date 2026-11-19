
using UnityEngine;

/// <summary>
/// Vault 파쿠르 상태.
/// 애니메이션 실행 및 완료 후 FSM 복귀를 담당한다.
/// Root Motion 이동 처리는 아직 구현하지 않는다.
/// </summary>
public class PlayerVaultState : PlayerBaseState
{
    // Animator에 등록된 실제 State 이름
    private const string VaultStateName = "Vault";

    private bool isFinishing;

    public PlayerVaultState(
        PlayerController _playerController,
        PlayerStateMachine _playerStateMachine)
        : base(_playerController, _playerStateMachine)
    {
    }

    public override void Enter()
    {
        isFinishing = false;

        // 기존 Animator 파라미터 재사용
        playerController.PlayerAnimationController.SetAirborne(false);
        playerController.PlayerAnimationController.SetLanding(false);

        // Vault 애니메이션 실행
        playerController.PlayerAnimationController.SetMotion(
            PlayerMotion.Vault);

        Debug.Log("[Vault] Enter");
    }

    public override void Update()
    {
        if (isFinishing)
            return;

        // 기존 애니메이션 완료 판정 메서드 재사용
        bool isComplete =
            playerController.PlayerAnimationController
                .IsMotionComplete(VaultStateName);

        if (!isComplete)
            return;

        FinishVault();
    }

    private void FinishVault()
    {
        if (isFinishing)
            return;

        isFinishing = true;

        Debug.Log("[Vault] Animation Complete");

        // 현재 접지 상태 확인
        playerController.GroundDetector.CheckGround();

        bool isGrounded =
            playerController.GroundDetector.IsGrounded &&
            playerController.GroundDetector.IsWalkableSlope;

        // 미접지 상태면 Fall로 복귀
        if (!isGrounded)
        {
            Debug.Log("[Vault] Exit -> Fall");

            playerStateMachine.ChangeState(
                new PlayerFallState(
                    playerController,
                    playerStateMachine,
                    false));

            return;
        }

        // 이동 입력 여부 확인
        bool hasMoveInput =
            playerController.InputHandler.Movement.sqrMagnitude
            > 0.01f;

        if (hasMoveInput)
        {
            Debug.Log("[Vault] Exit -> Move");

            playerStateMachine.ChangeState(
                new PlayerMoveState(
                    playerController,
                    playerStateMachine));
        }
        else
        {
            Debug.Log("[Vault] Exit -> Idle");

            playerStateMachine.ChangeState(
                new PlayerIdleState(
                    playerController,
                    playerStateMachine));
        }
    }

    public override void Exit()
    {
        Debug.Log("[Vault] Exit");
    }
}
