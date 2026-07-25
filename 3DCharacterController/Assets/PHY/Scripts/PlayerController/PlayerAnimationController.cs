using UnityEngine;

public class PlayerAnimationController : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private CharacterController characterController;
    [SerializeField] private GroundDetector groundDetector;
    [SerializeField] private JumpController jumpController;
    [SerializeField] private MovementController movementController;

    private readonly int SpeedHash = Animator.StringToHash("Speed");
    private readonly int YSpeedHash = Animator.StringToHash("YSpeed");
    private readonly int IsFallingHash = Animator.StringToHash("IsFalling");
    private readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
    private readonly int IsLandingHash = Animator.StringToHash("IsLanding");
    private readonly int HasMoveInputHash = Animator.StringToHash("HasMoveInput");

    private void Reset()
    {
        characterController = GetComponent<CharacterController>();
        groundDetector = GetComponent<GroundDetector>();
        jumpController = GetComponent<JumpController>();
        movementController = GetComponent<MovementController>();
        animator = GetComponent<Animator>();
    }

    private void Awake()
    {
        if (characterController == null) characterController = GetComponent<CharacterController>();
        if (groundDetector == null) groundDetector = GetComponent<GroundDetector>();
        if (jumpController == null) jumpController = GetComponent<JumpController>();
        if (movementController == null) movementController = GetComponent<MovementController>();
        if (animator == null) animator = GetComponent<Animator>();
       
    }

    private void Update()
    {
        if(!IsAnimationReady()) return;

        UpdateMoveSpeed();

        SetYSpeed(jumpController.VerticalVelocity);
        SetGrounded(groundDetector.IsGrounded);
    }

    private void UpdateMoveSpeed()
    {
        if(movementController.RunSpeed <= 0f)
        {
            SetSpeed(0f);
            return;
        }

        float normalizedSpeed = movementController.CurrentHorizontalSpeed / movementController.RunSpeed;
        normalizedSpeed = Mathf.Clamp01(normalizedSpeed);

        SetSpeed(normalizedSpeed);
    }

    public void SetSpeed(float value)
    {
        if (animator == null) return;
        animator.SetFloat(SpeedHash, value, 0.05f, Time.deltaTime);
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

    public void SetFalling(bool value)
    {
        if(animator == null) return;
        Debug.Log($"SetFalling È£ÃâµÊ: {value}");
        animator.SetBool(IsFallingHash, value);
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

    private bool IsAnimationReady()
    {
        return animator != null
            && characterController != null
            && groundDetector != null
            && jumpController != null
            && movementController != null;
    }
}