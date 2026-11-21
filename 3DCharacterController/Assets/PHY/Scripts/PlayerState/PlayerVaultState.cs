using UnityEngine;

/// <summary>
/// Vault 상태.
///
/// 1단계 Aligning : 달리기 모션을 유지한 채 StartPoint 위치/방향으로 부드럽게 이동
/// 2단계 Playing  : Vault 모션 시작 + Root Motion으로 이동
///
/// 종료 규칙
///  - 착지 이벤트(OnParkourExitWindow) 전   : 입력과 관계없이 유지
///  - 착지 이벤트 ~ 클립 끝                 : 이동 입력이 들어오는 즉시 Move로 전환
///  - 클립 끝                               : 입력이 없으면 Idle, 있으면 Move
///  - 지면 위가 아니면                      : Fall
///
/// Move로 넘어갈 때는 클립 루트모션 속도의 크기와 캐릭터 정면 방향으로
/// MovementController 속도를 맞춘 뒤, 이후는 기존 가속 로직에 맡긴다.
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
        playerController.PlayerAnimationController.ResetParkourExitWindow();

        if (obstacle != null)
            SetObstacleCollision(true);

        if (PrepareAlignment())
            phase = Phase.Aligning;
        else
            BeginMotion();

        Debug.Log("[Vault] Enter");
    }

    // ───────────── 정렬 ─────────────

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

        // 좌우 위치는 플레이어의 현재 위치를 유지하고,
        // StartPoint 기준선(앞뒤 거리)과 방향에만 맞춘다.
        // Y는 현재 높이 유지 (바닥 높이는 CharacterController가 처리)
        Vector3 right = Vector3.Cross(Vector3.up, forward.normalized);

        Vector3 toPlayer = player.position - sp.position;
        toPlayer.y = 0f;

        float lateral = Vector3.Dot(toPlayer, right);

        Vector3 basePos = sp.position + right * lateral;

        alignTargetPos = new Vector3(
            basePos.x,
            player.position.y,
            basePos.z);

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

    // ───────────── 모션 ─────────────

    private void BeginMotion()
    {
        phase = Phase.Playing;

        // 이전 파쿠르에서 남은 이벤트 플래그 초기화
        playerController.PlayerAnimationController.ResetParkourExitWindow();

        playerController.PlayerAnimationController.SetMotion(
            PlayerMotion.Vault);

        Debug.Log("[Vault] Motion Start");
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

        PlayerAnimationController anim =
            playerController.PlayerAnimationController;

        bool windowOpened = anim.ParkourExitWindowOpened;
        bool clipComplete = anim.IsMotionComplete(VaultStateName);

        // 착지 이벤트 전이면 입력과 관계없이 유지
        // (이벤트를 안 넣은 클립은 클립 끝에서 종료되어 안전장치가 된다)
        if (!windowOpened && !clipComplete)
            return;

        bool hasMoveInput =
            playerController.InputHandler.Movement.sqrMagnitude > 0.01f;

        // 종료 구간 안에서는 입력이 들어오면 즉시 전환,
        // 입력이 없으면 클립 끝까지 재생
        if (!clipComplete && !hasMoveInput)
            return;

        FinishVault(hasMoveInput);
    }

    // ───────────── 종료 ─────────────

    private void FinishVault(bool hasMoveInput)
    {
        if (isFinishing)
            return;

        isFinishing = true;

        playerController.GroundDetector.CheckGround();

        bool isGrounded =
            playerController.GroundDetector.IsGrounded &&
            playerController.GroundDetector.IsWalkableSlope;

        if (!isGrounded)
        {
            Debug.Log("[Vault] Exit -> Fall");

            playerStateMachine.ChangeState(
                new PlayerFallState(
                    playerController,
                    playerStateMachine,
                    false));

            return;
        }

        if (hasMoveInput)
        {
            // 클립 속도의 크기 + 캐릭터 정면 방향으로 속도를 맞춘다.
            // 이후는 MovementController가 설정 속도까지 가속한다.
            Vector3 rootVelocity =
                playerController.PlayerAnimationController.RootMotionVelocity;

            rootVelocity.y = 0f;

            Vector3 forward = playerController.transform.forward;
            forward.y = 0f;
            forward.Normalize();

            float speed = rootVelocity.magnitude;

            playerController.MovementController
                .SetHorizontalVelocity(forward * speed);

            Debug.Log($"[Vault] Exit -> Move (속도 인계 {speed:F2} m/s)");

            playerStateMachine.ChangeState(
                new PlayerMoveState(
                    playerController,
                    playerStateMachine));
        }
        else
        {
            playerController.MovementController
                .SetHorizontalVelocity(Vector3.zero);

            Debug.Log("[Vault] Exit -> Idle");

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

        playerController.PlayerAnimationController.ResetParkourExitWindow();

        Debug.Log("[Vault] Exit");
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
}