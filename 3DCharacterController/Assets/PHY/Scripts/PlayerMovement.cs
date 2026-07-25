using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    // movementController로 옮기기
    [SerializeField] private float speed = 10f;

    // InputHandler로 옮기기
    private Vector2 movement;

    private void Update()
    {
        ProcessTranslation();
    }

    // InputHandler로 옮기기
    public void OnMove(InputValue value)
    {
        movement = value.Get<Vector2>();
        Debug.Log($"MoveSlope Input : {movement}");
    }

    // movementController로 옮기기
    public void ProcessTranslation()
    {
        // movementController 로직
        Vector3 move = new Vector3(movement.x, 0f, movement.y);

        // Rigidbody.linearvelocity 바뀔 수도 잇음
        transform.localPosition += move * speed * Time.deltaTime;
    }

    // JumpController
    public void OnJump()
    {
        Debug.Log("TryJump");
    }
}
