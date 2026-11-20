using UnityEngine;

/// <summary>
/// 파쿠르 시작 진입점.
/// Idle / Move (이후 Fall 등) 상태가 공통으로 호출한다.
/// 감지(ParkourDetector) → 판정(ParkourConditionChecker) → 상태 생성 순서로 처리한다.
/// </summary>
public class ParkourController : MonoBehaviour
{
    [Header("Components")]
    [SerializeField]
    private ParkourDetector parkourDetector;

    [SerializeField]
    private ParkourConditionChecker conditionChecker;

    [Header("Debug")]
    [SerializeField]
    private bool showDebugLog = true;

    public ParkourDetector Detector => parkourDetector;

    private void Reset()
    {
        FindReferences();
    }

    private void Awake()
    {
        FindReferences();
    }

    private void FindReferences()
    {
        if (parkourDetector == null)
            parkourDetector = GetComponent<ParkourDetector>();

        if (conditionChecker == null)
            conditionChecker = GetComponent<ParkourConditionChecker>();
    }

    /// <summary>
    /// 파쿠르 실행을 시도한다.
    /// 성공하면 상태를 전이하고 true, 실패하면 아무것도 하지 않고 false.
    /// 호출한 상태는 false일 때 원래 하던 처리(점프 등)를 이어서 한다.
    /// </summary>
    /// <param name="fromState">로그용 호출 상태 이름 (예: "Idle", "Move")</param>
    public bool TryStartParkour(
        PlayerController playerController,
        PlayerStateMachine playerStateMachine,
        string fromState)
    {
        if (parkourDetector == null || conditionChecker == null)
            return false;

        if (!parkourDetector.HasObstacle)
            return false;

        if (!conditionChecker.CanExecute(out string reason))
        {
            Log($"[Parkour] {fromState} 실행 불가: {reason}");
            return false;
        }

        ParkourObstacle obstacle = parkourDetector.CurrentObstacle;

        PlayerBaseState nextState = CreateState(
            obstacle,
            playerController,
            playerStateMachine);

        if (nextState == null)
        {
            Log($"[Parkour] {fromState} 실행 불가: {obstacle.Type} 상태 미구현");
            return false;
        }

        Log($"[Parkour] {fromState} -> {obstacle.Type}");

        playerStateMachine.ChangeState(nextState);
        return true;
    }

    /// <summary>
    /// 파쿠르 타입별 상태 생성.
    /// 새 동작을 추가할 때는 여기에 case 하나만 추가하면 된다.
    /// </summary>
    private PlayerBaseState CreateState(
        ParkourObstacle obstacle,
        PlayerController playerController,
        PlayerStateMachine playerStateMachine)
    {
        switch (obstacle.Type)
        {
            case ParkourType.Vault:
                return new PlayerVaultState(
                    playerController,
                    playerStateMachine,
                    obstacle);

            // case ParkourType.Mantle:
            //     return new PlayerMantleState(
            //         playerController,
            //         playerStateMachine,
            //         obstacle);

            default:
                return null;
        }
    }

    private void Log(string message)
    {
        if (showDebugLog)
            Debug.Log(message);
    }
}