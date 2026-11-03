using Unity.Netcode;
using UnityEngine;

namespace ElfAndSafety
{
    /// <summary>
    /// Syncs a <see cref="Carryable"/>'s owner state from the host, plus its pose while loose or reserved.
    /// On the host the Carryable runs normally and this only publishes it. On clients the Carryable is a
    /// kinematic mirror: held items follow their holder's hold point, installed parts are attached by
    /// <see cref="NetworkToy"/>, and loose items glide to the host's physics pose.
    /// Netcode's own parenting and NetworkTransform are not used here: installed parts hang under mount
    /// points, which Netcode can't parent to, and solo play must keep working with no session at all.
    /// </summary>
    [RequireComponent(typeof(Carryable), typeof(NetworkObject))]
    public class NetworkCarryable : NetworkBehaviour
    {
        const ulong NoHolder = ulong.MaxValue;

        [SerializeField] float positionThreshold = 0.002f;
        [SerializeField] float rotationThreshold = 0.5f;
        [Tooltip("How quickly a client's copy glides to the host's pose.")]
        [SerializeField] float followSharpness = 20f;
        [SerializeField] float snapDistance = 2f;

        readonly NetworkVariable<CarryState> state = new NetworkVariable<CarryState>(CarryState.Loose);
        readonly NetworkVariable<ulong> holder = new NetworkVariable<ulong>(NoHolder);
        readonly NetworkVariable<Vector3> position = new NetworkVariable<Vector3>();
        readonly NetworkVariable<Quaternion> rotation = new NetworkVariable<Quaternion>(Quaternion.identity);

        Carryable item;

        internal CarryState SyncedState => state.Value;
        internal ulong SyncedHolder => holder.Value;

        void Awake()
        {
            item = GetComponent<Carryable>();
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                Publish(force: true);
                return;
            }

            item.IsMirror = true;
            transform.SetPositionAndRotation(position.Value, rotation.Value);
            ApplyState();
            state.OnValueChanged += OnSyncedChanged;
            holder.OnValueChanged += OnSyncedChanged;
        }

        public override void OnNetworkDespawn()
        {
            state.OnValueChanged -= OnSyncedChanged;
            holder.OnValueChanged -= OnSyncedChanged;
        }

        void Update()
        {
            if (!IsSpawned) return;

            if (IsServer)
            {
                Publish(force: false);
                return;
            }

            // A holder that spawned after this item: attach once it exists.
            if (state.Value == CarryState.Held && item.State != CarryState.Held)
            {
                ApplyState();
            }

            if (item.State == CarryState.Loose || item.State == CarryState.Reserved)
            {
                FollowSyncedPose();
            }
        }

        void Publish(bool force)
        {
            CarryState current = item.State;
            ulong currentHolder = current == CarryState.Held && item.Holder != null && item.Holder.TryGetComponent(out NetworkPlayer player)
                ? player.OwnerClientId
                : NoHolder;

            if (force || state.Value != current) state.Value = current;
            if (force || holder.Value != currentHolder) holder.Value = currentHolder;

            if (force || current == CarryState.Loose || current == CarryState.Reserved)
            {
                if (force || (transform.position - position.Value).sqrMagnitude > positionThreshold * positionThreshold)
                {
                    position.Value = transform.position;
                }
                if (force || Quaternion.Angle(transform.rotation, rotation.Value) > rotationThreshold)
                {
                    rotation.Value = transform.rotation;
                }
            }
        }

        void OnSyncedChanged<T>(T previous, T current) => ApplyState();

        void ApplyState()
        {
            switch (state.Value)
            {
                case CarryState.Held:
                    NetworkPlayer player = NetworkPlayer.Find(holder.Value);
                    if (player != null) player.Interactor.MirrorHeld(item);
                    break;

                case CarryState.Loose:
                    if (item.State != CarryState.Loose) item.SetLoose(position.Value, rotation.Value, Vector3.zero);
                    break;

                case CarryState.Reserved:
                    if (item.State != CarryState.Reserved) item.SetReserved(null, position.Value, rotation.Value);
                    break;

                // Installed: NetworkToy attaches the part to its mount. Consumed: the host despawns it.
            }
        }

        void FollowSyncedPose()
        {
            Vector3 target = position.Value;
            if ((transform.position - target).sqrMagnitude > snapDistance * snapDistance)
            {
                transform.SetPositionAndRotation(target, rotation.Value);
                return;
            }

            float t = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
            transform.SetPositionAndRotation(Vector3.Lerp(transform.position, target, t), Quaternion.Slerp(transform.rotation, rotation.Value, t));
        }
    }
}
