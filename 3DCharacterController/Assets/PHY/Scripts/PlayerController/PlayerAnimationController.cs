
using UnityEngine;

public class PlayerAnimationController : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private CharacterController characterController;
    [SerializeField] private GroundDetector groundDetector;
    [SerializeField] private JumpController jumpController;
    [SerializeField] private MovementController movementController;

    // 추가: Root Motion 전달 대상
    [SerializeField] private PlayerController playerController;

    private readonly int SpeedHash = Animator.StringToHash("Speed");
    private readonly int YSpeedHash = Animator.StringToHash("YSpeed");
    private readonly int IsAirborneHash = Animator.StringToHash("IsAirborne");
    private readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
    private readonly int IsLandingHash = Animator.StringToHash("IsLanding");
    private readonly int HasMoveInputHash = Animator.StringToHash("HasMoveInput");
    private readonly int MotionStateHash = Animator.StringToHash("MotionState");
    private readonly string IdleJumpTrigger = "IdleJumpTrigger";

    private void Reset()
    {
        characterController = GetComponent<CharacterController>();
        groundDetector = GetComponent<GroundDetector>();
        jumpController = GetComponent<JumpController>();
        movementController = GetComponent<MovementController>();
        animator = GetComponent<Animator>();

        // 추가
        playerController = GetComponentInParent<PlayerController>();
    }

    private void Awake()
    {
        if (characterController == null)
            characterController = GetComponent<CharacterController>();

        if (groundDetector == null)
            groundDetector = GetComponent<GroundDetector>();

        if (jumpController == null)
            jumpController = GetComponent<JumpController>();

        if (movementController == null)
            movementController = GetComponent<MovementController>();

        if (animator == null)
            animator = GetComponent<Animator>();

        // 추가
        if (playerController == null)
            playerController = GetComponentInParent<PlayerController>();
    }

    private void Update()
    {
        if (!IsAnimationReady()) return;

        UpdateMoveSpeed();

        SetYSpeed(jumpController.VerticalVelocity);
        SetGrounded(groundDetector.IsGrounded);
    }

    // 추가: Vault 중 Root Motion 전달
    private void OnAnimatorMove()
    {
        if (animator == null)
            return;

        if (playerController == null ||
            !playerController.IsVaultActive)
        {
            // 기존 Animator의 Root Motion 설정 유지
            if (animator.applyRootMotion)
                animator.ApplyBuiltinRootMotion();

            return;
        }

        // 추가: Root Motion 진단 로그
        Debug.Log(
            $"[Vault RootMotion] " +
            $"deltaPosition: {animator.deltaPosition.ToString("F4")}"
        );

        playerController.ApplyVaultRootMotion(
            animator.deltaPosition);
    }

    private void UpdateMoveSpeed()
    {
        if (movementController.RunSpeed <= 0f)
        {
            SetSpeed(0f);
            return;
        }

        float normalizedSpeed =
            movementController.CurrentHorizontalSpeed /
            movementController.RunSpeed;

        normalizedSpeed = Mathf.Clamp01(normalizedSpeed);

        SetSpeed(normalizedSpeed);
    }

    public void SetSpeed(float value)
    {
        if (animator == null) return;

        animator.SetFloat(
            SpeedHash,
            value,
            0.05f,
            Time.deltaTime);
    }

    public PlayerMotion CurrentMotion { get; private set; }

    public void SetMotion(PlayerMotion motion)
    {
        CurrentMotion = motion;

        if (animator == null) return;

        animator.SetInteger(
            MotionStateHash,
            (int)motion);
    }

    // 지정한 Animator State의 애니메이션 재생 완료 여부 확인
    // State 전환 중이거나 해당 State가 아니면 false 반환
    // 반복 재생하지 않는 단일 점프 클립에서 사용
    public bool IsMotionComplete(string stateName)
    {
        if (animator == null)
        {
            return false;
        }

        if (animator.IsInTransition(0))
        {
            return false;
        }

        AnimatorStateInfo stateInfo =
            animator.GetCurrentAnimatorStateInfo(0);

        return stateInfo.IsName(stateName) &&
               stateInfo.normalizedTime >= 1f;
    }

    public void SetYSpeed(float value)
    {
        if (animator == null) return;

        animator.SetFloat(YSpeedHash, value);
    }

    public void SetGrounded(bool value)
    {
        if (animator == null) return;

        animator.SetBool(IsGroundedHash, value);
    }

    public void SetAirborne(bool value)
    {
        if (animator == null) return;

        animator.SetBool(IsAirborneHash, value);
    }

    public void SetLanding(bool value)
    {
        if (animator == null) return;

        animator.SetBool(IsLandingHash, value);
    }

    public void SetMoveInput(bool value)
    {
        if (animator == null) return;

        animator.SetBool(HasMoveInputHash, value);
    }

    public void TriggerIdleJump()
    {
        if (animator == null) return;

        animator.SetTrigger(IdleJumpTrigger);
    }

    private bool IsAnimationReady()
    {
        return animator != null
            && characterController != null
            && groundDetector != null
            && jumpController != null
            && movementController != null;
    }
}
