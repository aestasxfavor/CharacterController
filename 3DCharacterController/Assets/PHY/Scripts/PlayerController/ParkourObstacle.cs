using System.Collections.Generic;
using UnityEngine;

public class ParkourObstacle : MonoBehaviour
{
    [Header("Parkour Settings")]
    [SerializeField]
    private ParkourType parkourType = ParkourType.Vault;

    // 앞뒤 거리 기준선 + 모션 진행 방향(forward)을 알려준다.
    // 좌우 위치는 정렬에 쓰이지 않지만, 판정 기준의 중심선이므로
    // 장애물 폭의 가운데(좌우 중앙)에 두는 것을 전제로 한다.
    [SerializeField]
    private Transform startPoint;

    [Header("Vault 중 충돌을 무시할 '실제 몸통' 콜라이더 (isTrigger 꺼진 것들)")]
    [SerializeField]
    private Collider[] solidColliders;

    [Header("Width")]
    // 0이면 solidColliders에서 자동 계산, 0보다 크면 이 값을 사용 (반폭, m)
    [SerializeField, Min(0f)]
    private float halfWidthOverride = 0f;

    public ParkourType Type => parkourType;
    public Transform StartPoint => startPoint;
    public Collider[] SolidColliders => solidColliders;

    private void Reset()
    {
        CollectSolidColliders();
    }

    private void Awake()
    {
        if (solidColliders == null || solidColliders.Length == 0)
            CollectSolidColliders();
    }

    // 인스펙터 우클릭 메뉴에서도 다시 수집 가능
    [ContextMenu("Collect Solid Colliders")]
    private void CollectSolidColliders()
    {
        List<Collider> list = new List<Collider>();

        foreach (Collider c in GetComponentsInChildren<Collider>())
        {
            // 디텍션 트리거는 제외, 진짜 막는 콜라이더만
            if (!c.isTrigger)
                list.Add(c);
        }

        solidColliders = list.ToArray();
    }

    /// <summary>
    /// 장애물의 반폭: 주어진 좌우 축(right) 방향으로의 반 길이(m).
    /// BoxCollider는 회전을 고려해서 정확히 계산하고,
    /// 그 외 콜라이더는 월드 bounds로 근사한다.
    /// </summary>
    public float GetHalfWidth(Vector3 right)
    {
        if (halfWidthOverride > 0f)
            return halfWidthOverride;

        if (solidColliders == null)
            return 0f;

        right.y = 0f;
        if (right.sqrMagnitude < 0.0001f)
            return 0f;

        right.Normalize();

        float max = 0f;

        foreach (Collider col in solidColliders)
        {
            if (col == null) continue;

            float h;

            if (col is BoxCollider box)
            {
                Transform t = box.transform;
                Vector3 s = t.lossyScale;

                Vector3 half = new Vector3(
                    Mathf.Abs(box.size.x * s.x),
                    Mathf.Abs(box.size.y * s.y),
                    Mathf.Abs(box.size.z * s.z)) * 0.5f;

                // 박스의 세 반축을 right 방향으로 투영해서 합산
                h = Mathf.Abs(Vector3.Dot(t.right, right)) * half.x
                  + Mathf.Abs(Vector3.Dot(t.up, right)) * half.y
                  + Mathf.Abs(Vector3.Dot(t.forward, right)) * half.z;
            }
            else
            {
                Vector3 e = col.bounds.extents;

                h = Mathf.Abs(right.x) * e.x
                  + Mathf.Abs(right.y) * e.y
                  + Mathf.Abs(right.z) * e.z;
            }

            if (h > max)
                max = h;
        }

        return max;
    }
}