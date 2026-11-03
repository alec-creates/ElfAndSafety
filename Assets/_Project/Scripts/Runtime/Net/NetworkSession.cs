using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.UI;

namespace ElfAndSafety
{
    /// <summary>
    /// Co-op session (milestone 4). Solo play needs no networking at all: the scene has no
    /// NetworkManager. Hosting or joining (chosen in the main menu) clears local practice items, removes the
    /// solo elf, and creates the NetworkManager from a prefab; leaving (or losing the host) destroys it and goes
    /// back to the main menu.
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

        [SerializeField] ushort port = 7777;

        bool returnPending;
        bool leftOnPurpose;
        bool wasConnected;
        bool startedAsClient;

        /// <summary>Where a client connects: the address typed in the main menu (127.0.0.1 for this PC).</summary>
        static string Address => GameLaunch.JoinAddress;

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

        void Start()
        {
            // The main menu says how this Workshop should start.
            switch (GameLaunch.TakePendingMode())
            {
                case LaunchMode.Host: StartHost(); break;
                case LaunchMode.Join: StartClient(); break;
            }
        }

        void Update()
        {
            if (returnPending)
            {
                ReturnToMenu();
                return;
            }

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsConnectedClient) wasConnected = true;
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

            startedAsClient = !asHost;
            NetworkManager manager = Instantiate(networkManagerPrefab);
            manager.GetComponent<UnityTransport>().SetConnectionData(Address, port);
            manager.OnClientStopped += OnSessionStopped;

            bool started = asHost ? manager.StartHost() : manager.StartClient();
            if (!started)
            {
                WorkshopFeedback.Problem(asHost ? "Couldn't host (is the port in use?)" : "Couldn't start the client");
                returnPending = true;
                return false;
            }

            if (asHost)
            {
                Instantiate(workshopSyncPrefab).Spawn();
            }

            WorkshopFeedback.Info(asHost ? $"Hosting on port {port}. Waiting for a second elf" : $"Joining {Address}:{port}...");
            return true;
        }

        /// <summary>Ends the session and goes back to the main menu.</summary>
        public void Leave()
        {
            if (NetworkManager.Singleton == null) return;

            leftOnPurpose = true;
            if (NetworkManager.Singleton.IsListening)
            {
                NetworkManager.Singleton.Shutdown();
            }
            returnPending = true;
        }

        void OnSessionStopped(bool wasHost)
        {
            // Covers leaving, a failed join, and the host going away. Clean up outside the callback.
            returnPending = true;
        }

        void ReturnToMenu()
        {
            returnPending = false;
            NetworkManager manager = NetworkManager.Singleton;
            if (manager != null)
            {
                manager.OnClientStopped -= OnSessionStopped;
                if (manager.IsListening) manager.Shutdown();
                Destroy(manager.gameObject);
            }

            string message = null;
            if (!leftOnPurpose && startedAsClient)
            {
                message = wasConnected ? "The host ended the session" : $"Couldn't connect to {Address}";
            }
            GameLaunch.LoadMainMenu(message);
        }

        string StatusText()
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsListening)
            {
                return string.Empty;
            }

            if (manager.IsServer)
            {
                int players = manager.ConnectedClientsIds.Count;
                return $"CO-OP: hosting on port {port}, {players} elf{(players == 1 ? "" : "s")} connected";
            }

            return manager.IsConnectedClient ? $"CO-OP: joined {Address}" : $"CO-OP: connecting to {Address}...";
        }
    }
}
