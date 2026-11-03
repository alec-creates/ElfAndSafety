using UnityEngine;

namespace ElfAndSafety
{
    public enum CarryState
    {
        Loose,
        Held,
        Installed,
        Reserved,
        Consumed
    }

    /// <summary>
    /// A station operation that is holding an item still, e.g. the cleaning station mid-scrub.
    /// </summary>
    public interface IReservingStation
    {
        string BusyMessage { get; }
    }

    /// <summary>
    /// A physical object with exactly one owner at a time: nobody (loose, with normal physics), a player's
    /// hands, a robot it is installed in, or a station operation that has reserved it. Every transfer goes
    /// through one of the Set* methods, which first releases the previous owner, so an object can never be
    /// in two places. Gameplay lives on this root object; art goes in a child "Visual" object.
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
        [Tooltip("Below this height the item returns to where it first appeared.")]
        [SerializeField] float respawnBelowY = -20f;
        [Tooltip("Loose items slower than this count as resting, so stations can start work on them.")]
        [SerializeField] float restingSpeed = 0.2f;

        Rigidbody body;
        Collider[] colliders;
        Vector3 spawnPosition;
        Quaternion spawnRotation;

        // Plain fields of Unity-serializable types, so ownership survives a script recompile in Play mode.
        CarryState state = CarryState.Loose;
        Object owner;

        public string DisplayName => displayName;
        public CarryState State => state;
        public ICarryableOwner Owner => owner != null ? owner as ICarryableOwner : null;

        public PlayerInteractor Holder => state == CarryState.Held ? owner as PlayerInteractor : null;
        public bool IsHeld => state == CarryState.Held;

        /// <summary>Only loose items can be picked up. Installed parts and reserved items cannot.</summary>
        public bool CanBeTaken => state == CarryState.Loose;

        public bool IsResting => state == CarryState.Loose && body.linearVelocity.sqrMagnitude < restingSpeed * restingSpeed;

        /// <summary>Approximate size, used to keep the held item from poking through walls.</summary>
        public float HoldRadius { get; private set; }

        public Vector3 HoldOffset => holdOffset;
        public Quaternion HoldRotation => Quaternion.Euler(holdRotation);

        /// <summary>
        /// Set on a co-op client: the host simulates this item, so here it never runs its own physics and
        /// only mirrors the host's state (see NetworkCarryable).
        /// </summary>
        internal bool IsMirror { get; set; }

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            spawnPosition = transform.position;
            spawnRotation = transform.rotation;

            // Only this object's own colliders: parts installed as children manage theirs separately.
            colliders = System.Array.FindAll(GetComponentsInChildren<Collider>(true), c => c.GetComponentInParent<Carryable>() == this);

            // Keeps overlapping or wedged items from launching across the room.
            body.maxDepenetrationVelocity = 1.5f;
            body.maxLinearVelocity = 12f;
            body.interpolation = RigidbodyInterpolation.Interpolate;

            Bounds bounds = new Bounds(transform.position, Vector3.zero);
            foreach (var c in colliders)
            {
                if (!c.isTrigger) bounds.Encapsulate(c.bounds);
            }
            HoldRadius = Mathf.Clamp(bounds.extents.magnitude * 0.5f, 0.05f, 0.3f);
        }

        void FixedUpdate()
        {
            if (!IsMirror && state == CarryState.Loose && body.position.y < respawnBelowY)
            {
                SetLoose(spawnPosition, spawnRotation, Vector3.zero);
            }
        }

        void OnDestroy()
        {
            // Destroyed while held, installed or reserved: make sure nobody keeps a stale reference.
            ReleaseOwner();
        }

        public bool CanInteract(PlayerInteractor player, out string actionLabel)
        {
            if (state == CarryState.Reserved)
            {
                actionLabel = owner is IReservingStation station ? station.BusyMessage : $"{displayName} is busy";
                return false;
            }

            // Carrying something and aiming at an item on a station: same as aiming at the station.
            if (player.Held != null)
            {
                WorkStation station = WorkStation.Containing(transform.position);
                if (station != null && state == CarryState.Loose)
                {
                    return station.CanInteract(player, out actionLabel);
                }

                actionLabel = null;
                return false;
            }

            actionLabel = $"Pick up {displayName}";
            return CanBeTaken;
        }

        public void Interact(PlayerInteractor player)
        {
            if (player.Held != null)
            {
                WorkStation.Containing(transform.position)?.Interact(player);
            }
            else
            {
                player.RequestPickUp(this);
            }
        }

        // --- Ownership transfers. Players, robots and stations call these; a network host can own them later. ---

        internal void SetHeld(PlayerInteractor holder)
        {
            ReleaseOwner();
            owner = holder;
            state = CarryState.Held;
            transform.SetParent(null, true);
            MakeKinematic(collidersEnabled: false);
        }

        /// <summary>Attaches to a mount on another object and moves with it. Not targetable while installed.</summary>
        internal void SetInstalled(ICarryableOwner installedIn, Transform mount)
        {
            ReleaseOwner();
            owner = installedIn as Object;
            state = CarryState.Installed;
            MakeKinematic(collidersEnabled: false);
            transform.SetParent(mount, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }

        /// <summary>A station operation holds the item still at a pose until it releases it with <see cref="SetLoose"/>.</summary>
        internal void SetReserved(ICarryableOwner station, Vector3 position, Quaternion rotation)
        {
            ReleaseOwner();
            owner = station as Object;
            state = CarryState.Reserved;
            transform.SetParent(null, true);
            MakeKinematic(collidersEnabled: true);
            transform.SetPositionAndRotation(position, rotation);
        }

        internal void SetLoose(Vector3 position, Quaternion rotation, Vector3 velocity)
        {
            ReleaseOwner();
            state = CarryState.Loose;
            transform.SetParent(null, true);
            transform.SetPositionAndRotation(position, rotation);

            if (IsMirror)
            {
                // A client's copy: aimable, but its movement comes from the host.
                MakeKinematic(collidersEnabled: true);
                return;
            }

            SetCollidersEnabled(true);
            body.isKinematic = false;
            body.position = position;
            body.rotation = rotation;
            body.linearVelocity = Vector3.ClampMagnitude(velocity, maxDropSpeed);
            body.angularVelocity = Vector3.zero;
            body.interpolation = RigidbodyInterpolation.Interpolate;
        }

        /// <summary>Removes the object from play (shipped or reset). Safe to call more than once.</summary>
        internal void Consume()
        {
            if (state == CarryState.Consumed)
            {
                return;
            }

            // Installed parts leave play with the object they are installed in, rather than lingering
            // as "installed" in something already gone until Unity destroys the hierarchy.
            foreach (Carryable child in GetComponentsInChildren<Carryable>(true))
            {
                if (child != this && child.state == CarryState.Installed)
                {
                    child.Consume();
                }
            }

            ReleaseOwner();
            state = CarryState.Consumed;

            // In a co-op session the host despawns it, which removes it on every screen.
            if (TryGetComponent(out Unity.Netcode.NetworkObject networkObject) && networkObject.IsSpawned)
            {
                networkObject.Despawn(destroy: true);
                return;
            }

            gameObject.SetActive(false);
            Destroy(gameObject);
        }

        /// <summary>Called every frame by the holder; held items follow the view rigidly.</summary>
        internal void SetHeldPose(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);
        }

        void ReleaseOwner()
        {
            ICarryableOwner previous = Owner;
            owner = null;
            if (state != CarryState.Consumed)
            {
                state = CarryState.Loose;
            }

            // Owner returns null for destroyed owners, so they are skipped.
            previous?.OnCarryableReleased(this);
        }

        void MakeKinematic(bool collidersEnabled)
        {
            body.interpolation = RigidbodyInterpolation.None;
            body.isKinematic = true;
            SetCollidersEnabled(collidersEnabled);
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
