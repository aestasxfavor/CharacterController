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

    [Header("Turn")]
    // 이동 방향과 크게 어긋난 상태(급회전)에서 허용하는 최대 속도 비율
    // 1이면 회전 중 감속 없음, 낮을수록 급회전 때 많이 감속
    [SerializeField, Range(0f, 1f)] private float turnMinSpeedRatio = 0.4f;

    // 급회전 중 속도를 줄일 때의 감속 속도 (m/s²)
    [SerializeField] private float turnBrakeResponse = 40f;

    // 이 속도 이하에서는 현재 이동 방향이 의미 없다고 보고 입력 방향을 그대로 사용
    private const float MinDirectionSpeed = 0.05f;

    // 이 정렬도(이동 방향과 입력 방향의 내적) 미만이면 급회전으로 판단
    private const float TurnBrakeAlignment = 0.95f;

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

        if (isGrounded)
        {
            currentHorizontalVelocity = GetGroundVelocity(moveDirection, deltaTime);
            return currentHorizontalVelocity;
        }

        // 공중: 기존 방식 유지 (낮은 이동 영향력 + 별도의 가속 / 감속 값)
        Vector3 targetVelocity = moveDirection * CurrentMoveSpeed * airControl;
        float response = hasMoveInput ? airMoveResponse : airStopResponse;

        currentHorizontalVelocity = Vector3.MoveTowards(
            currentHorizontalVelocity,
            targetVelocity,
            response * deltaTime
        );

        return currentHorizontalVelocity;
    }

    /// <summary>
    /// 지상 이동 속도 계산
    /// 속도 벡터를 직접 보간하면 방향 전환 때 속도가 0을 거쳐 반대로 가속되어
    /// 몸은 이미 돌았는데 이전 방향으로 미끄러지는 구간이 생긴다.
    /// 이를 막기 위해 이동 방향은 캐릭터 회전과 같은 각속도로 입력 방향에 맞춰 돌리고,
    /// 속도(크기)는 따로 가감속한다. 급회전 구간에서는 허용 속도를 낮춰 감속한다.
    /// </summary>
    private Vector3 GetGroundVelocity(Vector3 moveDirection, float deltaTime)
    {
        Vector3 currentFlat = new Vector3(currentHorizontalVelocity.x, 0f, currentHorizontalVelocity.z);
        float currentSpeed = currentFlat.magnitude;
        float targetYaw = GetYaw(moveDirection);

        Vector3 direction = moveDirection;
        float alignment = 1f;

        if (currentSpeed > MinDirectionSpeed)
        {
            // Rotate()와 같은 함수 / 같은 각속도로 방향을 돌려 몸의 회전과 이동 방향을 맞춘다
            float newYaw = Mathf.MoveTowardsAngle(GetYaw(currentFlat), targetYaw, rotationSpeed * deltaTime);
            direction = YawToDirection(newYaw);
            alignment = Mathf.Clamp01(Vector3.Dot(direction, moveDirection));
        }

        // 입력 방향과 어긋날수록 허용 속도를 낮춤 (정렬되면 1)
        float turnLimit = Mathf.Lerp(turnMinSpeedRatio, 1f, alignment);
        float limitedTargetSpeed = CurrentMoveSpeed * turnLimit;

        bool isSlowingDown = currentSpeed > limitedTargetSpeed;
        bool isTurnBraking = isSlowingDown && alignment < TurnBrakeAlignment;

        float response;
        if (isTurnBraking) response = turnBrakeResponse;
        else response = isSlowingDown ? runToWalkResponse : moveResponse;

        float newSpeed = Mathf.MoveTowards(currentSpeed, limitedTargetSpeed, response * deltaTime);

        return direction * newSpeed;
    }

    /// <summary>
    /// 이동 방향을 기준으로 캐릭터 회전을 처리
    /// 경사면 이동 중 불필요한 회전 흔들림을 줄이기 위해
    /// 목표 방향(Y축 회전)까지 일정 각속도로 회전하는 방식 사용
    /// 이동 방향 보간(GetGroundVelocity)과 같은 MoveTowardsAngle을 써서 두 회전이 어긋나지 않게 한다.
    /// </summary>
    public void Rotate(Transform target, Vector3 moveDirection, float deltaTime)
    {
        Vector3 flatDirection = new Vector3(moveDirection.x, 0f, moveDirection.z);

        if (flatDirection.sqrMagnitude < 0.01f) return;

        float targetYaw = GetYaw(flatDirection);
        float newYaw = Mathf.MoveTowardsAngle(target.eulerAngles.y, targetYaw, rotationSpeed * deltaTime);

        target.rotation = Quaternion.Euler(0f, newYaw, 0f);
    }

    // 수평 방향 벡터를 Y축 회전각(도)으로 변환
    private static float GetYaw(Vector3 direction)
    {
        return Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
    }

    // Y축 회전각(도)을 수평 방향 벡터로 변환
    private static Vector3 YawToDirection(float yaw)
    {
        float rad = yaw * Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
    }
}
