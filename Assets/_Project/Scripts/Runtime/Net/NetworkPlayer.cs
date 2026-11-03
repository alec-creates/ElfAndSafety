using Unity.Netcode;
using UnityEngine;

namespace ElfAndSafety
{
    /// <summary>
    /// A networked elf. The owner moves it (its NetworkTransform is owner-authoritative) and sends requests
    /// to the host; everyone else sees it move, look up and down, and carry things.
    /// A client's Interact goes to the host as "this player used that target"; the host re-checks it with
    /// its own state and runs the same station code it runs for itself.
    /// </summary>
    [RequireComponent(typeof(PlayerInteractor), typeof(FirstPersonController))]
    public class NetworkPlayer : NetworkBehaviour
    {
        [SerializeField] Vector3 spawnPosition = new Vector3(0f, 0.05f, -0.6f);
        [SerializeField] float spawnSpacing = 1.2f;

        readonly NetworkVariable<float> pitch = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        PlayerInteractor interactor;
        FirstPersonController movement;
        WorkdayManager workday;

        public PlayerInteractor Interactor => interactor;

        void Awake()
        {
            interactor = GetComponent<PlayerInteractor>();
            movement = GetComponent<FirstPersonController>();
        }

        public static NetworkPlayer Find(ulong clientId)
        {
            foreach (NetworkPlayer player in FindObjectsByType<NetworkPlayer>())
            {
                if (player.IsSpawned && player.OwnerClientId == clientId) return player;
            }
            return null;
        }

        public override void OnNetworkSpawn()
        {
            workday = FindAnyObjectByType<WorkdayManager>();

            if (IsOwner)
            {
                name = "Elf (you)";
                Teleport(SpawnPoint());
                NetworkSession.Current?.Hud?.Bind(interactor);
                return;
            }

            // Someone else's elf: no camera, no input, no local movement; it follows the network.
            name = $"Elf {OwnerClientId}";
            foreach (Camera cam in GetComponentsInChildren<Camera>()) cam.enabled = false;
            foreach (AudioListener listener in GetComponentsInChildren<AudioListener>()) listener.enabled = false;
            GetComponent<PlayerInputReader>().enabled = false;
            movement.enabled = false;
            interactor.InteractionEnabled = false;
        }

        void Update()
        {
            if (!IsSpawned) return;

            if (IsOwner)
            {
                if (!Mathf.Approximately(pitch.Value, movement.Pitch)) pitch.Value = movement.Pitch;
            }
            else
            {
                movement.CameraPivot.localRotation = Quaternion.Euler(pitch.Value, 0f, 0f);
            }
        }

        /// <summary>Owner side: back to the start, unfrozen, for the next shift (online Play Again / Retry).</summary>
        internal void ResetForNewShift()
        {
            if (!IsOwner) return;

            Teleport(SpawnPoint());
            movement.MovementEnabled = true;
            interactor.InteractionEnabled = true;
            var input = GetComponent<PlayerInputReader>();
            input.AllowCursorCapture = true;
            input.LockCursor(true);
        }

        Vector3 SpawnPoint() => spawnPosition + Vector3.right * spawnSpacing * (OwnerClientId % 4);

        void Teleport(Vector3 position)
        {
            var controller = GetComponent<CharacterController>();
            controller.enabled = false;
            transform.position = position;
            controller.enabled = true;
        }

        // --- Requests from this elf's owner to the host. The host re-checks everything. ---

        internal void RequestInteract(IInteractable target)
        {
            if (DescribeTarget(target, out NetworkObjectReference networked, out string path))
            {
                InteractRpc(networked, path);
            }
        }

        internal void RequestPickUp(Carryable item)
        {
            if (item.TryGetComponent(out NetworkObject networkObject) && networkObject.IsSpawned)
            {
                PickUpRpc(networkObject);
            }
        }

        internal void RequestDrop(Vector3 position, Quaternion rotation, Vector3 velocity) => DropRpc(position, rotation, velocity);

        [Rpc(SendTo.Server)]
        void InteractRpc(NetworkObjectReference networked, string path, RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId) return;
            ApplyInteract(networked, path);
        }

        [Rpc(SendTo.Server)]
        void PickUpRpc(NetworkObjectReference itemReference, RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId || ShiftOver) return;

            if (itemReference.TryGet(out NetworkObject networkObject) && networkObject.TryGetComponent(out Carryable item))
            {
                // On the host this applies directly; refused if someone else got there first.
                interactor.RequestPickUp(item);
            }
        }

        [Rpc(SendTo.Server)]
        void DropRpc(Vector3 position, Quaternion rotation, Vector3 velocity, RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId || ShiftOver) return;
            interactor.ApplyDrop(position, rotation, velocity);
        }

        bool ShiftOver => workday != null && workday.HasEnded;

        /// <summary>Host side of a forwarded Interact, checked against the host's own state.</summary>
        internal bool ApplyInteract(NetworkObjectReference networked, string path)
        {
            if (ShiftOver) return false;

            IInteractable target = ResolveTarget(networked, path);
            if (target == null || !target.CanInteract(interactor, out _))
            {
                return false;
            }

            target.Interact(interactor);
            return true;
        }

        /// <summary>
        /// Names a target so the host can find the same object: a spawned NetworkObject plus the path below
        /// it (toys, robot part mounts), or the full scene path (stations, which are identical on every machine).
        /// </summary>
        internal static bool DescribeTarget(IInteractable target, out NetworkObjectReference networked, out string path)
        {
            networked = default;
            path = null;
            if (!(target is Component component)) return false;

            NetworkObject owner = component.GetComponentInParent<NetworkObject>();
            if (owner != null && owner.IsSpawned)
            {
                networked = owner;
                path = RelativePath(owner.transform, component.transform);
                return true;
            }

            path = RelativePath(null, component.transform);
            return true;
        }

        internal static IInteractable ResolveTarget(NetworkObjectReference networked, string path)
        {
            Transform found;
            if (networked.TryGet(out NetworkObject owner))
            {
                found = string.IsNullOrEmpty(path) ? owner.transform : owner.transform.Find(path);
            }
            else
            {
                GameObject sceneObject = string.IsNullOrEmpty(path) ? null : GameObject.Find("/" + path);
                found = sceneObject != null ? sceneObject.transform : null;
            }

            return found != null ? found.GetComponent<IInteractable>() : null;
        }

        static string RelativePath(Transform root, Transform target)
        {
            string path = string.Empty;
            for (Transform t = target; t != null && t != root; t = t.parent)
            {
                path = path.Length == 0 ? t.name : $"{t.name}/{path}";
            }
            return path;
        }
    }
}
