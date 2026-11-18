
using UnityEngine;

/// <summary>
/// 점프와 중력에 따른 수직 속도를 계산하는 컨트롤러
/// 수평 이동은 MovementController에서 처리
/// </summary>
public class JumpController : MonoBehaviour
{
    [Header("Jump Settings")]
    [SerializeField] private float jumpPower = 6f;
    [SerializeField] private float gravityScale = 3.5f;
    [SerializeField] private float maxFallSpeed = 20f;
    [SerializeField] private float groundStickVelocity = -0.5f;

    [Header("Debug")]
    [SerializeField] private bool showJumpDebug = true;

    public float VerticalVelocity { get; private set; }
    public bool IsJumping { get; private set; }

    private bool hasLeftGround;

    // 로그 출력 간격 제어용
    private float nextDebugTime;

    public void TryJump(bool isGrounded)
    {
        if (!isGrounded)
        {
            //if (showJumpDebug)
            //{
            //    Debug.LogWarning("[Jump Debug] TryJump 실패: IsGrounded = False");
            //}

            return;
        }

        VerticalVelocity = jumpPower;
        IsJumping = true;
        hasLeftGround = false;
        nextDebugTime = 0f;

        //if (showJumpDebug)
        //{
        //    Debug.Log(
        //        $"[Jump Debug] 점프 시작 | " +
        //        $"IsJumping: {IsJumping}, " +
        //        $"HasLeftGround: {hasLeftGround}, " +
        //        $"VelocityY: {VerticalVelocity:F2}"
        //    );
        //}
    }

    public void UpdateVertical(bool isGrounded, float deltaTime)
    {
        // 실제 지면 이탈 최초 확인
        if (IsJumping && !isGrounded && !hasLeftGround)
        {
            hasLeftGround = true;

            //if (showJumpDebug)
            //{
            //    Debug.Log(
            //        "[Jump Debug] 지면 이탈 확인 | " +
            //        "HasLeftGround: True"
            //    );
            //}
        }

        // 기존 지상 속도 처리
        if (!IsJumping && isGrounded)
        {
            if (VerticalVelocity < 0f)
            {
                VerticalVelocity = groundStickVelocity;
            }

            return;
        }

        // 기존 중력 적용
        ApplyGravity(deltaTime);
        LimitFallSpeed();

        // 하강 중 내부 상태 확인
        if (showJumpDebug &&
            IsJumping &&
            VerticalVelocity <= 0f &&
            Time.time >= nextDebugTime)
        {
            nextDebugTime = Time.time + 0.25f;

            //Debug.Log(
            //    $"[Jump Debug] 하강 상태 | " +
            //    $"CC Grounded: {isGrounded}, " +
            //    $"HasLeftGround: {hasLeftGround}, " +
            //    $"IsJumping: {IsJumping}, " +
            //    $"VelocityY: {VerticalVelocity:F2}"
            //);
        }

        // 기존 점프 종료 조건 유지
        if (IsJumping &&
            hasLeftGround &&
            isGrounded &&
            VerticalVelocity <= 0f)
        {
            //if (showJumpDebug)
            //{
            //    Debug.Log(
            //        "[Jump Debug] 점프 종료 조건 충족 → EndJump 호출"
            //    );
            //}

            EndJump();
        }
    }

    private void ApplyGravity(float deltaTime)
    {
        VerticalVelocity +=
            Physics.gravity.y * gravityScale * deltaTime;
    }

    private void LimitFallSpeed()
    {
        if (VerticalVelocity < -maxFallSpeed)
        {
            VerticalVelocity = -maxFallSpeed;
        }
    }

    private void EndJump()
    {
        IsJumping = false;
        hasLeftGround = false;
        VerticalVelocity = groundStickVelocity;

        //if (showJumpDebug)
        //{
        //    Debug.Log(
        //        $"[Jump Debug] 점프 종료 완료 | " +
        //        $"IsJumping: {IsJumping}, " +
        //        $"HasLeftGround: {hasLeftGround}, " +
        //        $"VelocityY: {VerticalVelocity:F2}"
        //    );
        //}
    }
}
