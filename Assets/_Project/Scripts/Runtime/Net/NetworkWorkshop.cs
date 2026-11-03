using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace ElfAndSafety
{
    /// <summary>
    /// The shared workshop state for a co-op session, spawned by the host: shift state and timer, shipped
    /// count, whether a delivery batch is out, cleaning progress, and the last inspection result. The host
    /// publishes its own components each frame; clients copy the values into theirs, so their existing HUD,
    /// labels and prompts show the host's game. Station messages are sent to every player.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class NetworkWorkshop : NetworkBehaviour
    {
        readonly NetworkVariable<ShiftState> shiftState = new NetworkVariable<ShiftState>(ShiftState.NotStarted);
        readonly NetworkVariable<float> remaining = new NetworkVariable<float>();
        readonly NetworkVariable<int> shipped = new NetworkVariable<int>();
        readonly NetworkVariable<bool> batchOut = new NetworkVariable<bool>();
        readonly NetworkVariable<bool> cleaningActive = new NetworkVariable<bool>();
        readonly NetworkVariable<float> cleaningProgress = new NetworkVariable<float>();
        readonly NetworkVariable<FixedString128Bytes> lastShipResult = new NetworkVariable<FixedString128Bytes>();

        WorkdayManager workday;
        ShipmentCounter counter;
        DeliveryChute delivery;
        CleaningStation cleaning;
        ShippingChute shipping;

        public static NetworkWorkshop Instance { get; private set; }

        internal ShiftState SyncedShiftState => shiftState.Value;
        internal int SyncedShipped => shipped.Value;

        public override void OnNetworkSpawn()
        {
            Instance = this;
            workday = FindAnyObjectByType<WorkdayManager>();
            counter = FindAnyObjectByType<ShipmentCounter>();
            delivery = FindAnyObjectByType<DeliveryChute>();
            cleaning = FindAnyObjectByType<CleaningStation>();
            shipping = FindAnyObjectByType<ShippingChute>();

            if (IsServer)
            {
                WorkshopFeedback.Posted += Broadcast;
                Publish();
            }
        }

        public override void OnNetworkDespawn()
        {
            if (Instance == this) Instance = null;
            if (IsServer) WorkshopFeedback.Posted -= Broadcast;
        }

        void Update()
        {
            if (!IsSpawned) return;

            if (IsServer) Publish();
            else Apply();
        }

        void Publish()
        {
            if (workday != null)
            {
                if (shiftState.Value != workday.State) shiftState.Value = workday.State;
                if (Mathf.Abs(remaining.Value - workday.RemainingSeconds) > 0.05f || workday.RemainingSeconds <= 0f && remaining.Value != 0f)
                {
                    remaining.Value = workday.RemainingSeconds;
                }
            }

            if (counter != null && shipped.Value != counter.Shipped) shipped.Value = counter.Shipped;
            if (delivery != null && batchOut.Value != delivery.HasActiveBatch) batchOut.Value = delivery.HasActiveBatch;

            if (cleaning != null)
            {
                if (cleaningActive.Value != cleaning.IsCleaning) cleaningActive.Value = cleaning.IsCleaning;
                if (Mathf.Abs(cleaningProgress.Value - cleaning.Progress) > 0.01f) cleaningProgress.Value = cleaning.Progress;
            }

            if (shipping != null)
            {
                var result = new FixedString128Bytes(Truncate(shipping.LastResult, 100));
                if (!lastShipResult.Value.Equals(result)) lastShipResult.Value = result;
            }
        }

        void Apply()
        {
            // Count first: the workday may present a verdict that shows the shipped count.
            if (counter != null) counter.MirrorShipped(shipped.Value);
            if (delivery != null) delivery.MirrorBatchOut(batchOut.Value);
            if (cleaning != null) cleaning.MirrorCleaning(cleaningActive.Value, cleaningProgress.Value);
            if (shipping != null) shipping.MirrorLastResult(lastShipResult.Value.ToString());
            if (workday != null) workday.ApplyMirror(shiftState.Value, remaining.Value);
        }

        // --- Station messages for everyone, and restarting together. ---

        void Broadcast(string message, bool isProblem)
        {
            FeedbackRpc(new FixedString512Bytes(Truncate(message, 200)), isProblem);
        }

        [Rpc(SendTo.NotServer)]
        void FeedbackRpc(FixedString512Bytes message, bool isProblem)
        {
            if (isProblem) WorkshopFeedback.Problem(message.ToString());
            else WorkshopFeedback.Info(message.ToString());
        }

        /// <summary>Play Again / Retry from any player: the host resets the workshop for everyone.</summary>
        internal void RequestRestart() => RestartRpc();

        [Rpc(SendTo.Server)]
        void RestartRpc()
        {
            if (workday != null) workday.ResetShift();
        }

        static string Truncate(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return text.Length <= maxLength ? text : text.Substring(0, maxLength);
        }
    }
}
