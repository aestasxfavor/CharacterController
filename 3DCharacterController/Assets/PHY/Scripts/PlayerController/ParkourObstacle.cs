
using UnityEngine;

public class ParkourObstacle : MonoBehaviour
{
    [Header("Parkour Settings")]
    [SerializeField]
    private ParkourType parkourType = ParkourType.Vault;

    [SerializeField]
    private Transform startPoint;

    public ParkourType Type => parkourType;
    public Transform StartPoint => startPoint;
}
