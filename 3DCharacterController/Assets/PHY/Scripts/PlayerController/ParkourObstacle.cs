using System.Collections.Generic;
using UnityEngine;

public class ParkourObstacle : MonoBehaviour
{
    [Header("Parkour Settings")]
    [SerializeField]
    private ParkourType parkourType = ParkourType.Vault;

    [SerializeField]
    private Transform startPoint;

    [Header("Vault 중 충돌을 무시할 '실제 몸통' 콜라이더 (isTrigger 꺼진 것들)")]
    [SerializeField]
    private Collider[] solidColliders;

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
}