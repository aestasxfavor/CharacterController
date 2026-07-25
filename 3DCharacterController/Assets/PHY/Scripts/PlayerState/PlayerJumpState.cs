using UnityEngine;
/// <summary>
/// 플레이어 점프 상승 구간을 처리하는 상태
/// 실제로 지면을 벗어난 뒤 Y속도가 하강 방향으로 바뀌면 FallState로 전환됨
/// </summary>
public class PlayerJumpState : PlayerBaseState
{
    // 점프 직후 접지 판정이 잠깐 남는 경우를 구분하기 위한 값
    private bool hasLeftGround;

    public PlayerJumpState(PlayerController _playerController, PlayerStateMachine _playerStateMachine)
        : base(_playerController, _playerStateMachine)
    {
    }

    public override void Enter()
    {
        hasLeftGround = false;
    }

    public override void Update()
    {
        bool isGrounded = playerController.GroundDetector.IsGrounded;
        float ySpeed = playerController.JumpController.VerticalVelocity;

        // 점프 후 실제로 지면을 벗어났는지 확인하기 위한 로직
        if (!isGrounded)
        {
            hasLeftGround = true;
        }

        // 지면을 벗어난 뒤 Y속도가 0 이하가 되면 하강 구간으로 판단
        if (hasLeftGround && !isGrounded && ySpeed <= 0f)
        {
            playerStateMachine.ChangeState(new PlayerFallState(playerController, playerStateMachine, false));
            return;
        }

        // 짧은 점프처럼 FallState를 거치기 전에 다시 접지한 경우 바로 지상 상태로 복귀함
        if (hasLeftGround && isGrounded)
        {
            if (playerController.InputHandler.Movement.sqrMagnitude > 0.01f)
            {
                playerStateMachine.ChangeState(new PlayerMoveState(playerController, playerStateMachine));
            }
            else
            {
                playerStateMachine.ChangeState(new PlayerIdleState(playerController, playerStateMachine));
            }

            return;
        }
    }

    public override void Exit()
    {
       
    }
}