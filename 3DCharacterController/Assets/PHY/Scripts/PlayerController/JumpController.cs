using UnityEngine;
/// <summary>
/// 점프와 중력에 따른 수직 속도를 계산하는 컨트롤러
/// 수평 이동은 MovementController에서 처리함
/// JumpController는 Y축 이동 흐름만 담당함
/// </summary>
public class JumpController : MonoBehaviour
{
    [SerializeField] private float jumpPower = 6f;
    [SerializeField] private float gravityScale = 3.5f;
    [SerializeField] private float maxFallSpeed = 20f;

    // 지면에 붙어 있을 때 아래 방향으로 약하게 눌러 접지 상태를 유지하기 위한 속도
    [SerializeField] private float groundStickVelocity = -0.5f;

    // 현재 수직 이동 속도
    public float VerticalVelocity { get; private set; }

    // 현재 점프 중인지 여부
    public bool IsJumping { get; private set; }

    //점프 후 실제로 지면을 벗어났는지 확인하기 위한 값
    private bool hasLeftGround;

    /// <summary>
    /// 접지 상태일 때 점프 시작 속도 적용
    /// </summary>
    /// <param name="isGrounded">현재 접지 여부</param>
    public void TryJump(bool isGrounded)
    {
        if (!isGrounded) return;

        VerticalVelocity = jumpPower;
        IsJumping = true;
        hasLeftGround = false;
    }

    /// <summary>
    /// 접지 상태와 시간 값을 기준으로 수직 속도를 갱신
    /// 점프 중에는 중력을 적용하고, 지상에서는 접지 유지 속도를 적용
    /// </summary>
    /// <param name="isGrounded">현재 접지 여부</param>
    /// <param name="deltaTime">프레임 보정 시간</param>
    public void UpdateVertical(bool isGrounded, float deltaTime)
    {
        // 점프 중이고 실제로 땅에서 떨어진 프레임을 한 번이라도 겪었는지 기록
        if (IsJumping && !isGrounded)
        {
            hasLeftGround = true;
        }

        // 점프 중이 아니고 땅에 붙어 있으면 바닥 고정 속도만 유지
        if (!IsJumping && isGrounded)
        {
            if (VerticalVelocity < 0f)
            {
                VerticalVelocity = groundStickVelocity;
            }

            return;
        }

        // 점프 상승/하강 중에는 grounded가 잠깐 true여도 중력은 계속 적용
        ApplyGravity(deltaTime);

        LimitFallSpeed();

        // 점프 후 실제로 공중에 나간 적이 있고, 다시 땅에 닿았을 때만 점프를 종료함
        if (IsJumping && hasLeftGround && isGrounded && VerticalVelocity <= 0f)
        {
            EndJump();
        }
    }

    /// <summary>
    /// Unity 중력 값과 gravityScale을 이용해 수직 속도에 중력을 적용
    /// </summary>
    /// <param name="deltaTime">프레임 보정 시간</param>
    private void ApplyGravity(float deltaTime)
    {
        VerticalVelocity += Physics.gravity.y * gravityScale * deltaTime;
    }

    /// <summary>
    /// 낙하 속도가 설정한 최대값을 넘지 않도록 제한
    /// </summary>
    private void LimitFallSpeed()
    {
        if (VerticalVelocity < -maxFallSpeed)
        {
            VerticalVelocity = -maxFallSpeed;
        }
    }

    /// <summary>
    /// 점프 상태를 종료하고 접지 유지 속도로 전환함
    /// </summary>
    private void EndJump()
    {
        IsJumping = false;
        hasLeftGround = false;
        VerticalVelocity = groundStickVelocity;
    }

    
}