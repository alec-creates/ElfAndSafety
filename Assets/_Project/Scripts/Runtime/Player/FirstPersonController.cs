using UnityEngine;

namespace ElfAndSafety
{
    /// <summary>
    /// Simple first-person movement on a CharacterController: mouse look, WASD, sprint and a small hop.
    /// </summary>
    [RequireComponent(typeof(CharacterController), typeof(PlayerInputReader))]
    public class FirstPersonController : MonoBehaviour
    {
        [SerializeField] Transform cameraPivot;

        [Header("Movement")]
        [SerializeField] float walkSpeed = 4f;
        [SerializeField] float sprintSpeed = 6.5f;
        [SerializeField] float acceleration = 14f;
        [SerializeField] float jumpHeight = 0.9f;
        [SerializeField] float gravity = -20f;

        [Header("Look")]
        [SerializeField] float mouseSensitivity = 0.12f;
        [SerializeField] float maxPitch = 85f;

        CharacterController controller;
        PlayerInputReader input;
        Vector3 horizontalVelocity;
        float verticalVelocity;
        float pitch;

        /// <summary>Turn off to freeze the player, e.g. when the workday ends.</summary>
        public bool MovementEnabled { get; set; } = true;

        public Vector3 Velocity => controller.velocity;
        public Transform CameraPivot => cameraPivot;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            input = GetComponent<PlayerInputReader>();
            GameLayers.ApplyCollisionRules();
        }

        void Update()
        {
            if (MovementEnabled)
            {
                UpdateLook();
            }

            // A cutscene (the trapdoor) may switch the controller off and move the player itself.
            if (controller.enabled)
            {
                UpdateMovement();
            }
        }

        void UpdateLook()
        {
            Vector2 look = input.Look * mouseSensitivity;
            transform.Rotate(0f, look.x, 0f, Space.Self);

            pitch = Mathf.Clamp(pitch - look.y, -maxPitch, maxPitch);
            cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        void UpdateMovement()
        {
            Vector2 move = MovementEnabled ? Vector2.ClampMagnitude(input.Move, 1f) : Vector2.zero;
            float speed = input.SprintHeld ? sprintSpeed : walkSpeed;
            Vector3 targetVelocity = (transform.right * move.x + transform.forward * move.y) * speed;
            horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, targetVelocity, acceleration * speed * Time.deltaTime);

            if (controller.isGrounded)
            {
                // Small downward push keeps the controller snapped to the floor and ramps.
                verticalVelocity = -2f;

                if (MovementEnabled && input.JumpPressed)
                {
                    verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
                }
            }

            verticalVelocity += gravity * Time.deltaTime;

            Vector3 motion = horizontalVelocity + Vector3.up * verticalVelocity;
            controller.Move(motion * Time.deltaTime);
        }
    }
}
