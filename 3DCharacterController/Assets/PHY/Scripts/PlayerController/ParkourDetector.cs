
using UnityEngine;

public class ParkourDetector : MonoBehaviour
{
    [Header("Detection Settings")]
    [SerializeField]
    private LayerMask obstacleLayer;

    [Header("Debug")]
    [SerializeField]
    private bool showDebugLog = true;

    public ParkourObstacle CurrentObstacle
    {
        get;
        private set;
    }

    private Collider currentTrigger;

    public bool HasObstacle => CurrentObstacle != null;

    private void OnTriggerEnter(Collider other)
    {
        if (!IsObstacleLayer(other.gameObject.layer))
            return;

        ParkourObstacle obstacle = FindObstacle(other);

        if (obstacle == null)
            return;

        SetCurrentObstacle(obstacle, other);
    }

    private void OnTriggerExit(Collider other)
    {
        if (other != currentTrigger)
            return;

        ClearCurrentObstacle();
    }

    private bool IsObstacleLayer(int layer)
    {
        return (obstacleLayer.value & (1 << layer)) != 0;
    }

    private ParkourObstacle FindObstacle(Collider other)
    {
        if (other.TryGetComponent(out ParkourObstacle obstacle))
        {
            return obstacle;
        }

        return other.GetComponentInParent<ParkourObstacle>();
    }

    private void SetCurrentObstacle(ParkourObstacle obstacle, Collider trigger)
    {
        if (CurrentObstacle == obstacle && currentTrigger == trigger)
        {
            return;
        }

        CurrentObstacle = obstacle;
        currentTrigger = trigger;

        if (showDebugLog)
        {
            Debug.Log(
                $"[ParkourDetector] 감지: " + $"{obstacle.name} / {obstacle.Type}");
        }
    }

    private void ClearCurrentObstacle()
    {
        if (CurrentObstacle == null &&
            currentTrigger == null)
        {
            return;
        }

        CurrentObstacle = null;
        currentTrigger = null;

        if (showDebugLog)
        {
            Debug.Log("[ParkourDetector] 파쿠르 장애물 감지 해제");
        }
    }

    private void OnDisable()
    {
        CurrentObstacle = null;
        currentTrigger = null;
    }
}
