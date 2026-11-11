
using UnityEngine;

/// <summary>
/// 플레이어의 수평 이동과 회전을 계산하는 컨트롤러
/// 카메라 기준 이동 방향, 걷기와 달리기 속도, 지상 및 공중 이동을 처리
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
    // 이동 방향과 크게 어긋난 상태에서 허용하는 최대 속도 비율
    [SerializeField, Range(0f, 1f)]
    private float turnMinSpeedRatio = 0.4f;

    // 급회전 시 감속 속도
    [SerializeField] private float turnBrakeResponse = 40f;

    // 이 속도 이하에서는 기존 진행 방향을 사용하지 않음
    private const float MinDirectionSpeed = 0.05f;

    // 급회전으로 판단하는 방향 정렬 기준
    private const float TurnBrakeAlignment = 0.95f;

    private Vector3 currentHorizontalVelocity;

    // 하강 및 구르기 중 이동 입력 잠금
    private bool isFallMovementLocked;

    public float WalkSpeed => walkSpeed;
    public float RunSpeed => runSpeed;
    public float CurrentHorizontalSpeed => currentHorizontalVelocity.magnitude;
    public float CurrentMoveSpeed { get; private set; }
    public bool IsRunning { get; private set; }
    public bool IsFallMovementLocked => isFallMovementLocked;

    /// <summary>
    /// 이동 입력 잠금 여부 설정
    /// 잠금 시 현재 수평 속도와 방향을 그대로 유지
    /// </summary>
    public void SetFallMovementLocked(bool locked)
    {
        isFallMovementLocked = locked;
    }

    /// <summary>
    /// 입력 방향을 카메라 기준 월드 이동 방향으로 변환
    /// </summary>
    public Vector3 GetMoveDirection(
        Vector2 movement,
        Transform cameraTransform)
    {
        if (movement.sqrMagnitude < 0.01f)
            return Vector3.zero;

        if (cameraTransform == null)
        {
            Vector3 fallbackDirection =
                new Vector3(movement.x, 0f, movement.y).normalized;

            return fallbackDirection;
        }

        Vector3 cameraForward = cameraTransform.forward;
        Vector3 cameraRight = cameraTransform.right;

        cameraForward.y = 0f;
        cameraRight.y = 0f;

        cameraForward.Normalize();
        cameraRight.Normalize();

        Vector3 moveDirection =
            cameraForward * movement.y +
            cameraRight * movement.x;

        return moveDirection.normalized;
    }

    /// <summary>
    /// 지상 및 공중 상태에 따라 수평 이동 속도를 계산
    /// </summary>
    public Vector3 GetHorizontalMove(
        Vector3 moveDirection,
        float deltaTime,
        bool isGrounded,
        bool isRunPressed)
    {
        // 하강 또는 Rolling 중에는 기존 수평 속도를 유지
        if (isFallMovementLocked)
        {
            return currentHorizontalVelocity;
        }

        bool hasMoveInput =
            moveDirection.sqrMagnitude > 0.01f;

        IsRunning = hasMoveInput && isRunPressed;

        CurrentMoveSpeed =
            hasMoveInput
                ? (IsRunning ? runSpeed : walkSpeed)
                : 0f;

        // 지상에서 입력이 없으면 즉시 정지
        if (isGrounded && !hasMoveInput)
        {
            currentHorizontalVelocity = Vector3.zero;
            return Vector3.zero;
        }

        // 지상 이동은 기존 계산 방식 유지
        if (isGrounded)
        {
            currentHorizontalVelocity =
                GetGroundVelocity(moveDirection, deltaTime);

            return currentHorizontalVelocity;
        }

        // 공중에서는 별도의 이동 처리 사용
        currentHorizontalVelocity =
            GetAirVelocity(
                moveDirection,
                deltaTime,
                hasMoveInput);

        return currentHorizontalVelocity;
    }

    /// <summary>
    /// 공중 이동 계산
    /// 기존 수평 속도를 자동으로 감소시키지 않음
    /// 이동 입력이 있으면 제한적인 방향 조작을 허용
    /// 입력이 없으면 기존 수평 관성을 유지
    /// </summary>
    private Vector3 GetAirVelocity(
        Vector3 moveDirection,
        float deltaTime,
        bool hasMoveInput)
    {
        // 공중에서 키를 놓더라도 수평 속도 유지
        if (!hasMoveInput)
        {
            return currentHorizontalVelocity;
        }

        float currentSpeed =
            currentHorizontalVelocity.magnitude;

        // 공중에서 사용할 목표 속도
        // 이미 보유한 수평 속도보다 낮게 만들지 않음
        float targetSpeed = Mathf.Max(
            currentSpeed,
            CurrentMoveSpeed * airControl);

        // 수평 속도가 거의 없다면 기존 공중 가속 방식 사용
        if (currentSpeed <= MinDirectionSpeed)
        {
            Vector3 targetVelocity =
                moveDirection * targetSpeed;

            return Vector3.MoveTowards(
                currentHorizontalVelocity,
                targetVelocity,
                airMoveResponse * deltaTime);
        }

        // 기존 수평 속도의 크기는 유지하고
        // 공중 입력에 따른 방향만 제한적으로 변경
        Vector3 currentDirection =
            currentHorizontalVelocity.normalized;

        // 속도가 빠를수록 동일한 입력으로 회전하는 각도가 작아짐
        float maxRadiansDelta =
            airMoveResponse * deltaTime /
            Mathf.Max(currentSpeed, MinDirectionSpeed);

        Vector3 newDirection = Vector3.RotateTowards(
            currentDirection,
            moveDirection,
            maxRadiansDelta,
            0f);

        // 공중에서 기존 속도가 감소하지 않도록 유지
        return newDirection.normalized * currentSpeed;
    }

    /// <summary>
    /// 지상 이동 속도 계산
    /// 기존 이동 방향 보간과 급회전 감속 로직 유지
    /// </summary>
    private Vector3 GetGroundVelocity(
        Vector3 moveDirection,
        float deltaTime)
    {
        Vector3 currentFlat = new Vector3(
            currentHorizontalVelocity.x,
            0f,
            currentHorizontalVelocity.z);

        float currentSpeed = currentFlat.magnitude;
        float targetYaw = GetYaw(moveDirection);

        Vector3 direction = moveDirection;
        float alignment = 1f;

        if (currentSpeed > MinDirectionSpeed)
        {
            float newYaw = Mathf.MoveTowardsAngle(
                GetYaw(currentFlat),
                targetYaw,
                rotationSpeed * deltaTime);

            direction = YawToDirection(newYaw);

            alignment = Mathf.Clamp01(
                Vector3.Dot(direction, moveDirection));
        }

        // 방향이 어긋날수록 급회전 시 허용 속도 감소
        float turnLimit = Mathf.Lerp(
            turnMinSpeedRatio,
            1f,
            alignment);

        float limitedTargetSpeed =
            CurrentMoveSpeed * turnLimit;

        bool isSlowingDown =
            currentSpeed > limitedTargetSpeed;

        bool isTurnBraking =
            isSlowingDown &&
            alignment < TurnBrakeAlignment;

        float response;

        if (isTurnBraking)
        {
            response = turnBrakeResponse;
        }
        else
        {
            response = isSlowingDown
                ? runToWalkResponse
                : moveResponse;
        }

        float newSpeed = Mathf.MoveTowards(
            currentSpeed,
            limitedTargetSpeed,
            response * deltaTime);

        return direction * newSpeed;
    }

    /// <summary>
    /// 이동 방향을 기준으로 캐릭터 회전
    /// 기존 지상 회전 방식 유지
    /// </summary>
    public void Rotate(
        Transform target,
        Vector3 moveDirection,
        float deltaTime)
    {
        Vector3 flatDirection = new Vector3(
            moveDirection.x,
            0f,
            moveDirection.z);

        if (flatDirection.sqrMagnitude < 0.01f)
            return;

        float targetYaw = GetYaw(flatDirection);

        float newYaw = Mathf.MoveTowardsAngle(
            target.eulerAngles.y,
            targetYaw,
            rotationSpeed * deltaTime);

        target.rotation =
            Quaternion.Euler(0f, newYaw, 0f);
    }

    // 수평 방향을 Y축 회전각으로 변환
    private static float GetYaw(Vector3 direction)
    {
        return Mathf.Atan2(
            direction.x,
            direction.z) * Mathf.Rad2Deg;
    }

    // Y축 회전각을 수평 방향 벡터로 변환
    private static Vector3 YawToDirection(float yaw)
    {
        float rad = yaw * Mathf.Deg2Rad;

        return new Vector3(
            Mathf.Sin(rad),
            0f,
            Mathf.Cos(rad));
    }
}
