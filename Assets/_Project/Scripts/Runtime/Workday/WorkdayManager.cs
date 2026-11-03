using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ElfAndSafety
{
    public enum ShiftState
    {
        /// <summary>Before clocking in: free practice, nothing counts.</summary>
        NotStarted,
        Running,
        Succeeded,
        Failed
    }

    /// <summary>
    /// One scored shift: clock in, ship the quota of robots before the timer runs out, get Santa's verdict.
    /// The outcome is resolved exactly once. This runs before player interactions each frame, so the timer
    /// is checked first: a shipment in the frame the timer reaches zero is too late.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class WorkdayManager : MonoBehaviour
    {
        [Header("Tuning")]
        [Tooltip("Length of the scored shift, in seconds.")]
        [SerializeField, Min(10f)] float shiftLengthSeconds = 300f;
        [Tooltip("Accepted robots needed to pass the shift.")]
        [SerializeField, Min(1)] int quota = 2;

        [Header("References")]
        [SerializeField] ShipmentCounter counter;
        [SerializeField] DeliveryChute delivery;
        [SerializeField] TrapdoorSequence trapdoor;

        [Header("Santa")]
        [SerializeField] string successTitle = "QUOTA MET";
        [TextArea] [SerializeField] string successLine = "Ho ho ho! Every present accounted for. Santa is... satisfied. For now.";
        [SerializeField] string failureTitle = "QUOTA MISSED";
        [TextArea] [SerializeField] string failureLine = "Ho ho ho! You've been reassigned to furnace operations.";

        public ShiftState State { get; private set; } = ShiftState.NotStarted;
        public float RemainingSeconds { get; private set; }
        public float ShiftLengthSeconds => shiftLengthSeconds;
        public int Quota => quota;
        public int Shipped => counter != null ? counter.Shipped : 0;

        /// <summary>Request Delivery and DEV reset only work before the shift (practice).</summary>
        public bool PracticeToolsAllowed => State == ShiftState.NotStarted;

        /// <summary>Shipping works in practice and during the shift, and stops once the shift ends.</summary>
        public bool AcceptsShipments => State == ShiftState.NotStarted || State == ShiftState.Running;

        public bool HasEnded => State == ShiftState.Succeeded || State == ShiftState.Failed;

        /// <summary>True once the verdict panel should show (after the trapdoor sequence on failure).</summary>
        public bool VerdictVisible { get; private set; }
        public string VerdictTitle { get; private set; }
        public string VerdictMessage { get; private set; }

        /// <summary>How many times the shift outcome was resolved. Must never exceed one.</summary>
        public int VerdictCount { get; private set; }

        public event Action<ShiftState> ShiftEnded;

        void Awake()
        {
            RemainingSeconds = shiftLengthSeconds;
        }

        void OnEnable()
        {
            if (counter != null) counter.Changed += OnShipmentRecorded;
        }

        void OnDisable()
        {
            if (counter != null) counter.Changed -= OnShipmentRecorded;
        }

        void Update()
        {
            // On a co-op client the host runs the clock; NetworkWorkshop mirrors it here.
            if (!NetworkSession.IsRemoteClient) Tick(Time.deltaTime);
        }

        internal void Tick(float deltaTime)
        {
            if (State != ShiftState.Running)
            {
                return;
            }

            RemainingSeconds = Mathf.Max(0f, RemainingSeconds - deltaTime);
            if (RemainingSeconds <= 0f)
            {
                Resolve(success: false);
            }
        }

        public bool CanStartShift(out string reason)
        {
            if (State != ShiftState.NotStarted)
            {
                reason = null;
                return false;
            }

            if (delivery == null || counter == null)
            {
                reason = "Workday is not set up";
                return false;
            }

            reason = null;
            return true;
        }

        /// <summary>Clears any practice items, resets the count, delivers a fresh batch and starts the timer.</summary>
        public bool StartShift()
        {
            if (!CanStartShift(out string reason))
            {
                if (reason != null) WorkshopFeedback.Problem(reason);
                return false;
            }

            counter.ResetCount();
            if (!delivery.DeliverForShift(out reason))
            {
                WorkshopFeedback.Problem($"Can't start the shift: {reason}");
                return false;
            }

            RemainingSeconds = shiftLengthSeconds;
            State = ShiftState.Running;
            WorkshopFeedback.Info($"Shift started! Ship {quota} robots in {FormatTime(shiftLengthSeconds)}");
            return true;
        }

        /// <summary>
        /// Starts over with a fresh Workshop. Solo: reloads the scene. Co-op: reloading would end the session,
        /// so the host resets the workshop in place for everyone (see <see cref="ResetShift"/>).
        /// </summary>
        public void Restart()
        {
            if (NetworkSession.IsOnline)
            {
                if (NetworkWorkshop.Instance != null) NetworkWorkshop.Instance.RequestRestart();
                return;
            }

            SceneManager.LoadScene(gameObject.scene.name);
        }

        /// <summary>
        /// Co-op host: after the verdict, clear every toy, zero the count, and return to practice with
        /// the trapdoor closed. Each player's own elf is reset when it sees the state change.
        /// </summary>
        internal void ResetShift()
        {
            if (!HasEnded)
            {
                return;
            }

            delivery.ClearForSession();
            counter.ResetCount();
            State = ShiftState.NotStarted;
            RemainingSeconds = shiftLengthSeconds;
            VerdictCount = 0;
            ResetPresentation();
        }

        /// <summary>
        /// Co-op client: the host owns the shift. Apply its state here, and play this player's own part of
        /// the outcome (frozen controls, the trapdoor fall for this elf, the verdict screen) once.
        /// </summary>
        internal void ApplyMirror(ShiftState hostState, float hostRemaining)
        {
            RemainingSeconds = hostRemaining;
            if (hostState == State)
            {
                return;
            }

            bool wasEnded = HasEnded;
            State = hostState;

            if (HasEnded)
            {
                VerdictCount++;
                PresentOutcome(State == ShiftState.Succeeded);
            }
            else if (State == ShiftState.NotStarted && wasEnded)
            {
                VerdictCount = 0;
                ResetPresentation();
            }
        }

        /// <summary>Test hook: jump the clock without waiting.</summary>
        internal void SetRemainingSeconds(float seconds)
        {
            RemainingSeconds = Mathf.Max(0f, seconds);
        }

        public static string FormatTime(float seconds)
        {
            int whole = Mathf.CeilToInt(seconds);
            return $"{whole / 60}:{whole % 60:00}";
        }

        void OnShipmentRecorded(int shipped)
        {
            // A client's count is only a mirror; the host decides the verdict.
            if (!NetworkSession.IsRemoteClient && State == ShiftState.Running && shipped >= quota)
            {
                Resolve(success: true);
            }
        }

        void Resolve(bool success)
        {
            // The single place the outcome is decided; anything after the first call is ignored.
            if (State != ShiftState.Running)
            {
                return;
            }

            State = success ? ShiftState.Succeeded : ShiftState.Failed;
            VerdictCount++;
            PresentOutcome(success);
        }

        void PresentOutcome(bool success)
        {
            FreezePlayers();
            ShiftEnded?.Invoke(State);

            if (success)
            {
                ShowVerdict(successTitle, successLine);
            }
            else
            {
                StartCoroutine(PlayFailure());
            }
        }

        IEnumerator PlayFailure()
        {
            // Each machine drops its own elf; the others see it fall through its synced position.
            PlayerInteractor player = LocalPlayer();
            if (trapdoor != null && player != null)
            {
                yield return trapdoor.Play(player.transform);
            }

            ShowVerdict(failureTitle, failureLine);
        }

        void ResetPresentation()
        {
            StopAllCoroutines();
            VerdictVisible = false;
            VerdictTitle = null;
            VerdictMessage = null;
            if (trapdoor != null) trapdoor.ResetDoors();

            PlayerInteractor player = LocalPlayer();
            if (player != null && player.TryGetComponent(out NetworkPlayer networkPlayer))
            {
                networkPlayer.ResetForNewShift();
            }
        }

        /// <summary>The elf this machine controls: the solo elf, or the networked elf it owns.</summary>
        static PlayerInteractor LocalPlayer()
        {
            foreach (var player in FindObjectsByType<PlayerInteractor>())
            {
                if (!player.TryGetComponent(out NetworkPlayer networkPlayer) || networkPlayer.IsOwner)
                {
                    return player;
                }
            }
            return null;
        }

        void FreezePlayers()
        {
            foreach (var player in FindObjectsByType<PlayerInteractor>())
            {
                player.InteractionEnabled = false;
            }

            foreach (var movement in FindObjectsByType<FirstPersonController>())
            {
                movement.MovementEnabled = false;
            }
        }

        void ShowVerdict(string title, string message)
        {
            VerdictTitle = title;
            VerdictMessage = message;
            VerdictVisible = true;

            // Free the mouse for the restart button.
            foreach (var input in FindObjectsByType<PlayerInputReader>())
            {
                input.AllowCursorCapture = false;
                input.LockCursor(false);
            }
        }
    }
}
