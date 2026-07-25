using UnityEngine;
/// <summary>
/// Raycast를 이용해 플레이어의 지면 상태를 감지하는 컴포넌트
/// 지면 감지 여부, 바닥까지의 거리, 경사각, 이동 가능한 경사 여부를 계산
/// </summary>
public class GroundDetector : MonoBehaviour
{
    [Header("Ground Check")]
    [SerializeField] private Transform groundCheckPoint;
    [SerializeField] private LayerMask groundLayer;

    [Header("Ground Distance")]
    [SerializeField] private float maxGroundCheckDistance = 1.0f;
    [SerializeField] private float groundedDistance = 0.2f;
    [SerializeField] private float landingCheckDistance = 0.08f;

    [Header("Slope Setting")]
    [SerializeField] private float maxSlopeAngle = 45f;

    // 감지된 지면의 법선 방향
    // 경사각 계산과 경사면 이동 가능 여부 판단에 사용함
    public Vector3 GroundNormal { get; private set; } = Vector3.up;

    // 현재 감지된 지면의 경사각
    public float SlopeAngle { get; private set; }

    // groundCheckPoint기준 바닥까지의 거리
    // 지면 접촉 여부와 착지 근접 판정에 사용함
    public float DistanceToGround { get; private set; } = Mathf.Infinity;

    // Raycast가 지면을 감지했는지 여부
    public bool HasGroundHit { get; private set; }
    
    // 플레이어가 이동 가능한 지면 위에 있는지 여부
    // 거리 조건과 경사각 조건을 같이 확인함
    public bool IsGrounded { get; private set; }

    // 착지 판단에 사용할 정도로 지면과 가까운지 여부 
    public bool IsNearGround { get; private set; }

    // 감지된 지면이 플레이어가 걸을 수 있는 경사면인지 여부
    public bool IsWalkableSlope { get; private set; }


    /// <summary>
    /// Raycast를 이용해 지면 정보 갱신
    /// CharacterController의 기본 접지 판정만으로 부족한 거리, 법선, 경사 정보를 별도로 계산
    /// </summary>
    public void CheckGround()
    {
        if (groundCheckPoint == null)
        {
            ResetGroundInfo();
            return;
        }

        Vector3 origin = groundCheckPoint.position;
        Vector3 direction = Vector3.down;

        Debug.DrawRay(origin, direction * maxGroundCheckDistance, Color.red);

        bool isHit = Physics.Raycast(
            origin,
            direction,
            out RaycastHit hit,
            maxGroundCheckDistance,
            groundLayer,
            QueryTriggerInteraction.Ignore
        );

        HasGroundHit = isHit;

        if (isHit)
        {
            DistanceToGround = hit.distance;
            GroundNormal = hit.normal;

            // 지면 법선과 위쪽 방향 사이의 각도로 경사각을 계산
            SlopeAngle = Vector3.Angle(Vector3.up, hit.normal);

            // 감지된 지면이 설정한 최대 경사각 안에 있는지 확인 
            IsWalkableSlope = SlopeAngle <= maxSlopeAngle;

            // Raycast로 감지된 지면이 착지 가능한 거리 안에 있고, 경사각도 걷기 가능한 범위 내에 있는지 확인
            IsGrounded = DistanceToGround <= groundedDistance && IsWalkableSlope;

            // 낙하 중 착지 전환 판단에 사용할 근접 지면 판정
            IsNearGround = DistanceToGround <= landingCheckDistance;

            Debug.DrawRay(hit.point, GroundNormal * 0.7f, Color.blue);
        }
        else
        {
            ResetGroundInfo();
        }
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
    /// Scene 뷰에서 지면 체크 범위를 확인하기 위한 Gizmo 표시
    /// </summary>
    private void OnDrawGizmos()
    {
        if (groundCheckPoint == null) return;

        Gizmos.color = IsGrounded ? Color.green : Color.red;
        Gizmos.DrawLine(
            groundCheckPoint.position,
            groundCheckPoint.position + Vector3.down * groundedDistance
        );
    }
}