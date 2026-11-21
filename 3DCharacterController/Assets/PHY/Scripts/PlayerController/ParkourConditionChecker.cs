using UnityEngine;

public class ParkourConditionChecker : MonoBehaviour
{
    [Header("Components")]
    [SerializeField]
    private ParkourDetector parkourDetector;

    [SerializeField]
    private PlayerController playerController;

    [Header("Execution Conditions")]
    // StartPoint 앞(펜스 반대쪽)으로 이만큼 떨어져 있어도 허용
    [SerializeField, Min(0.1f)]
    private float maxApproachDistance = 1.5f;

    // StartPoint를 이만큼까지 지나쳤어도 허용 (뒤로 살짝 당겨서 정렬)
    [SerializeField, Min(0f)]
    private float maxPassedDistance = 0.5f;

    // 장애물 폭의 양 끝에서 이만큼 안쪽까지만 실행 허용
    // (끝에 서 있을 때 몸이 장애물 밖으로 나가 허공에서 시작하는 것을 방지)
    [SerializeField, Min(0f)]
    private float edgeMargin = 0.3f;

    // 폭을 계산하지 못했을 때(콜라이더 없음) 쓸 기본 반폭
    [SerializeField, Min(0.1f)]
    private float fallbackHalfWidth = 0.6f;

    [SerializeField, Range(0f, 180f)]
    private float maxFacingAngle = 50f;

    public bool CanExecute(out string reason)
    {
        reason = "";

        if (parkourDetector == null ||
            playerController == null)
        {
            reason = "필수 컴포넌트 누락";
            return false;
        }

        ParkourObstacle obstacle =
            parkourDetector.CurrentObstacle;

        if (obstacle == null)
        {
            reason = "감지된 장애물 없음";
            return false;
        }

        if (obstacle.Type != ParkourType.Vault &&
            obstacle.Type != ParkourType.Mantle)
        {
            reason = "실행 가능한 타입이 아님";
            return false;
        }

        Transform startPoint = obstacle.StartPoint;

        if (startPoint == null)
        {
            reason = "StartPoint 미설정";
            return false;
        }

        // StartPoint 진행축(forward = 장애물을 향하는 방향) 기준으로 위치 분해
        Vector3 startForward = startPoint.forward;
        startForward.y = 0f;

        if (startForward.sqrMagnitude < 0.001f)
        {
            reason = "StartPoint 방향 벡터 무효";
            return false;
        }

        startForward.Normalize();

        Vector3 startRight =
            Vector3.Cross(Vector3.up, startForward);

        Vector3 fromStart =
            transform.position - startPoint.position;

        fromStart.y = 0f;

        // 음수: 아직 StartPoint에 도달 전, 양수: 이미 지나침
        float along = Vector3.Dot(fromStart, startForward);
        float lateral = Mathf.Abs(Vector3.Dot(fromStart, startRight));

        if (along < -maxApproachDistance)
        {
            reason = $"너무 멀리 있음: {-along:F2}m 전";
            return false;
        }

        if (along > maxPassedDistance)
        {
            reason = $"StartPoint를 너무 지나침: {along:F2}m";
            return false;
        }

        // 좌우: 장애물 폭 안(끝에서 edgeMargin 안쪽)이면 어디든 허용
        float halfWidth = obstacle.GetHalfWidth(startRight);

        if (halfWidth <= 0f)
            halfWidth = fallbackHalfWidth;

        float allowedLateral = Mathf.Max(0f, halfWidth - edgeMargin);

        if (lateral > allowedLateral)
        {
            reason =
                $"장애물 폭 밖: {lateral:F2}m (허용 {allowedLateral:F2}m)";
            return false;
        }

        Vector3 playerForward = transform.forward;
        playerForward.y = 0f;

        if (playerForward.sqrMagnitude < 0.001f)
        {
            reason = "방향 벡터가 유효하지 않음";
            return false;
        }

        float angle = Vector3.Angle(
            playerForward,
            startForward);

        if (angle > maxFacingAngle)
        {
            reason = $"방향 불일치: {angle:F1}도";
            return false;
        }

        if (!playerController.GroundDetector.IsGrounded ||
            !playerController.GroundDetector.IsWalkableSlope)
        {
            reason = "지면 또는 경사 조건 불만족";
            return false;
        }

        if (playerController.JumpController.IsJumping ||
            playerController.ShouldStartFalling)
        {
            reason = "점프 또는 낙하 중";
            return false;
        }

        reason = "실행 가능";
        return true;
    }
}