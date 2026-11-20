using UnityEngine;

public class PlayerMoveState : PlayerBaseState
{
    public PlayerMoveState(
        PlayerController _playerController,
        PlayerStateMachine _playerStateMachine)
        : base(_playerController, _playerStateMachine)
    {
    }

    public override void Enter()
    {
        playerController.PlayerAnimationController.SetAirborne(false);
        playerController.PlayerAnimationController.SetLanding(false);
        playerController.PlayerAnimationController.SetMoveInput(true);
        playerController.PlayerAnimationController.SetMotion(PlayerMotion.Locomotion);

        Debug.Log("move enter");
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
            playerController.InputHandler.ResetJumpInput();

            // 파쿠르 우선 시도, 실패하면 일반 점프
            if (TryStartParkour())
                return;

            Debug.Log("move -> jump 전이");

            playerStateMachine.ChangeState(
                new PlayerJumpState(
                    playerController,
                    playerStateMachine,
                    false));
            return;
        }

        if (playerController.InputHandler.Movement.sqrMagnitude <= 0.01f)
        {
            playerStateMachine.ChangeState(
                new PlayerIdleState(
                    playerController,
                    playerStateMachine));
            return;
        }
    }

    private bool TryStartParkour()
    {
        ParkourController parkour = playerController.ParkourController;

        return parkour != null &&
               parkour.TryStartParkour(
                   playerController,
                   playerStateMachine,
                   "Move");
    }

    public override void Exit()
    {
        Debug.Log("move exit");
    }
}