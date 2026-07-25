using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 플레이어 입력 값을 관리하는 입력 처리 컴포넌트
/// 이동, 점프, 달리기, 카메라 Look 입력을 상태와 컨트롤러에서
/// 사용할 수 있게 제공함
/// </summary>
public class InputHandler : MonoBehaviour
{
    public Vector2 Movement { get; private set; }
    public Vector2 Look { get; private set; }
    public bool JumpPressed { get; private set; }
    public bool IsRunPressed { get; private set; }

    /// <summary>
    /// 이동 입력 상태 저장
    /// </summary>
    /// <param name="value">이동 입력값</param>
    public void OnMove(InputValue value)
    {
        Movement = value.Get<Vector2>();

    }

    /// <summary>
    /// 점프 입력 상태 저장 
    /// </summary>
    public void OnJump()
    {
        JumpPressed = true;
    }

    /// <summary>
    /// 달리기 입력 상태 저장
    /// </summary>
    /// <param name="value">달리기 입력값</param>
    public void OnRun(InputValue value)
    {
        IsRunPressed = value.isPressed;
    }

    /// <summary>
    /// 마우스 Look 입력값 저장
    /// </summary>
    /// <param name="value">마우스 Look 입력값</param>
    public void OnLook(InputValue value)
    {
        Look = value.Get<Vector2>();
    }

    /// <summary>
    /// 점프 입력 초기화 
    /// </summary>
    public void ResetJumpInput()
    {
        JumpPressed = false;
    }
}
