using UnityEngine;
/// <summary>
/// 플레이어 점프 구간을 처리하는 상태
/// 순서: (PreJump 웅크림) → Jump 도약 → 상승 → 정점에서 FallState로 전환
/// 제자리 점프는 웅크린 뒤 실제로 뜨고, 이동 중 점프는 웅크림 없이 바로 뜬다
/// </summary>
public class PlayerJumpState : PlayerBaseState
{
    // 웅크리기 시작해서 실제로 땅을 박차기까지의 시간 (PreJump 클립은 0.40초, 눈으로 보며 조절)
    private const float PreJumpDuration = 0.15f;

    private readonly bool usePreJump;

    // 웅크림이 끝나고 수직 속도를 적용했는지
    private bool jumpStarted;
    private float preJumpTimer;

    // 점프 직후 접지 판정이 잠깐 남는 경우를 구분하기 위한 값
    private bool hasLeftGround;

    public PlayerJumpState(PlayerController _playerController, PlayerStateMachine _playerStateMachine, bool _usePreJump)
        : base(_playerController, _playerStateMachine)
    {
        usePreJump = _usePreJump;
    }

    public override void Enter()
    {
        hasLeftGround = false;
        jumpStarted = false;
        preJumpTimer = 0f;

        if (usePreJump)
        {
            playerController.PlayerAnimationController.SetMotion(PlayerMotion.PreJump);
        }
        else
        {
            StartJump();
        }
    }

    public override void Update()
    {
        // 웅크리는 동안: 시간이 차면 점프 시작, 그 전에 바닥이 사라지면 낙하로 전환
        if (!jumpStarted)
        {
            if (playerController.ShouldStartFalling)
            {
                playerStateMachine.ChangeState(new PlayerFallState(playerController, playerStateMachine, false));
                return;
            }

            preJumpTimer += Time.deltaTime;
            if (preJumpTimer >= PreJumpDuration)
            {
                StartJump();
            }

            return;
        }

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
            playerStateMachine.ChangeState(new PlayerFallState(playerController, playerStateMachine, true));
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

    // 점프 시작 높이 기록, 수직 속도 적용, Jump 모션 시작
    private void StartJump()
    {
        jumpStarted = true;

        playerController.SetJumpStartY(playerController.transform.position.y);
        playerController.JumpController.TryJump(true);
        playerController.PlayerAnimationController.SetMotion(PlayerMotion.Jump);
    }

    public override void Exit()
    {

    }
}
