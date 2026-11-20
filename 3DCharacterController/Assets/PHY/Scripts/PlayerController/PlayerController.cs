
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Components")]
    [SerializeField]
    private CharacterController characterController;

    [SerializeField]
    private PlayerAnimationController playerAnimationController;

    private Transform cameraTransform;

    [Header("Controllers")]
    [SerializeField] private InputHandler inputHandler;
    [SerializeField] private MovementController movementController;
    [SerializeField] private JumpController jumpController;
    [SerializeField] private GroundDetector groundDetector;
    [SerializeField] private HighFallDetector highFallDetector;
    [SerializeField] private ParkourController parkourController;

    [Header("StatePatterns")]
    [SerializeField] private PlayerStateMachine playerStateMachine;

    [Header("Air State")]
    // FSM 낙하 진입을 위한 유예 시간
    // 수평 속도 보존 시점과는 별도로 관리
    [SerializeField] private float fallEnterDelay = 0.08f;

    [Header("Rolling")]
    // 구르기 상태 유지 시간
    [SerializeField, Min(0.05f)]
    private float rollingDuration = 0.8f;

    public float RollingDuration => rollingDuration;

    [Header("Debug")]
    [SerializeField] private bool showDebugLog = true;

    public CharacterController CharacterController => characterController;

    public PlayerAnimationController PlayerAnimationController => playerAnimationController;

    public InputHandler InputHandler => inputHandler;
    public MovementController MovementController => movementController;
    public JumpController JumpController => jumpController;
    public GroundDetector GroundDetector => groundDetector;
    public HighFallDetector HighFallDetector => highFallDetector;

    public ParkourController ParkourController => parkourController;
    // 마지막으로 지면에 지지된 이후 경과 시간
    public float UnsupportedTime { get; private set; }

    // 기존 FSM 낙하 진입 판정 유지
    public bool ShouldStartFalling =>
        !jumpController.IsJumping &&
        UnsupportedTime >= fallEnterDelay;

    public float JumpStartY => jumpStartY;
    private float jumpStartY;

    public bool WasGrounded => wasGrounded;
    private bool wasGrounded;

    public bool ControllerGroundedAfterMove
    {
        get;
        private set;
    }

    // 추가: Vault 실행 여부 확인
    public bool IsVaultActive =>
        playerStateMachine != null &&
        playerStateMachine.CurrentState is PlayerVaultState;

    private void Reset()
    {
        FindRefernces();
    }

    private void Awake()
    {
        FindRefernces();
    }

    private void Start()
    {
        groundDetector.CheckGround();
        wasGrounded = groundDetector.IsGrounded;

        if (playerStateMachine != null)
        {
            playerStateMachine.ChangeState(
                new PlayerIdleState(
                    this,
                    playerStateMachine));
        }
    }

    public void SetCameraTransform(
        Transform newCameraTransform)
    {
        cameraTransform = newCameraTransform;
    }

    private void Update()
    {
        if (!IsReady())
            return;

        // 추가: Vault 중 일반 이동과 중력 처리 제외
        if (IsVaultActive)
        {
            groundDetector.CheckGround();

            // 애니메이션 완료 및 FSM 복귀 판정 유지
            playerStateMachine.UpdateState();
            return;
        }

        // 이전 프레임의 실제 Move 접지 결과 보관
        bool previousMoveGrounded =
            ControllerGroundedAfterMove;

        // 현재 프레임의 Move 결과는 이후 새로 기록
        ControllerGroundedAfterMove = false;

        wasGrounded = groundDetector.IsGrounded;

        // 현재 프레임의 이동 전에 지면 상태 검사
        groundDetector.CheckGround();

        // 이전 Move 결과를 기준으로 수직 이동 처리
        jumpController.UpdateVertical(
            previousMoveGrounded,
            Time.deltaTime);

        // 동일한 접지 기준으로 입력 잠금 갱신
        UpdateFallMovementLock(previousMoveGrounded);

        // 실제 수평 및 수직 이동
        ProcessMovement();

        // 기존 FSM 업데이트 순서 유지
        playerStateMachine.UpdateState();
    }

    // 추가: Vault 애니메이션의 이동량을
    // CharacterController에 적용
    public void ApplyVaultRootMotion(Vector3 deltaPosition)
    {

        // 디버그 확인용
        //Animator a = GetComponentInChildren<Animator>();
        //Debug.Log($"[VaultRM] t={Time.time:F3} delta={deltaPosition.magnitude:F3} inTrans={a.IsInTransition(0)} vaultState={a.GetCurrentAnimatorStateInfo(0).IsName("Vault")}");
        
        if (!IsVaultActive || characterController == null)
            return;

        CollisionFlags flags =
            characterController.Move(deltaPosition);

        bool hitBelow =
            (flags & CollisionFlags.Below) != 0;

        // Root Motion 이동 후 접지 정보 갱신
        groundDetector.CheckGround();

        ControllerGroundedAfterMove =
            hitBelow &&
            groundDetector.IsGrounded;

        UnsupportedTime =
            IsSupportedByGround()
                ? 0f
                : UnsupportedTime + Time.deltaTime;
    }

    /// <summary>
    /// 점프 하강과 플랫폼 이탈 시 수평 이동 입력 제어
    /// 플랫폼 이탈 시 관성 보존은 FSM 낙하 진입보다 먼저 적용
    /// </summary>
    private void UpdateFallMovementLock(
        bool previousMoveGrounded)
    {
        // Rolling 중에는 접지 후에도 LOCK 유지
        if (playerStateMachine != null &&
            playerStateMachine.CurrentState is PlayerRollingState)
        {
            movementController.SetFallMovementLocked(true);
            return;
        }

        // 실제 점프가 아닌 상태에서 지면을 지지하면 잠금 해제
        if (groundDetector.IsGrounded &&
            previousMoveGrounded &&
            !jumpController.IsJumping)
        {
            movementController.SetFallMovementLocked(false);
            return;
        }

        // 수직 속도가 음수일 때만 하강으로 판단
        bool isDescending =
            jumpController.VerticalVelocity < 0f;

        // 기존 점프는 상승 중 입력 제어 유지
        // 하강 중에만 LOCK + KEEP 적용
        bool descendingFromJump =
            jumpController.IsJumping &&
            !previousMoveGrounded;

        // 플랫폼 이탈은 지면 감지기가
        // 지지 상실을 확인하는 순간부터 관성 보존
        bool descendingFromLedge =
            !jumpController.IsJumping &&
            !groundDetector.IsGrounded;

        bool shouldLock =
            isDescending &&
            (descendingFromJump || descendingFromLedge);

        movementController.SetFallMovementLocked(shouldLock);
    }

    private void ProcessMovement()
    {
        Vector3 moveDirection =
            movementController.GetMoveDirection(
                inputHandler.Movement,
                cameraTransform);

        bool hasMoveInput =
            moveDirection.sqrMagnitude > 0.01f;

        bool isRunPressed =
            inputHandler.IsRunPressed;

        playerAnimationController.SetMoveInput(
            hasMoveInput);

        Vector3 horizontalMove =
            movementController.GetHorizontalMove(
                moveDirection,
                Time.deltaTime,
                groundDetector.IsGrounded,
                isRunPressed);

        Vector3 finalMove = horizontalMove;

        finalMove.y =
            jumpController.VerticalVelocity;

        // CharacterController 이동 및 충돌 판정
        CollisionFlags flags =
            characterController.Move(
                finalMove * Time.deltaTime);

        bool hitBelow = (flags & CollisionFlags.Below) != 0;

        // 이동 후 지면 상태 갱신
        groundDetector.CheckGround();

        ControllerGroundedAfterMove =
            hitBelow &&
            groundDetector.IsGrounded;

        // FSM 낙하 상태 진입용 시간 갱신
        UnsupportedTime =
            IsSupportedByGround()
                ? 0f
                : UnsupportedTime + Time.deltaTime;

        bool isRolling =
            playerStateMachine != null &&
            playerStateMachine.CurrentState is PlayerRollingState;

        // Rolling 중에는 캐릭터 회전 제한
        // 기존 지상 이동 회전은 유지
        if (!isRolling &&
            hasMoveInput &&
            groundDetector.IsGrounded)
        {
            movementController.Rotate(
                transform,
                moveDirection,
                Time.deltaTime);
        }
    }

    /// <summary>
    /// 기존 지면 지지 판정
    /// GroundDetector와 CharacterController 접지 상태 활용
    /// </summary>
    private bool IsSupportedByGround()
    {
        if (groundDetector.IsGrounded)
        {
            return true;
        }

        if (!characterController.isGrounded)
        {
            return false;
        }

        return !groundDetector.HasGroundHit ||
               groundDetector.IsWalkableSlope;
    }

    public void SetJumpStartY(float value)
    {
        jumpStartY = value;
    }

    private bool IsReady()
    {
        return characterController != null
            && inputHandler != null
            && movementController != null
            && jumpController != null
            && groundDetector != null
            && highFallDetector != null
            && playerAnimationController != null
            && cameraTransform != null;
    }

    private void FindRefernces()
    {
        if (characterController == null)
        {
            characterController = GetComponent<CharacterController>();
        }

        if (inputHandler == null)
        {
            inputHandler = GetComponent<InputHandler>();
        }

        if (movementController == null)
        {
            movementController = GetComponent<MovementController>();
        }

        if (jumpController == null)
        {
            jumpController = GetComponent<JumpController>();
        }

        if (groundDetector == null)
        {
            groundDetector = GetComponent<GroundDetector>();
        }

        if (highFallDetector == null)
        {
            highFallDetector = GetComponent<HighFallDetector>();
        }

        if (playerAnimationController == null)
        {
            playerAnimationController = GetComponent<PlayerAnimationController>();
        }

        if (playerStateMachine == null)
        {
            playerStateMachine = GetComponent<PlayerStateMachine>();
        }

        if (parkourController == null)
        {
            parkourController = GetComponent<ParkourController>();

        }
    }
}
