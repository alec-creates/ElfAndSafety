using UnityEngine;

namespace ElfAndSafety
{
    /// <summary>
    /// A loose physics object that one player at a time can carry, drop, or place into an <see cref="ItemSocket"/>.
    /// Gameplay lives on this root object; art goes in a child "Visual" object so it can be swapped freely.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Carryable : MonoBehaviour, IInteractable
    {
        [SerializeField] string displayName = "Thing";

        [Tooltip("Position offset from the player's hold point, in hold-point space.")]
        [SerializeField] Vector3 holdOffset = Vector3.zero;
        [Tooltip("Rotation applied on top of the player's view rotation while held.")]
        [SerializeField] Vector3 holdRotation = Vector3.zero;

        [Tooltip("Dropped items inherit at most this much of the player's speed.")]
        [SerializeField] float maxDropSpeed = 2.5f;
        [Tooltip("Below this height the item returns to where it started the day.")]
        [SerializeField] float respawnBelowY = -20f;

        Rigidbody body;
        Collider[] colliders;
        Vector3 spawnPosition;
        Quaternion spawnRotation;

        public string DisplayName => displayName;
        public PlayerInteractor Holder { get; private set; }
        public ItemSocket Socket { get; private set; }
        public bool IsHeld => Holder != null;

        /// <summary>Approximate size, used to keep the held item from poking through walls.</summary>
        public float HoldRadius { get; private set; }

        public Vector3 HoldOffset => holdOffset;
        public Quaternion HoldRotation => Quaternion.Euler(holdRotation);

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            colliders = GetComponentsInChildren<Collider>();
            spawnPosition = transform.position;
            spawnRotation = transform.rotation;

            // Keeps overlapping or wedged items from launching across the room.
            body.maxDepenetrationVelocity = 1.5f;
            body.maxLinearVelocity = 12f;
            body.interpolation = RigidbodyInterpolation.Interpolate;

            Bounds bounds = new Bounds(transform.position, Vector3.zero);
            foreach (var c in colliders)
            {
                bounds.Encapsulate(c.bounds);
            }
            HoldRadius = Mathf.Clamp(bounds.extents.magnitude * 0.5f, 0.05f, 0.3f);
        }

        void FixedUpdate()
        {
            if (!IsHeld && Socket == null && body.position.y < respawnBelowY)
            {
                SetLoose(spawnPosition, spawnRotation, Vector3.zero);
            }
        }

        public bool CanInteract(PlayerInteractor player, out string actionLabel)
        {
            actionLabel = Socket != null ? $"Take {displayName}" : $"Pick up {displayName}";
            return !IsHeld && player.Held == null;
        }

        public void Interact(PlayerInteractor player) => player.RequestPickUp(this);

        // --- State changes. Only PlayerInteractor and ItemSocket call these, so a network host can own them later. ---

        internal bool TryBeginCarry(PlayerInteractor holder)
        {
            if (IsHeld)
            {
                return false;
            }

            if (Socket != null)
            {
                Socket.ReleaseOccupant(this);
                Socket = null;
            }

            Holder = holder;
            body.interpolation = RigidbodyInterpolation.None;
            body.isKinematic = true;
            SetCollidersEnabled(false);
            return true;
        }

        internal void EndCarry(Vector3 position, Quaternion rotation, Vector3 inheritedVelocity)
        {
            Holder = null;
            SetLoose(position, rotation, Vector3.ClampMagnitude(inheritedVelocity, maxDropSpeed));
        }

        internal void AttachToSocket(ItemSocket socket, Transform attachPoint)
        {
            Holder = null;
            Socket = socket;
            body.interpolation = RigidbodyInterpolation.None;
            body.isKinematic = true;
            SetCollidersEnabled(true);
            transform.SetPositionAndRotation(attachPoint.position, attachPoint.rotation);
        }

        /// <summary>Called every frame by the holder; held items follow the view rigidly.</summary>
        internal void SetHeldPose(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);
        }

        void SetLoose(Vector3 position, Quaternion rotation, Vector3 velocity)
        {
            transform.SetPositionAndRotation(position, rotation);
            SetCollidersEnabled(true);
            body.isKinematic = false;
            body.position = position;
            body.rotation = rotation;
            body.linearVelocity = velocity;
            body.angularVelocity = Vector3.zero;
            body.interpolation = RigidbodyInterpolation.Interpolate;
        }

        void SetCollidersEnabled(bool enabled)
        {
            foreach (var c in colliders)
            {
                c.enabled = enabled;
            }
        }
    }
}
