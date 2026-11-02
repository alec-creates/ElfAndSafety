using UnityEngine;

namespace ElfAndSafety
{
    /// <summary>
    /// Finds what the player is looking at, shows what Interact would do, and carries one item at a time.
    /// The Request* methods are the only places carry state changes; in the networking milestone they become
    /// requests the host validates.
    /// </summary>
    [DefaultExecutionOrder(10)]
    [RequireComponent(typeof(PlayerInputReader))]
    public class PlayerInteractor : MonoBehaviour
    {
        [SerializeField] Camera viewCamera;
        [SerializeField] Transform holdPoint;
        [SerializeField] float reach = 2.6f;

        [Tooltip("What the interaction ray can hit. Exclude the Player layer.")]
        [SerializeField] LayerMask targetMask = ~0;
        [Tooltip("Geometry that pulls a held item closer instead of letting it clip through. Exclude Player and Carryable.")]
        [SerializeField] LayerMask holdBlockMask = ~0;

        PlayerInputReader input;
        CharacterController body;

        public Carryable Held { get; private set; }
        public IInteractable Target { get; private set; }
        public string TargetAction { get; private set; }

        /// <summary>Turn off to freeze interactions, e.g. when the workday ends.</summary>
        public bool InteractionEnabled { get; set; } = true;

        void Awake()
        {
            input = GetComponent<PlayerInputReader>();
            body = GetComponent<CharacterController>();
        }

        void Update()
        {
            if (!InteractionEnabled)
            {
                SetTarget(null, null);
                return;
            }

            UpdateTarget();

            if (input.InteractPressed && Target != null)
            {
                Target.Interact(this);
                UpdateTarget();
            }
            else if (input.DropPressed && Held != null)
            {
                RequestDrop();
            }
        }

        void LateUpdate()
        {
            // After the camera has turned this frame, so the held item never lags behind the view.
            if (Held != null)
            {
                Held.SetHeldPose(ComputeHoldPosition(Held), holdPoint.rotation * Held.HoldRotation);
            }
        }

        public bool RequestPickUp(Carryable item)
        {
            if (Held != null || !item.TryBeginCarry(this))
            {
                return false;
            }

            Held = item;
            return true;
        }

        public bool RequestPlace(ItemSocket socket)
        {
            if (Held == null || !socket.TryPlace(Held))
            {
                return false;
            }

            Held = null;
            return true;
        }

        public void RequestDrop()
        {
            if (Held == null)
            {
                return;
            }

            Carryable item = Held;
            Held = null;
            Vector3 velocity = body != null ? body.velocity : Vector3.zero;
            item.EndCarry(ComputeHoldPosition(item), holdPoint.rotation * item.HoldRotation, velocity);
        }

        void UpdateTarget()
        {
            Transform view = viewCamera.transform;
            IInteractable found = null;
            string action = null;

            if (Physics.Raycast(view.position, view.forward, out RaycastHit hit, reach, targetMask, QueryTriggerInteraction.Collide))
            {
                var candidate = hit.collider.GetComponentInParent<IInteractable>();
                if (candidate != null && candidate.CanInteract(this, out string label))
                {
                    found = candidate;
                    action = label;
                }
            }

            SetTarget(found, action);
        }

        void SetTarget(IInteractable target, string action)
        {
            Target = target;
            TargetAction = action;
        }

        /// <summary>The hold point, pulled toward the camera if a wall is in the way.</summary>
        Vector3 ComputeHoldPosition(Carryable item)
        {
            Vector3 origin = viewCamera.transform.position;
            Vector3 desired = holdPoint.TransformPoint(item.HoldOffset);
            Vector3 toDesired = desired - origin;
            float distance = toDesired.magnitude;

            if (distance > 0.001f &&
                Physics.SphereCast(origin, item.HoldRadius, toDesired / distance, out RaycastHit hit, distance, holdBlockMask, QueryTriggerInteraction.Ignore))
            {
                return origin + toDesired / distance * hit.distance;
            }

            return desired;
        }
    }
}
