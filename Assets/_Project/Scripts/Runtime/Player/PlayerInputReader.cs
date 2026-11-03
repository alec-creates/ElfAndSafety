using UnityEngine;
using UnityEngine.InputSystem;

namespace ElfAndSafety
{
    /// <summary>
    /// Reads keyboard and mouse input for the local player and owns cursor locking.
    /// Esc releases the mouse; clicking the game view captures it again (that click is not treated as Interact).
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class PlayerInputReader : MonoBehaviour
    {
        InputAction moveAction;
        InputAction lookAction;
        InputAction jumpAction;
        InputAction sprintAction;
        InputAction interactAction;
        InputAction dropAction;
        InputAction releaseCursorAction;

        bool swallowClickThisFrame;

        /// <summary>Turn off while a menu needs the mouse (the verdict screen), so clicks don't recapture it.</summary>
        public bool AllowCursorCapture { get; set; } = true;

        public bool HasFocus => Cursor.lockState == CursorLockMode.Locked;
        public Vector2 Move => HasFocus && !PauseMenu.IsPaused ? moveAction.ReadValue<Vector2>() : Vector2.zero;
        public Vector2 Look => HasFocus && !PauseMenu.IsPaused ? lookAction.ReadValue<Vector2>() : Vector2.zero;
        public bool JumpPressed => HasFocus && jumpAction.WasPressedThisFrame();
        public bool SprintHeld => HasFocus && sprintAction.IsPressed();
        public bool InteractPressed => HasFocus && !PauseMenu.IsPaused && !swallowClickThisFrame && interactAction.WasPressedThisFrame();
        public bool DropPressed => HasFocus && dropAction.WasPressedThisFrame();

        void Awake()
        {
            moveAction = new InputAction("Move", InputActionType.Value);
            moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");

            lookAction = new InputAction("Look", InputActionType.Value, "<Mouse>/delta");
            jumpAction = new InputAction("Jump", InputActionType.Button, "<Keyboard>/space");
            sprintAction = new InputAction("Sprint", InputActionType.Button, "<Keyboard>/leftShift");

            interactAction = new InputAction("Interact", InputActionType.Button, "<Keyboard>/e");
            interactAction.AddBinding("<Mouse>/leftButton");

            dropAction = new InputAction("Drop", InputActionType.Button, "<Keyboard>/q");
            releaseCursorAction = new InputAction("ReleaseCursor", InputActionType.Button, "<Keyboard>/escape");
        }

        void OnEnable()
        {
            SetActionsEnabled(true);
            LockCursor(true);
        }

        void OnDisable()
        {
            SetActionsEnabled(false);
            LockCursor(false);
        }

        void OnDestroy()
        {
            moveAction.Dispose();
            lookAction.Dispose();
            jumpAction.Dispose();
            sprintAction.Dispose();
            interactAction.Dispose();
            dropAction.Dispose();
            releaseCursorAction.Dispose();
        }

        void Update()
        {
            swallowClickThisFrame = false;

            // With a pause menu in the scene, Esc opens it (and it frees the mouse) instead.
            if (releaseCursorAction.WasPressedThisFrame() && !PauseMenu.HandlesEscape)
            {
                LockCursor(false);
            }
            else if (!HasFocus && AllowCursorCapture && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                LockCursor(true);
                swallowClickThisFrame = true;
            }
        }

        public void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        void SetActionsEnabled(bool enabled)
        {
            foreach (var action in new[] { moveAction, lookAction, jumpAction, sprintAction, interactAction, dropAction, releaseCursorAction })
            {
                if (enabled) action.Enable();
                else action.Disable();
            }
        }
    }
}
