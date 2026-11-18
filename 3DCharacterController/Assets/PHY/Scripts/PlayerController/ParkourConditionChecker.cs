
using UnityEngine;

public class ParkourConditionChecker : MonoBehaviour
{
    [Header("Components")]
    [SerializeField]
    private ParkourDetector parkourDetector;

    [SerializeField]
    private PlayerController playerController;

    [Header("Execution Conditions")]
    [SerializeField, Min(0.1f)]
    private float maxStartDistance = 1.3f;

    [SerializeField, Range(0f, 180f)]
    private float maxFacingAngle = 50f;

    public bool CanExecute(out string reason)
    {
        reason = "";

        if (parkourDetector == null ||
            playerController == null)
        {
            reason = "필수 컴포넌트 참조 없음";
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
            reason = "현재 구현 대상이 아닌 타입";
            return false;
        }

        Transform startPoint = obstacle.StartPoint;

        if (startPoint == null)
        {
            reason = "StartPoint 미설정";
            return false;
        }

        // Player와 StartPoint 사이의 수평 거리
        Vector3 toStart =
            startPoint.position - transform.position;

        toStart.y = 0f;

        float distance = toStart.magnitude;

        if (distance > maxStartDistance)
        {
            reason =
                $"거리 초과: {distance:F2}m";
            return false;
        }

        // 캐릭터와 파쿠르 진행 방향 비교
        Vector3 playerForward = transform.forward;
        Vector3 startForward = startPoint.forward;

        playerForward.y = 0f;
        startForward.y = 0f;

        if (playerForward.sqrMagnitude < 0.001f ||
            startForward.sqrMagnitude < 0.001f)
        {
            reason = "방향 벡터가 유효하지 않음";
            return false;
        }

        float angle = Vector3.Angle(
            playerForward,
            startForward);

        if (angle > maxFacingAngle)
        {
            reason =
                $"방향 불일치: {angle:F1}도";
            return false;
        }

        // 기존 GroundDetector의 접지 판정 재사용
        if (!playerController.GroundDetector.IsGrounded ||
            !playerController.GroundDetector.IsWalkableSlope)
        {
            reason = "접지 또는 경사 조건 미충족";
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
