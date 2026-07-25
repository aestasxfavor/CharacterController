using UnityEngine;

/// <summary>
/// 플레이어의 수평 이동과 회전을 계산하는 컨트롤러
/// 카메라 기준 이동 방향, 걷기 / 달리기 속도, 지상 / 공중 이동 반응을 처리
/// </summary>
public class MovementController : MonoBehaviour
{
    [SerializeField] private float walkSpeed = 4f;
    [SerializeField] private float runSpeed = 7f;

    [SerializeField] private float rotationSpeed = 1080f;

    [SerializeField] private float moveResponse = 10f;
    [SerializeField] private float runToWalkResponse = 4f;

    [SerializeField] private float airControl = 0.5f;
    [SerializeField] private float airMoveResponse = 5f;
    [SerializeField] private float airStopResponse = 5f;

    private Vector3 currentHorizontalVelocity;

    public float WalkSpeed => walkSpeed;
    public float RunSpeed => runSpeed;
    public float CurrentHorizontalSpeed => currentHorizontalVelocity.magnitude;
    public float CurrentMoveSpeed { get; private set; }

    public bool IsRunning { get; private set; }

    /// <summary>
    /// 입력 방향을 카메라 기준 월드 이동 방향으로 변환
    /// 카메라가 없을 경우 입력값 기준 기본 방향 반환
    /// </summary>
    public Vector3 GetMoveDirection(Vector2 movement, Transform cameraTransform)
    {
        if (movement.sqrMagnitude < 0.01f) return Vector3.zero;

        // 카메라 없으면 입력값 기준 기본 방향 사용
        if (cameraTransform == null)
        {
            Vector3 fallbackDirection = new Vector3(movement.x, 0f, movement.y).normalized;
            return fallbackDirection;
        }

        Vector3 cameraForward = cameraTransform.forward;
        Vector3 cameraRight = cameraTransform.right;

        // 수평 이동만 반영하기 위한 y축 제거 로직
        cameraForward.y = 0f;
        cameraRight.y = 0f;



        cameraForward.Normalize();
        cameraRight.Normalize();

        Vector3 moveDirection = cameraForward * movement.y + cameraRight * movement.x;
        moveDirection = moveDirection.normalized;

        return moveDirection;
    }

    /// <summary>
    /// 지상 / 공중 상태와 달리기 입력에 따라 최종 수평 이동 속도를 계산
    /// </summary>
    public Vector3 GetHorizontalMove(Vector3 moveDirection, float deltaTime, bool isGrounded, bool isRunPressed)
    {
        bool hasMoveInput = moveDirection.sqrMagnitude > 0.01f;

        IsRunning = hasMoveInput && isRunPressed;

        CurrentMoveSpeed = hasMoveInput ? (IsRunning ? runSpeed : walkSpeed) : 0f;

        // 지상에서 이동 입력이 없을 경우 남아 있는 수평 속도 제거
        // 감속 보간만 사용할 경우 키를 뗀 뒤 미끄러지는 느낌이 남을 수 있어
        // 정지 안정성을 우선하는 방식으로 처리
        if (isGrounded && !hasMoveInput)
        {
            currentHorizontalVelocity = Vector3.zero;
            return Vector3.zero;
        }

        float speedMultiplier = isGrounded ? 1f : airControl;

        Vector3 targetVelocity = moveDirection * CurrentMoveSpeed * speedMultiplier;

        float response;

        if (isGrounded)
        {
            float currentSpeed = currentHorizontalVelocity.magnitude;
            float targetSpeed = targetVelocity.magnitude;

            bool isSlowingDown = currentSpeed > targetSpeed;

            response = isSlowingDown ? runToWalkResponse : moveResponse;
        }
        else
        {
            response = hasMoveInput ? airMoveResponse : airStopResponse;
        }

        currentHorizontalVelocity = Vector3.MoveTowards(
            currentHorizontalVelocity,
            targetVelocity,
            response * deltaTime
        );

        return currentHorizontalVelocity;
    }

    /// <summary>
    /// 이동 방향을 기준으로 캐릭터 회전을 처리
    /// 경사면 이동 중 불필요한 회전 흔들림을 줄이기 위해
    /// 목표 회전까지 일정 각속도로 회전하는 RotateTowards 방식 사용
    /// </summary>

    public void Rotate(Transform target, Vector3 moveDirection, float deltaTime)
    {
        Vector3 flatDirection = new Vector3(moveDirection.x, 0f, moveDirection.z);

        if (flatDirection.sqrMagnitude < 0.01f) return;

        Quaternion targetRotation = Quaternion.LookRotation(flatDirection);
        target.rotation = Quaternion.RotateTowards(target.rotation, targetRotation, rotationSpeed * deltaTime);


    }
}