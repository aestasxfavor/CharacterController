using UnityEngine;

/// <summary>
/// Mantle 상태 (170cm 장애물을 올라가 윗면에 선다).
///
/// 1단계 Aligning : 좌우는 유지한 채 StartPoint 위치/방향으로 부드럽게 이동
/// 2단계 Playing  : Mantle 모션 시작 + 루트모션으로 이동 (Y 포함)
///
/// 종료 규칙 (Vault와 동일)
///  - 종료 이벤트(OnParkourExitWindow) 전 : 입력과 상관없이 유지
///  - 종료 이벤트 ~ 클립 끝               : 이동 입력이 들어오면 즉시 Move
///  - 클립 끝                             : 입력이 없으면 Idle, 있으면 Move
///  - 지면 위가 아니면                    : Fall
///
/// Vault와 다른 점
///  - 종료 직전에 장애물 윗면 높이로 Y를 보정한 뒤 충돌을 복구한다.
///  - 지면 확인에 통과하면 PlayerController의 지면 상태를 리셋한다.
/// </summary>
public class PlayerMantleState : PlayerParkourState
{
    private const string MantleStateName = "Mantle";

    // 정렬 이동 속도(m/s). 거리 / 속도 = 정렬 시간
    private const float AlignSpeed = 6f;
    private const float MinAlignDuration = 0.05f;
    private const float MaxAlignDuration = 0.25f;

    // 윗면 스냅: 캡슐 바닥이 윗면 + 이 값 높이가 되도록 보정
    private const float SnapClearance = 0.02f;
    // 보정 거리가 이 값을 넘으면 스냅하지 않고 경고만 남긴다 (높이 불일치 감지용)
    private const float MaxSnapDistance = 0.15f;

    private enum Phase { Aligning, Playing }

    private Phase phase;
    private bool isFinishing;

    private readonly ParkourObstacle obstacle;
    private bool collisionIgnored;

    private Vector3 alignStartPos;
    private Vector3 alignTargetPos;
    private Quaternion alignStartRot;
    private Quaternion alignTargetRot;
    private float alignElapsed;
    private float alignDuration;

    public override bool IsAligning => phase == Phase.Aligning;

    public PlayerMantleState(
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

        Debug.Log("[Mantle] Enter");
    }

    // ---------- 정렬 ----------

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

        // 좌우 위치는 유지하고, StartPoint 기준선(앞뒤 거리)과 방향만 맞춘다.
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

    // ---------- 모션 ----------

    private void BeginMotion()
    {
        phase = Phase.Playing;

        // 정렬 구간에서 들어온 이벤트 플래그 초기화
        playerController.PlayerAnimationController.ResetParkourExitWindow();

        playerController.PlayerAnimationController.SetMotion(
            PlayerMotion.Mantle);

        Debug.Log("[Mantle] Motion Start");
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
        bool clipComplete = anim.IsMotionComplete(MantleStateName);

        // 종료 이벤트 전이면 입력과 상관없이 유지
        if (!windowOpened && !clipComplete)
            return;

        bool hasMoveInput =
            playerController.InputHandler.Movement.sqrMagnitude > 0.01f;

        // 종료 구간 안에서는 입력이 있을 때만 즉시 전환,
        // 입력이 없으면 클립 끝까지 대기
        if (!clipComplete && !hasMoveInput)
            return;

        FinishMantle(hasMoveInput);
    }

    // ---------- 종료 ----------

    private void FinishMantle(bool hasMoveInput)
    {
        if (isFinishing)
            return;

        isFinishing = true;

        // 충돌 복구(Exit) 전에 윗면 높이로 보정해서 파묻힘/튕김을 막는다.
        SnapToSurface();

        playerController.GroundDetector.CheckGround();

        bool isGrounded =
            playerController.GroundDetector.IsGrounded &&
            playerController.GroundDetector.IsWalkableSlope;

        if (!isGrounded)
        {
            Debug.Log("[Mantle] Exit -> Fall");

            playerStateMachine.ChangeState(
                new PlayerFallState(
                    playerController,
                    playerStateMachine,
                    false));

            return;
        }

        // 오르는 동안 쌓인 공중 상태(UnsupportedTime 등)를 정리
        playerController.ResetGroundedAfterParkour();

        if (hasMoveInput)
        {
            Vector3 rootVelocity =
                playerController.PlayerAnimationController.RootMotionVelocity;

            rootVelocity.y = 0f;

            Vector3 forward = playerController.transform.forward;
            forward.y = 0f;
            forward.Normalize();

            float speed = rootVelocity.magnitude;

            playerController.MovementController
                .SetHorizontalVelocity(forward * speed);

            Debug.Log($"[Mantle] Exit -> Move (속도 인계 {speed:F2} m/s)");

            playerStateMachine.ChangeState(
                new PlayerMoveState(
                    playerController,
                    playerStateMachine));
        }
        else
        {
            playerController.MovementController
                .SetHorizontalVelocity(Vector3.zero);

            Debug.Log("[Mantle] Exit -> Idle");

            playerStateMachine.ChangeState(
                new PlayerIdleState(
                    playerController,
                    playerStateMachine));
        }
    }

    /// <summary>
    /// 캡슐 바닥을 장애물 윗면 높이에 맞춘다. 충돌 무시 상태에서 호출해야 한다.
    /// </summary>
    private void SnapToSurface()
    {
        if (obstacle == null)
            return;

        Collider[] solids = obstacle.SolidColliders;

        if (solids == null || solids.Length == 0)
            return;

        float topY = float.NegativeInfinity;

        foreach (Collider col in solids)
        {
            if (col == null) continue;
            topY = Mathf.Max(topY, col.bounds.max.y);
        }

        if (float.IsNegativeInfinity(topY))
            return;

        CharacterController cc = playerController.CharacterController;

        float bottomY = cc.bounds.min.y;
        // 변경 후
        float delta = topY - cc.transform.position.y;

        Debug.Log($"[Mantle] 윗면 {topY:F3} / 캡슐 바닥 {bottomY:F3} / 보정 {delta:F3}");
        LogFinishDebug(topY);

        if (Mathf.Abs(delta) > MaxSnapDistance)
        {
            Debug.LogWarning($"[Mantle] 높이 불일치: 보정 {delta:F3}m가 한도 {MaxSnapDistance}m 초과, 스냅 생략");
            return;
        }

        cc.Move(Vector3.up * delta);
    }

    /// <summary>
    /// 캐릭터 발 파묻힘 문제 디버깅용 로그. Mantle 종료 직전에 호출한다.
    /// </summary>
    /// <param name="topY"></param>
    private void LogFinishDebug(float topY)
    {
        Transform player = playerController.transform;
        CharacterController cc = playerController.CharacterController;
        Animator animator =
            playerController.PlayerAnimationController.GetComponent<Animator>();

        float pivotY = player.position.y;
        float capsuleBottom = cc.bounds.min.y;

        string along = "n/a";
        if (obstacle != null && obstacle.StartPoint != null)
        {
            Vector3 fwd = obstacle.StartPoint.forward;
            fwd.y = 0f;
            fwd.Normalize();

            Vector3 d = player.position - obstacle.StartPoint.position;
            d.y = 0f;

            along = Vector3.Dot(d, fwd).ToString("F3");
        }

        string toes = "n/a";
        if (animator != null)
        {
            Transform l = animator.GetBoneTransform(HumanBodyBones.LeftToes);
            Transform r = animator.GetBoneTransform(HumanBodyBones.RightToes);

            if (l != null && r != null)
                toes = $"L {l.position.y:F3} / R {r.position.y:F3}";
        }

        Debug.Log(
            $"[Mantle] 진단 | 피벗Y {pivotY:F3} / 캡슐바닥 {capsuleBottom:F3} / " +
            $"StartPoint 기준 전진 {along}m / 발끝Y {toes} / 윗면 {topY:F3}");
    }

    public override void Exit()
    {
        // 어떤 경로로 빠져나가든 반드시 충돌 복구
        SetObstacleCollision(false);

        playerController.PlayerAnimationController.ResetParkourExitWindow();

        Debug.Log("[Mantle] Exit");
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