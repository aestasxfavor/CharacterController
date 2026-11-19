
using UnityEngine;

public class PlayerIdleState : PlayerBaseState
{
    private readonly ParkourDetector parkourDetector;
    private readonly ParkourConditionChecker parkourChecker;

    public PlayerIdleState(
        PlayerController _playerController,
        PlayerStateMachine _playerStateMachine)
        : base(_playerController, _playerStateMachine)
    {
        parkourDetector =
            playerController.GetComponent<ParkourDetector>();

        parkourChecker =
            playerController.GetComponent<ParkourConditionChecker>();
    }

    public override void Enter()
    {
        playerController.PlayerAnimationController.SetAirborne(false);
        playerController.PlayerAnimationController.SetLanding(false);
        playerController.PlayerAnimationController.SetMoveInput(false);
        playerController.PlayerAnimationController.SetMotion(PlayerMotion.Locomotion);

        Debug.Log("idle enter");
    }

    public override void Update()
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

        bool canJump =
            playerController.InputHandler.JumpPressed &&
            playerController.GroundDetector.IsGrounded &&
            playerController.GroundDetector.IsWalkableSlope;

        if (canJump)
        {
            if (parkourDetector != null &&
                parkourDetector.HasObstacle &&
                parkourChecker != null)
            {
                bool canParkour =
                    parkourChecker.CanExecute(
                        out string reason);

                if (canParkour)
                {
                    Debug.Log(
                        $"[Parkour] Idle -> " +
                        $"{parkourDetector.CurrentObstacle.Type} " +
                        $"실행 조건 통과");

                    // 추가: Vault 타입만 VaultState로 전환
                    if (parkourDetector.CurrentObstacle.Type ==
                        ParkourType.Vault)
                    {
                        playerController.InputHandler.ResetJumpInput();

                        playerStateMachine.ChangeState(
                            new PlayerVaultState(
                                playerController,
                                playerStateMachine));

                        return;
                    }
                }
                else
                {
                    Debug.Log(
                        $"[Parkour] Idle 실행 불가: {reason}");
                }
            }

            playerController.InputHandler.ResetJumpInput();

            bool hasMoveInput =
                playerController.InputHandler.Movement.sqrMagnitude > 0.01f;

            playerStateMachine.ChangeState(
                new PlayerJumpState(
                    playerController,
                    playerStateMachine,
                    !hasMoveInput));

            return;
        }

        if (playerController.InputHandler.Movement.sqrMagnitude > 0.01f)
        {
            playerStateMachine.ChangeState(
                new PlayerMoveState(
                    playerController,
                    playerStateMachine));
            return;
        }
    }

    public override void Exit()
    {
        Debug.Log("idle exit");
    }
}
