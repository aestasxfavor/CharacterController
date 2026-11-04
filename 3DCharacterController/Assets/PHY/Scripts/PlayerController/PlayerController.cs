using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private CharacterController characterController;
    [SerializeField] private PlayerAnimationController playerAnimationController;
    private Transform cameraTransform;

    [Header("Controllers")]
    [SerializeField] private InputHandler inputHandler;
    [SerializeField] private MovementController movementController;
    [SerializeField] private JumpController jumpController;
    [SerializeField] private GroundDetector groundDetector;
    [SerializeField] private HighFallDetector highFallDetector;

    [Header("StatePatterns")]
    [SerializeField] private PlayerStateMachine playerStateMachine;

    [Header("Air State")]
    // 지면에 지지되지 않은 상태가 이 시간(초) 이상 이어지면 낙하로 판단
    // 모서리나 작은 턱에서 접지가 한두 프레임 끊기는 경우를 걸러내는 유예 시간
    [SerializeField] private float fallEnterDelay = 0.08f;

    [Header("Debug")]
    [SerializeField] private bool showDebugLog = true;

    public CharacterController CharacterController => characterController;
    public PlayerAnimationController PlayerAnimationController => playerAnimationController;
    public InputHandler InputHandler => inputHandler;
    public MovementController MovementController => movementController;
    public JumpController JumpController => jumpController;
    public GroundDetector GroundDetector => groundDetector;
    public HighFallDetector HighFallDetector => highFallDetector;

    // 마지막으로 지면에 지지된 이후 경과 시간 (지지 중이면 0)
    public float UnsupportedTime { get; private set; }

    // 점프 중이 아니고 일정 시간 이상 지지받지 못하면 낙하로 판단 (Idle / Move 공용)
    public bool ShouldStartFalling => !jumpController.IsJumping && UnsupportedTime >= fallEnterDelay;

    public float JumpStartY => jumpStartY;
    private float jumpStartY;

    public bool WasGrounded => wasGrounded;
    private bool wasGrounded;

    public bool ControllerGroundedAfterMove { get; private set; }

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
            playerStateMachine.ChangeState(new PlayerIdleState(this, playerStateMachine));
        }
    }

    public void SetCameraTransform(Transform newCameraTransform)
    {
        cameraTransform = newCameraTransform;
    }

    private void Update()
    {
        if (!IsReady()) return;

        ControllerGroundedAfterMove = false;

        wasGrounded = groundDetector.IsGrounded;

        groundDetector.CheckGround();

        // JumpController의 수직 속도 보정은 착지 반응이 즉시 필요하므로
        // Raycast 기반 GroundDetector가 아닌 CharacterController의 접지 판정을 사용한다.
        jumpController.UpdateVertical(characterController.isGrounded, Time.deltaTime);

        ProcessMovement();

        playerStateMachine.UpdateState();
    }

    private void ProcessMovement()
    {
        Vector3 moveDirection = movementController.GetMoveDirection(inputHandler.Movement, cameraTransform);

        bool hasMoveInput = moveDirection.sqrMagnitude > 0.01f;
        bool isRunPressed = inputHandler.IsRunPressed;

        playerAnimationController.SetMoveInput(hasMoveInput);

        Vector3 horizontalMove = movementController.GetHorizontalMove(moveDirection, Time.deltaTime,
            groundDetector.IsGrounded, isRunPressed);

        Vector3 finalMove = horizontalMove;
        finalMove.y = jumpController.VerticalVelocity;

        // CollisionFlags: Move 후 캐릭터 컨트롤러가 어떤 방향으로 충돌이 있었는지 알려주는 플래그
        CollisionFlags flags = characterController.Move(finalMove * Time.deltaTime);

        bool hitBelow = (flags & CollisionFlags.Below) != 0;

        groundDetector.CheckGround();

        // CharacterController.Move 이후 실제 아래 방향 충돌이 있었는지 확인해
        // FallState의 착지 전이 기준으로 사용한다.
        ControllerGroundedAfterMove = hitBelow && groundDetector.IsGrounded;

        UnsupportedTime = IsSupportedByGround() ? 0f : UnsupportedTime + Time.deltaTime;

        //if (hasMoveInput)
        //{
        //    Debug.Log($"IsGrounded: {groundDetector.IsGrounded}");
        //}

        if (hasMoveInput && groundDetector.IsGrounded)
        {
            movementController.Rotate(transform, moveDirection, Time.deltaTime);
        }

        
    }

    /// <summary>
    /// 현재 지면에 지지되고 있는지 판단
    /// GroundDetector가 접지로 보면 지지 중이고,
    /// 감지기가 지면을 못 찾았거나 걸을 수 있는 경사인데도 CharacterController가 접지라고 하면 지지 중으로 취급
    /// (걸을 수 없는 급경사 위에서는 CharacterController 접지를 인정하지 않음)
    /// </summary>
    private bool IsSupportedByGround()
    {
        if (groundDetector.IsGrounded) return true;

        if (!characterController.isGrounded) return false;

        return !groundDetector.HasGroundHit || groundDetector.IsWalkableSlope;
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
        if (characterController == null) characterController = GetComponent<CharacterController>();
        if (inputHandler == null) inputHandler = GetComponent<InputHandler>();
        if (movementController == null) movementController = GetComponent<MovementController>();
        if (jumpController == null) jumpController = GetComponent<JumpController>();
        if (groundDetector == null) groundDetector = GetComponent<GroundDetector>();
        if (highFallDetector == null) highFallDetector = GetComponent<HighFallDetector>();
        if (playerAnimationController == null) playerAnimationController = GetComponent<PlayerAnimationController>();
        if (playerStateMachine == null) playerStateMachine = GetComponent<PlayerStateMachine>();
    }
}