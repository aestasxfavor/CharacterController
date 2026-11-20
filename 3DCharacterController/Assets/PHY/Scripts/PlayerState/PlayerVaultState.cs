using UnityEngine;

/// <summary>
/// Vault 상태.
/// 1단계 Aligning : 달리기 모션을 유지한 채 StartPoint 위치/방향으로 부드럽게 이동
/// 2단계 Playing  : Vault 모션 시작 + Root Motion으로 이동
/// 상태 진입 시 장애물 몸통 콜라이더와 충돌을 무시하고, 나갈 때 복구한다.
/// </summary>
public class PlayerVaultState : PlayerBaseState
{
    private const string VaultStateName = "Vault";

    // 정렬 이동 속도(m/s). 거리 / 속도 = 정렬 시간
    private const float AlignSpeed = 6f;
    private const float MinAlignDuration = 0.05f;
    private const float MaxAlignDuration = 0.25f;

    private enum Phase { Aligning, Playing }

    private Phase phase;
    private bool isFinishing;

    // ParkourController가 판정 시점의 장애물을 넘겨준다
    // (Vault 도중 트리거를 벗어나도 유지됨)
    private readonly ParkourObstacle obstacle;
    private bool collisionIgnored;

    private Vector3 alignStartPos;
    private Vector3 alignTargetPos;
    private Quaternion alignStartRot;
    private Quaternion alignTargetRot;
    private float alignElapsed;
    private float alignDuration;

    // PlayerController가 정렬 중 루트모션을 무시하기 위해 참조
    public bool IsAligning => phase == Phase.Aligning;

    public PlayerVaultState(
        PlayerController _playerController,
        PlayerStateMachine _playerStateMachine,
        ParkourObstacle _obstacle)
        : base(_playerController, _playerStateMachine)
    {
        obstacle = _obstacle;
    }

    public override void Enter()
    {
        isFinishing = false;

        playerController.PlayerAnimationController.SetAirborne(false);
        playerController.PlayerAnimationController.SetLanding(false);

        if (obstacle != null)
            SetObstacleCollision(true);

        if (PrepareAlignment())
            phase = Phase.Aligning;
        else
            BeginMotion();

        Debug.Log("[Vault] Enter");
    }

    /// <summary>
    /// 정렬 목표 계산. 정렬이 필요 없으면 false.
    /// </summary>
    private bool PrepareAlignment()
    {
        if (obstacle == null || obstacle.StartPoint == null)
            return false;

        Transform sp = obstacle.StartPoint;
        Transform player = playerController.transform;

        Vector3 forward = sp.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude < 0.001f)
            return false;

        alignStartPos = player.position;
        alignStartRot = player.rotation;

        // Y는 현재 높이 유지 (바닥 높이는 CharacterController가 처리)
        alignTargetPos = new Vector3(
            sp.position.x,
            player.position.y,
            sp.position.z);

        alignTargetRot = Quaternion.LookRotation(forward.normalized);

        Vector3 flat = alignTargetPos - alignStartPos;
        flat.y = 0f;

        float dist = flat.magnitude;
        float angle = Quaternion.Angle(alignStartRot, alignTargetRot);

        if (dist < 0.02f && angle < 1f)
            return false;

        alignDuration = Mathf.Clamp(
            dist / AlignSpeed,
            MinAlignDuration,
            MaxAlignDuration);

        alignElapsed = 0f;
        return true;
    }

    private void UpdateAlignment()
    {
        alignElapsed += Time.deltaTime;

        float t = Mathf.Clamp01(alignElapsed / alignDuration);
        float smooth = Mathf.SmoothStep(0f, 1f, t);

        Transform player = playerController.transform;

        Vector3 desired = Vector3.Lerp(alignStartPos, alignTargetPos, smooth);
        Vector3 delta = desired - player.position;
        delta.y = 0f;

        playerController.CharacterController.Move(delta);

        player.rotation = Quaternion.Slerp(
            alignStartRot,
            alignTargetRot,
            smooth);

        if (t >= 1f)
            BeginMotion();
    }

    private void BeginMotion()
    {
        phase = Phase.Playing;

        playerController.PlayerAnimationController.SetMotion(
            PlayerMotion.Vault);

        Debug.Log("[Vault] Motion Start");
    }

    private void SetObstacleCollision(bool ignore)
    {
        if (obstacle == null) return;
        if (collisionIgnored == ignore) return;

        CharacterController cc = playerController.CharacterController;
        Collider[] solids = obstacle.SolidColliders;

        if (cc == null || solids == null) return;

        foreach (Collider col in solids)
        {
            if (col == null) continue;
            Physics.IgnoreCollision(cc, col, ignore);
        }

        collisionIgnored = ignore;
    }

    public override void Update()
    {
        if (isFinishing)
            return;

        if (phase == Phase.Aligning)
        {
            UpdateAlignment();
            return;
        }

        bool isComplete =
            playerController.PlayerAnimationController
                .IsMotionComplete(VaultStateName);

        if (!isComplete)
            return;

        FinishVault();
    }

    private void FinishVault()
    {
        if (isFinishing)
            return;

        isFinishing = true;

        Debug.Log("[Vault] Animation Complete");

        playerController.GroundDetector.CheckGround();

        bool isGrounded =
            playerController.GroundDetector.IsGrounded &&
            playerController.GroundDetector.IsWalkableSlope;

        if (!isGrounded)
        {
            playerStateMachine.ChangeState(
                new PlayerFallState(
                    playerController,
                    playerStateMachine,
                    false));
            return;
        }

        bool hasMoveInput =
            playerController.InputHandler.Movement.sqrMagnitude > 0.01f;

        if (hasMoveInput)
        {
            playerStateMachine.ChangeState(
                new PlayerMoveState(
                    playerController,
                    playerStateMachine));
        }
        else
        {
            playerStateMachine.ChangeState(
                new PlayerIdleState(
                    playerController,
                    playerStateMachine));
        }
    }

    public override void Exit()
    {
        // 어떤 경로로 빠져나가든 반드시 충돌 복구
        SetObstacleCollision(false);

        Debug.Log("[Vault] Exit");
    }
}