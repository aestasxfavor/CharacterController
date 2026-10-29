using UnityEngine;

/// <summary>
/// SphereCast를 이용해 플레이어의 지면 상태를 감지하는 컴포넌트
/// 캡슐 폭만큼 검사해서 모서리에 걸쳐 서 있어도 지면으로 인식함
/// 지면 감지 여부, 바닥까지의 거리, 경사각, 이동 가능한 경사 여부를 계산
/// </summary>
public class GroundDetector : MonoBehaviour
{
    [Header("Ground Check")]
    [SerializeField] private Transform groundCheckPoint;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private CharacterController characterController;

    [Header("Ground Distance")]
    [SerializeField] private float maxGroundCheckDistance = 1.0f;
    [SerializeField] private float groundedDistance = 0.2f;
    [SerializeField] private float landingCheckDistance = 0.08f;

    [Header("Probe")]
    // CharacterController 반지름 대비 검사 구의 크기 (1보다 작게)
    [SerializeField] private float probeRadiusScale = 0.9f;
    // 구가 시작부터 바닥과 겹치지 않도록 발 위치에서 띄우는 높이
    [SerializeField] private float probeLift = 0.1f;

    [Header("Slope Setting")]
    [SerializeField] private float maxSlopeAngle = 45f;

    // 감지된 지면의 법선 방향 (경사각 계산과 경사면 이동 가능 여부 판단에 사용)
    public Vector3 GroundNormal { get; private set; } = Vector3.up;

    // 현재 감지된 지면의 경사각
    public float SlopeAngle { get; private set; }

    // groundCheckPoint 기준 바닥까지의 수직 거리
    public float DistanceToGround { get; private set; } = Mathf.Infinity;   // 수정이 필요할거 같음 성능을 많이 잡아먹을 것 같음

    // 지면을 감지했는지 여부
    public bool HasGroundHit { get; private set; }

    // 이동 가능한 지면 위에 있는지 여부 (거리 조건 + 경사각 조건)
    public bool IsGrounded { get; private set; }

    // 착지 판단에 사용할 정도로 지면과 가까운지 여부
    public bool IsNearGround { get; private set; }

    // 감지된 지면이 걸을 수 있는 경사면인지 여부
    public bool IsWalkableSlope { get; private set; }

    private void Reset()
    {
        characterController = GetComponent<CharacterController>();
    }

    private void Awake()
    {
        if (characterController == null) characterController = GetComponent<CharacterController>();
    }

    /// <summary>
    /// 구 형태로 아래를 검사해 지면 정보 갱신
    /// </summary>
    public void CheckGround()
    {
        if (groundCheckPoint == null)
        {
            ResetGroundInfo();
            return;
        }

        float radius = characterController != null
            ? characterController.radius * probeRadiusScale
            : 0.2f;

        Vector3 feet = groundCheckPoint.position;
        Vector3 sphereStart = feet + Vector3.up * (radius + probeLift);
        float castDistance = maxGroundCheckDistance + radius + probeLift;

        bool usedSphere = true;

        bool isHit = Physics.SphereCast(
            sphereStart,
            radius,
            Vector3.down,
            out RaycastHit hit,
            castDistance,
            groundLayer,
            QueryTriggerInteraction.Ignore
        );

        // 구가 시작부터 바닥과 겹치면 point / normal이 유효하지 않으므로 중심 ray로 대체
        if (isHit && hit.distance <= 0f)
        {
            usedSphere = false;
            isHit = Physics.Raycast(
                feet + Vector3.up * probeLift,
                Vector3.down,
                out hit,
                maxGroundCheckDistance + probeLift,
                groundLayer,
                QueryTriggerInteraction.Ignore
            );
        }

        HasGroundHit = isHit;

        if (!isHit)
        {
            ResetGroundInfo();
            return;
        }

        // 거리: 발 위치와 접촉점 사이의 수직 간격
        DistanceToGround = Mathf.Max(0f, feet.y - hit.point.y);

        // 법선: 모서리 접촉에서는 SphereCast 법선이 비스듬하게 나오므로
        // 접촉점 안쪽(바닥 쪽)에서 ray를 한 번 더 쏴서 실제 바닥면 법선을 구함
        Vector3 contactNormal = hit.normal;
        Vector3 inward = Vector3.zero;

        if (usedSphere)
        {
            Vector3 sphereCenterAtHit = sphereStart + Vector3.down * hit.distance;
            inward = hit.point - sphereCenterAtHit;
            inward.y = 0f;
            inward = inward.sqrMagnitude > 0.0001f ? inward.normalized : Vector3.zero;
        }

        Vector3 normalRayOrigin = hit.point + inward * 0.02f + Vector3.up * 0.1f;

        if (Physics.Raycast(
                normalRayOrigin,
                Vector3.down,
                out RaycastHit surfaceHit,
                0.3f,
                groundLayer,
                QueryTriggerInteraction.Ignore))
        {
            contactNormal = surfaceHit.normal;
        }

        GroundNormal = contactNormal;

        // 지면 법선과 위쪽 방향 사이의 각도로 경사각을 계산
        SlopeAngle = Vector3.Angle(Vector3.up, GroundNormal);
        IsWalkableSlope = SlopeAngle <= maxSlopeAngle;

        // 착지 가능한 거리 안에 있고 경사각도 걷기 가능한 범위 내에 있는지 확인
        IsGrounded = DistanceToGround <= groundedDistance && IsWalkableSlope;

        // 낙하 중 착지 전환 판단에 사용할 근접 지면 판정
        IsNearGround = DistanceToGround <= landingCheckDistance;

        Debug.DrawRay(hit.point, GroundNormal * 0.7f, Color.blue);
    }

    /// <summary>
    /// 지면을 감지하지 못했을 때 접지 관련 정보를 기본값으로 초기화
    /// </summary>
    private void ResetGroundInfo()
    {
        HasGroundHit = false;
        IsGrounded = false;
        IsNearGround = false;
        IsWalkableSlope = false;

        DistanceToGround = Mathf.Infinity;
        GroundNormal = Vector3.up;
        SlopeAngle = 0f;
    }

    /// <summary>
    /// Scene 뷰에서 지면 체크 범위 확인용 Gizmo
    /// </summary>
    private void OnDrawGizmos()
    {
        if (groundCheckPoint == null) return;

        float radius = characterController != null
            ? characterController.radius * probeRadiusScale
            : 0.2f;

        Gizmos.color = IsGrounded ? Color.green : Color.red;
        Vector3 feet = groundCheckPoint.position;
        Gizmos.DrawLine(feet, feet + Vector3.down * groundedDistance);
        Gizmos.DrawWireSphere(feet + Vector3.up * (radius + probeLift), radius);
    }
}