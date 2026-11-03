using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ElfAndSafety
{
    /// <summary>
    /// Co-op session (milestone 4). Solo play needs no networking at all: the scene has no
    /// NetworkManager. Hosting or joining clears local practice items, removes the solo elf, and creates the
    /// NetworkManager from a prefab; leaving (or losing the host) destroys it and reloads a solo Workshop.
    /// The host decides gameplay; a client only mirrors it and sends requests.
    /// </summary>
    public class NetworkSession : MonoBehaviour
    {
        [SerializeField] NetworkManager networkManagerPrefab;
        [Tooltip("Spawned by the host: the shared shift, count and station state (NetworkWorkshop).")]
        [SerializeField] NetworkObject workshopSyncPrefab;
        [SerializeField] PlayerInteractor soloPlayer;
        [SerializeField] DeliveryChute delivery;
        [SerializeField] WorkdayManager workday;
        [SerializeField] InteractionHud hud;
        [SerializeField] Text statusLabel;

        [Tooltip("Address a client connects to. 127.0.0.1 for a second player on this PC (Multiplayer Play Mode).")]
        [SerializeField] string address = "127.0.0.1";
        [SerializeField] ushort port = 7777;

        bool returnToSoloPending;

        public static NetworkSession Current { get; private set; }

        public static bool IsOnline => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

        /// <summary>Hosting: this machine decides gameplay for everyone.</summary>
        public static bool IsServer => IsOnline && NetworkManager.Singleton.IsServer;

        /// <summary>Joined someone else's game: this machine mirrors the host and sends requests.</summary>
        public static bool IsRemoteClient => IsOnline && !NetworkManager.Singleton.IsServer;

        public InteractionHud Hud => hud;

        void Awake()
        {
            Current = this;
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        void Update()
        {
            if (returnToSoloPending)
            {
                ReturnToSolo();
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.f5Key.wasPressedThisFrame) StartHost();
                else if (keyboard.f6Key.wasPressedThisFrame) StartClient();
                else if (keyboard.f8Key.wasPressedThisFrame) Leave();
            }

            if (statusLabel != null) statusLabel.text = StatusText();
        }

        public bool StartHost() => Begin(asHost: true);

        public bool StartClient() => Begin(asHost: false);

        public bool CanBegin(out string reason)
        {
            if (NetworkManager.Singleton != null)
            {
                reason = "Already in a co-op session";
                return false;
            }

            if (networkManagerPrefab == null || workshopSyncPrefab == null || soloPlayer == null)
            {
                reason = "Co-op is not set up in this scene";
                return false;
            }

            if (workday != null && workday.State != ShiftState.NotStarted)
            {
                reason = "Co-op can only start before clocking in";
                return false;
            }

            reason = null;
            return true;
        }

        bool Begin(bool asHost)
        {
            if (!CanBegin(out string reason))
            {
                WorkshopFeedback.Problem(reason);
                return false;
            }

            // Practice items exist only on this machine, so they can't come along into a shared session.
            delivery.ClearForSession();
            Destroy(soloPlayer.gameObject);
            soloPlayer = null;

            NetworkManager manager = Instantiate(networkManagerPrefab);
            manager.GetComponent<UnityTransport>().SetConnectionData(address, port);
            manager.OnClientStopped += OnSessionStopped;

            bool started = asHost ? manager.StartHost() : manager.StartClient();
            if (!started)
            {
                WorkshopFeedback.Problem(asHost ? "Couldn't host (is the port in use?)" : "Couldn't start the client");
                returnToSoloPending = true;
                return false;
            }

            if (asHost)
            {
                Instantiate(workshopSyncPrefab).Spawn();
            }

            WorkshopFeedback.Info(asHost ? $"Hosting on port {port}. Waiting for a second elf" : $"Joining {address}:{port}...");
            return true;
        }

        /// <summary>Ends the session; the Workshop reloads for solo play.</summary>
        public void Leave()
        {
            if (NetworkManager.Singleton == null) return;

            if (NetworkManager.Singleton.IsListening)
            {
                NetworkManager.Singleton.Shutdown();
            }
            returnToSoloPending = true;
        }

        void OnSessionStopped(bool wasHost)
        {
            // Covers leaving, a failed join, and the host going away. Clean up outside the callback.
            returnToSoloPending = true;
        }

        void ReturnToSolo()
        {
            returnToSoloPending = false;
            NetworkManager manager = NetworkManager.Singleton;
            if (manager != null)
            {
                manager.OnClientStopped -= OnSessionStopped;
                if (manager.IsListening) manager.Shutdown();
                Destroy(manager.gameObject);
            }

            SceneManager.LoadScene(gameObject.scene.name);
        }

        string StatusText()
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsListening)
            {
                return CanBegin(out _)
                    ? $"CO-OP TEST   F5 Host   F6 Join {address}"
                    : string.Empty;
            }

            if (manager.IsServer)
            {
                int players = manager.ConnectedClientsIds.Count;
                return $"CO-OP: hosting, {players} elf{(players == 1 ? "" : "s")} connected   F8 Leave";
            }

            return manager.IsConnectedClient ? $"CO-OP: joined {address}   F8 Leave" : $"CO-OP: connecting to {address}...   F8 Leave";
        }
    }
}
