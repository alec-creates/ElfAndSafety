using System;
using UnityEngine;

namespace ElfAndSafety
{
    /// <summary>
    /// Finds what the player is looking at, shows what Interact would do, and carries one item at a time.
    /// The Request* methods are where carry state changes for this player; in the networking milestone they
    /// become requests the host validates.
    /// </summary>
    [DefaultExecutionOrder(10)]
    [RequireComponent(typeof(PlayerInputReader))]
    public class PlayerInteractor : MonoBehaviour, ICarryableOwner
    {
        [SerializeField] Camera viewCamera;
        [SerializeField] Transform holdPoint;
        [SerializeField] float reach = 2.6f;

        [Tooltip("What the interaction ray can hit. Exclude the Player layer.")]
        [SerializeField] LayerMask targetMask = ~0;
        [Tooltip("Geometry that pulls a held item closer instead of letting it clip through. Exclude Player and Carryable.")]
        [SerializeField] LayerMask holdBlockMask = ~0;

        readonly RaycastHit[] hits = new RaycastHit[16];

        PlayerInputReader input;
        CharacterController body;

        public Carryable Held { get; private set; }
        public IInteractable Target { get; private set; }
        public string TargetAction { get; private set; }

        /// <summary>Why nothing can be done with what the player is aiming at, if anything says so.</summary>
        public string TargetHint { get; private set; }

        /// <summary>Status line for the object under the crosshair, e.g. a robot's missing parts.</summary>
        public string LookInfo { get; private set; }

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
                ClearTarget();
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
            if (Held != null || item == null || !item.CanBeTaken)
            {
                return false;
            }

            item.SetHeld(this);
            Held = item;
            return true;
        }

        /// <summary>Sets the carried item down neatly at a station's place point. It stays a loose physics object.</summary>
        public bool RequestPlaceAt(Transform point)
        {
            if (Held == null || point == null)
            {
                return false;
            }

            Held.SetLoose(point.position, point.rotation, Vector3.zero);
            return true;
        }

        public void RequestDrop()
        {
            if (Held == null)
            {
                return;
            }

            Carryable item = Held;
            Vector3 velocity = body != null ? body.velocity : Vector3.zero;
            item.SetLoose(ComputeHoldPosition(item), holdPoint.rotation * item.HoldRotation, velocity);
        }

        /// <summary>
        /// A station hands an item straight to this player, e.g. a salvaged part when the output tray is full.
        /// Unlike <see cref="RequestPickUp"/> this may take an item that is installed or locked.
        /// </summary>
        internal bool ReceiveFromStation(Carryable item)
        {
            if (Held != null || item == null || item.State == CarryState.Consumed)
            {
                return false;
            }

            item.SetHeld(this);
            Held = item;
            return true;
        }

        void ICarryableOwner.OnCarryableReleased(Carryable item)
        {
            if (Held == item)
            {
                Held = null;
            }
        }

        void UpdateTarget()
        {
            Transform view = viewCamera.transform;
            int count = Physics.RaycastNonAlloc(view.position, view.forward, hits, reach, targetMask, QueryTriggerInteraction.Collide);
            Array.Sort(hits, 0, count, HitDistanceComparer.Instance);

            IInteractable found = null;
            string action = null;
            string hint = null;
            string info = null;

            // Trigger volumes (slots, robot part mounts) let the ray pass through to what is behind them;
            // the first solid collider ends the search.
            for (int i = 0; i < count; i++)
            {
                Collider hitCollider = hits[i].collider;

                if (info == null && hitCollider.GetComponentInParent<IDescribable>() is IDescribable describable)
                {
                    info = describable.Describe();
                }

                var candidate = hitCollider.GetComponentInParent<IInteractable>();
                if (candidate != null)
                {
                    if (candidate.CanInteract(this, out string label))
                    {
                        found = candidate;
                        action = label;
                        break;
                    }
                    hint ??= label;
                }

                if (!hitCollider.isTrigger)
                {
                    break;
                }
            }

            Target = found;
            TargetAction = action;
            TargetHint = found == null ? hint : null;
            LookInfo = info;
        }

        void ClearTarget()
        {
            Target = null;
            TargetAction = null;
            TargetHint = null;
            LookInfo = null;
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

        sealed class HitDistanceComparer : System.Collections.Generic.IComparer<RaycastHit>
        {
            public static readonly HitDistanceComparer Instance = new HitDistanceComparer();
            public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
        }
    }
}
