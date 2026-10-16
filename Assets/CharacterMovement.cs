using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class CharacterMovement : MonoBehaviour
{
    [SerializeField, Min(0f)] private float moveSpeed = 3f;
    [SerializeField, Min(0f)] private float turnSpeed = 120f;
    [SerializeField] private float gravity = -9.81f;

    private CharacterController characterController;
    private float verticalVelocity;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
    }

    private void Update()
    {
        float moveInput = 0f;
        float turnInput = 0f;
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.wKey.isPressed) moveInput += 1f;
            if (keyboard.sKey.isPressed) moveInput -= 1f;
            if (keyboard.dKey.isPressed) turnInput += 1f;
            if (keyboard.aKey.isPressed) turnInput -= 1f;
        }

        transform.Rotate(Vector3.up, turnInput * turnSpeed * Time.deltaTime, Space.World);
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        Vector3 horizontal = forward * (moveInput * moveSpeed);

        if (characterController.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;
        verticalVelocity += gravity * Time.deltaTime;

        characterController.Move((horizontal + Vector3.up * verticalVelocity) * Time.deltaTime);
    }
}
