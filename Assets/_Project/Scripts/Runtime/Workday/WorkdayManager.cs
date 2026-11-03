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
    /// The countdown to Christmas, one scored shift per day: clock in, fill the day's wish orders before the
    /// timer runs out, get Santa's verdict. Passing a day moves on to the next one in place; failing goes back to
    /// the first day; passing Christmas Eve wins the week. The outcome of each shift is resolved exactly once.
    /// This runs before player interactions each frame, so the timer is checked first: a shipment in the
    /// frame the timer reaches zero is too late.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class WorkdayManager : MonoBehaviour
    {
        [Header("Countdown")]
        [Tooltip("The seven days and their quotas. Without one, a single day uses the values below.")]
        [SerializeField] ChristmasCountdown countdown;

        [Header("Single day (used only when no countdown is set)")]
        [SerializeField, Min(10f)] float shiftLengthSeconds = 300f;
        [SerializeField, Min(1)] int quota = 2;

        [Header("References")]
        [SerializeField] ShipmentCounter counter;
        [SerializeField] DeliveryChute delivery;
        [SerializeField] TrapdoorSequence trapdoor;
        [SerializeField] ShippingChute shipping;

        [Header("Santa")]
        [TextArea] [SerializeField] string successLine = "Ho ho ho! Every present accounted for. Santa is... satisfied. For now.";
        [TextArea] [SerializeField] string christmasLine = "Ho ho ho! Every child gets a present, and you get to keep your job. Merry Christmas!";
        [TextArea] [SerializeField] string failureLine = "Ho ho ho! You've been reassigned to furnace operations.";

        int dayIndex;
        bool[] fulfilled = new bool[0];
        int wavesDelivered;
        float nextWaveAttempt;
        bool waveWaitAnnounced;

        public ShiftState State { get; private set; } = ShiftState.NotStarted;
        public float RemainingSeconds { get; private set; }

        public int DayIndex => dayIndex;
        public int DayNumber => dayIndex + 1;
        public int DayCount => countdown != null ? countdown.DayCount : 1;
        public bool IsFinalDay => dayIndex >= DayCount - 1;
        public int DaysUntilChristmas => DayCount - dayIndex;

        /// <summary>"7 days until Christmas" ... "Christmas Eve".</summary>
        public string DayTitle => DaysUntilChristmas <= 1 ? "Christmas Eve" : $"{DaysUntilChristmas} days until Christmas";
        public string SantaNote => countdown != null ? CurrentDay.santaNote : string.Empty;

        public float ShiftLengthSeconds => CurrentDay.shiftLengthSeconds;

        /// <summary>Today's quota: one accepted toy per wish order.</summary>
        public int Quota => CurrentDay.Quota;
        public int DeliveryWaves => CurrentDay.waves.Count;

        /// <summary>Today's wish list.</summary>
        public System.Collections.Generic.IReadOnlyList<WishOrder> Orders => CurrentDay.orders;

        public bool IsOrderFilled(int orderIndex) => orderIndex >= 0 && orderIndex < Fulfilled.Length && Fulfilled[orderIndex];

        /// <summary>What Request Delivery drops in practice: the day's first wave.</summary>
        public DeliveryWave PracticeWave => CurrentDay.waves.Count > 0 ? CurrentDay.waves[0] : DeliveryWave.Robots();
        public int WavesDelivered => wavesDelivered;
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

        /// <summary>What the verdict button does: Next Day, Back to Day 1, or Play Again.</summary>
        public string VerdictActionLabel =>
            State == ShiftState.Succeeded ? (IsFinalDay ? "Play Again" : "Next Day") : "Back to Day 1";

        /// <summary>How many times this shift's outcome was resolved. Must never exceed one.</summary>
        public int VerdictCount { get; private set; }

        public event Action<ShiftState> ShiftEnded;

        ChristmasCountdown.Day singleDay;

        bool[] Fulfilled
        {
            get
            {
                if (fulfilled.Length != Quota) fulfilled = new bool[Quota];
                return fulfilled;
            }
        }

        ChristmasCountdown.Day CurrentDay
        {
            get
            {
                if (countdown != null && countdown.DayCount > 0) return countdown.GetDay(dayIndex);
                if (singleDay == null)
                {
                    singleDay = new ChristmasCountdown.Day { shiftLengthSeconds = shiftLengthSeconds, waves = { DeliveryWave.Robots() } };
                    for (int i = 0; i < quota; i++) singleDay.orders.Add(new WishOrder { kind = OrderKind.WindUpRobot, kidName = $"Kid {i + 1}" });
                }
                return singleDay;
            }
        }

        void Awake()
        {
            RemainingSeconds = ShiftLengthSeconds;
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
                return;
            }

            DeliverDueWaves();
        }

        /// <summary>Later waves arrive on schedule; if the table is cluttered, a wave waits until there's room.</summary>
        void DeliverDueWaves()
        {
            float elapsed = ShiftLengthSeconds - RemainingSeconds;
            if (wavesDelivered >= DeliveryWaves || elapsed < wavesDelivered * CurrentDay.secondsBetweenWaves || elapsed < nextWaveAttempt)
            {
                return;
            }

            if (delivery.TryDeliverWave(CurrentDay.waves[wavesDelivered], out string reason))
            {
                wavesDelivered++;
                waveWaitAnnounced = false;
                return;
            }

            nextWaveAttempt = elapsed + 1f;
            if (!waveWaitAnnounced)
            {
                waveWaitAnnounced = true;
                WorkshopFeedback.Problem($"A delivery is waiting: {reason}");
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

            // Never start a day that can't be won with the toys and parts it will deliver.
            if (!ChristmasCountdown.IsWinnable(CurrentDay, delivery.SpawnPointCount, out string why))
            {
                reason = $"Day {DayNumber} can't be won: {why}. Fix the Christmas Countdown asset";
                return false;
            }

            reason = null;
            return true;
        }

        /// <summary>Clears any practice items, resets the count, delivers the first wave and starts the timer.</summary>
        public bool StartShift()
        {
            if (!CanStartShift(out string reason))
            {
                if (reason != null) WorkshopFeedback.Problem(reason);
                return false;
            }

            counter.ResetCount();
            fulfilled = new bool[Quota];
            if (!delivery.DeliverForShift(CurrentDay.waves[0], out reason))
            {
                WorkshopFeedback.Problem($"Can't start the shift: {reason}");
                return false;
            }

            wavesDelivered = 1;
            nextWaveAttempt = 0f;
            waveWaitAnnounced = false;
            RemainingSeconds = ShiftLengthSeconds;
            State = ShiftState.Running;
            string waves = DeliveryWaves > 1 ? $", {DeliveryWaves} deliveries" : string.Empty;
            WorkshopFeedback.Info($"Day {DayNumber} started! {Quota} wishes to fill in {FormatTime(ShiftLengthSeconds)}{waves}");
            return true;
        }

        /// <summary>
        /// During a shift: the open order this toy exactly matches. When none does, the reason comes from the
        /// closest open order for the same kind of toy, so the elves know what to fix.
        /// </summary>
        public bool TryMatchOrder(ToyBody toy, out int orderIndex, out string reason)
        {
            orderIndex = -1;
            string bestReason = null;
            int bestProblems = int.MaxValue;
            bool anyOrderForThisToy = false;

            for (int i = 0; i < Quota; i++)
            {
                ToyRecipe recipe = Orders[i].Recipe;
                if (recipe.Toy != toy.Type) continue;
                anyOrderForThisToy = true;
                if (IsOrderFilled(i)) continue;

                var problems = ToyInspection.FindProblems(toy, recipe);
                if (problems.Count == 0)
                {
                    orderIndex = i;
                    reason = null;
                    return true;
                }

                if (problems.Count < bestProblems)
                {
                    bestProblems = problems.Count;
                    bestReason = $"{string.Join(", ", problems)} (for {Orders[i].Describe()})";
                }
            }

            string toyName = ToyRecipe.ToyName(toy.Type);
            reason = bestReason ?? (anyOrderForThisToy
                ? $"every {toyName} on today's wish list is already filled"
                : $"nobody on today's wish list wants a {toyName}");
            return false;
        }

        /// <summary>Shipping: mark an order filled. Called just before the shipment is counted.</summary>
        internal void FillOrder(int orderIndex)
        {
            if (State == ShiftState.Running && orderIndex >= 0 && orderIndex < Fulfilled.Length)
            {
                Fulfilled[orderIndex] = true;
            }
        }

        /// <summary>Co-op: filled orders as bits, for syncing.</summary>
        internal int FulfilledMask
        {
            get
            {
                int mask = 0;
                for (int i = 0; i < Fulfilled.Length && i < 31; i++) if (Fulfilled[i]) mask |= 1 << i;
                return mask;
            }
        }

        internal void MirrorFulfilled(int mask)
        {
            for (int i = 0; i < Fulfilled.Length && i < 31; i++) Fulfilled[i] = (mask & (1 << i)) != 0;
        }

        /// <summary>
        /// The verdict button: after passing a day, the next day starts in place; after failing or winning the
        /// week, back to day 1. In co-op the host decides, whoever presses it.
        /// </summary>
        public void Continue()
        {
            if (NetworkSession.IsOnline)
            {
                if (NetworkWorkshop.Instance != null) NetworkWorkshop.Instance.RequestContinue();
                return;
            }

            ContinueLocally();
        }

        internal void ContinueLocally()
        {
            if (!HasEnded)
            {
                return;
            }

            if (State == ShiftState.Succeeded && !IsFinalDay)
            {
                ResetShift(dayIndex + 1);
            }
            else if (NetworkSession.IsOnline)
            {
                ResetShift(0);
            }
            else
            {
                Restart();
            }
        }

        /// <summary>
        /// Back to day 1 with a fresh Workshop. Solo: reloads the scene. Co-op: reloading would end the
        /// session, so the host resets the workshop in place for everyone (see <see cref="ResetShift"/>).
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
        /// After a verdict: clear every toy, zero the count, close the trapdoor, and start the given day in
        /// practice mode. Each player's own elf is reset (in co-op, when it sees the state change).
        /// </summary>
        internal void ResetShift(int newDayIndex)
        {
            if (!HasEnded)
            {
                return;
            }

            delivery.ClearForSession();
            counter.ResetCount();
            if (shipping != null) shipping.ResetForNewDay();
            dayIndex = Mathf.Clamp(newDayIndex, 0, DayCount - 1);
            fulfilled = new bool[Quota];
            State = ShiftState.NotStarted;
            RemainingSeconds = ShiftLengthSeconds;
            VerdictCount = 0;
            ResetPresentation();

            WorkshopFeedback.Info($"Day {DayNumber}: {DayTitle}. Clock in when you're ready");
        }

        /// <summary>
        /// Co-op client: the host owns the countdown and the shift. Apply its state here, and play this
        /// player's own part of the outcome (frozen controls, the trapdoor fall for this elf, the verdict
        /// screen) once.
        /// </summary>
        internal void ApplyMirror(int hostDayIndex, ShiftState hostState, float hostRemaining)
        {
            dayIndex = Mathf.Clamp(hostDayIndex, 0, DayCount - 1);
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
                fulfilled = new bool[Quota];
                ResetPresentation();
            }
        }

        /// <summary>Test hook: jump the clock without waiting.</summary>
        internal void SetRemainingSeconds(float seconds)
        {
            RemainingSeconds = Mathf.Max(0f, seconds);
        }

        /// <summary>Test hook: start on a later day.</summary>
        internal void SetDayForTest(int newDayIndex)
        {
            dayIndex = Mathf.Clamp(newDayIndex, 0, DayCount - 1);
            fulfilled = new bool[Quota];
            RemainingSeconds = ShiftLengthSeconds;
        }

        public static string FormatTime(float seconds)
        {
            int whole = Mathf.CeilToInt(seconds);
            return $"{whole / 60}:{whole % 60:00}";
        }

        void OnShipmentRecorded(int shipped)
        {
            // A client's count is only a mirror; the host decides the verdict.
            if (!NetworkSession.IsRemoteClient && State == ShiftState.Running && shipped >= Quota)
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

            if (!success)
            {
                StartCoroutine(PlayFailure());
            }
            else if (IsFinalDay)
            {
                ShowVerdict("MERRY CHRISTMAS!", christmasLine);
            }
            else
            {
                ChristmasCountdown.Day tomorrow = countdown != null ? countdown.GetDay(dayIndex + 1) : CurrentDay;
                string next = DaysUntilChristmas - 1 <= 1 ? "Tomorrow is Christmas Eve" : $"{DaysUntilChristmas - 1} days until Christmas";
                ShowVerdict($"DAY {DayNumber} DONE", $"{successLine}\n{next}. Santa expects {tomorrow.Quota} tomorrow.");
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

            string restart = DayCount > 1 ? $"\nBack to {DayCount} days until Christmas." : string.Empty;
            ShowVerdict("QUOTA MISSED", failureLine + restart);
        }

        void ResetPresentation()
        {
            StopAllCoroutines();
            VerdictVisible = false;
            VerdictTitle = null;
            VerdictMessage = null;
            if (trapdoor != null) trapdoor.ResetDoors();

            PlayerInteractor player = LocalPlayer();
            if (player == null)
            {
                return;
            }

            if (player.TryGetComponent(out NetworkPlayer networkPlayer))
            {
                networkPlayer.ResetForNewShift();
                return;
            }

            // Solo: back to the start, unfrozen, mouse captured again.
            player.GetComponent<FirstPersonController>().ResetToSpawn();
            player.InteractionEnabled = true;
            var input = player.GetComponent<PlayerInputReader>();
            input.AllowCursorCapture = true;
            input.LockCursor(true);
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

            // Free the mouse for the verdict button.
            foreach (var input in FindObjectsByType<PlayerInputReader>())
            {
                input.AllowCursorCapture = false;
                input.LockCursor(false);
            }
        }
    }
}
