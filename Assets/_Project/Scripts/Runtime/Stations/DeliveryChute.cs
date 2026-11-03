using System.Collections.Generic;
using UnityEngine;

namespace ElfAndSafety
{
    /// <summary>
    /// Drops one test batch of broken robots and parts onto the delivery table as loose physics objects.
    /// Only one batch exists at a time; the development reset removes everything the batch spawned,
    /// wherever it ended up.
    /// </summary>
    public class DeliveryChute : MonoBehaviour
    {
        [SerializeField] TestDeliveryBatch batch;
        [SerializeField] RobotBody robotPrefab;
        [SerializeField] RobotPart batteryPrefab;
        [SerializeField] RobotPart deadBatteryPrefab;
        [SerializeField] RobotPart windingKeyPrefab;
        [Tooltip("One per delivered item. Items drop in slightly above these and settle with physics.")]
        [SerializeField] Transform[] spawnPoints;
        [SerializeField] float spawnClearance = 0.12f;
        [SerializeField] TextMesh statusLabel;
        [Tooltip("Deliveries are refused if the batch can't produce at least this many valid robots.")]
        [SerializeField] int minimumCompletableRobots = 2;
        [Tooltip("Optional. When set, Request Delivery and DEV reset only work before the shift starts.")]
        [SerializeField] WorkdayManager workday;

        readonly List<Carryable> spawned = new List<Carryable>();

        /// <summary>Everything the current batch spawned that is still in play, in spawn order.</summary>
        public IReadOnlyList<Carryable> SpawnedItems
        {
            get
            {
                PruneSpawned();
                return spawned;
            }
        }

        /// <summary>On a co-op client the batch lives on the host, so this mirrors the host's answer.</summary>
        public bool HasActiveBatch => NetworkSession.IsRemoteClient ? mirroredBatchOut : SpawnedItems.Count > 0;

        bool mirroredBatchOut;

        internal void MirrorBatchOut(bool batchOut) => mirroredBatchOut = batchOut;

        // Items leave play through shipping too, so keep the label current every frame (the list is tiny).
        void Update() => RefreshLabel();

        public bool CanRequestDelivery(out string reason)
        {
            if (!PracticeToolsAllowed(out reason))
            {
                return false;
            }

            if (HasActiveBatch)
            {
                reason = "A batch is already out. Use DEV RESET for a fresh one";
                return false;
            }

            return CanSpawnBatch(out reason);
        }

        /// <summary>DEV reset is a practice tool: refused during (and after) a scored shift.</summary>
        public bool CanDevReset(out string reason) => PracticeToolsAllowed(out reason);

        bool PracticeToolsAllowed(out string reason)
        {
            if (workday == null || workday.PracticeToolsAllowed)
            {
                reason = null;
                return true;
            }

            reason = workday.State == ShiftState.Running ? "Disabled during a scored shift" : "The shift is over";
            return false;
        }

        bool CanSpawnBatch(out string reason)
        {
            if (batch == null || robotPrefab == null || batteryPrefab == null || deadBatteryPrefab == null || windingKeyPrefab == null)
            {
                reason = "Delivery chute is not set up";
                return false;
            }

            if (batch.CompletableRobots() < minimumCompletableRobots)
            {
                reason = $"Test batch can't complete {minimumCompletableRobots} robots. Fix the batch asset";
                return false;
            }

            if (spawnPoints.Length < batch.ItemCount)
            {
                reason = $"Test batch has {batch.ItemCount} items but the table only has {spawnPoints.Length} spots";
                return false;
            }

            if (!SpawnPointsClear(batch.ItemCount))
            {
                reason = "Delivery table is cluttered. Clear it first";
                return false;
            }

            reason = null;
            return true;
        }

        /// <summary>Spawns the batch onto the delivery table. Refused while a batch is still in play.</summary>
        public bool TryRequestDelivery()
        {
            if (!CanRequestDelivery(out string reason))
            {
                WorkshopFeedback.Problem(reason);
                return false;
            }

            SpawnBatch();
            return true;
        }

        /// <summary>
        /// Shift start: clears whatever practice items are around and delivers the one batch for the shift.
        /// Bypasses the practice-only gate, which is about to close.
        /// </summary>
        internal bool DeliverForShift(out string reason)
        {
            ClearBatch();
            if (!CanSpawnBatch(out reason))
            {
                return false;
            }

            SpawnBatch();
            return true;
        }

        void SpawnBatch()
        {
            int next = 0;
            foreach (TestDeliveryBatch.RobotEntry entry in batch.robots)
            {
                Transform point = spawnPoints[next++];
                RobotBody robot = Spawn(robotPrefab, point);
                robot.SetDirty(entry.dirty);
                if (entry.battery == TestDeliveryBatch.BatteryCondition.Working) robot.TryInstall(Spawn(batteryPrefab, point));
                if (entry.battery == TestDeliveryBatch.BatteryCondition.Dead) robot.TryInstall(Spawn(deadBatteryPrefab, point));
                if (entry.hasKey) robot.TryInstall(Spawn(windingKeyPrefab, point));
            }

            foreach (TestDeliveryBatch.LoosePart part in batch.looseParts)
            {
                RobotPart prefab = part switch
                {
                    TestDeliveryBatch.LoosePart.WorkingBattery => batteryPrefab,
                    TestDeliveryBatch.LoosePart.DeadBattery => deadBatteryPrefab,
                    _ => windingKeyPrefab,
                };
                Spawn(prefab, spawnPoints[next++]);
            }

            WorkshopFeedback.Info("Delivery! Broken robots on the delivery table");
            RefreshLabel();
        }

        /// <summary>
        /// Development action: removes every item from the current batch so a new one can be requested.
        /// Refused during a scored shift so it can't be used to replenish parts.
        /// </summary>
        public bool ResetTestBatch()
        {
            if (!CanDevReset(out string reason))
            {
                WorkshopFeedback.Problem($"DEV reset: {reason}");
                return false;
            }

            ClearBatch();
            WorkshopFeedback.Info("DEV: test batch cleared");
            return true;
        }

        /// <summary>Going online: practice items exist only on this machine, so remove them quietly.</summary>
        internal void ClearForSession() => ClearBatch();

        void ClearBatch()
        {
            // Copy first: consuming items releases them from their owners, which may touch this list's items.
            foreach (Carryable item in new List<Carryable>(SpawnedItems))
            {
                item.Consume();
            }

            spawned.Clear();
            RefreshLabel();
        }

        T Spawn<T>(T prefab, Transform point) where T : Component
        {
            T instance = Instantiate(prefab, point.position, point.rotation);
            if (NetworkSession.IsServer && instance.TryGetComponent(out Unity.Netcode.NetworkObject networkObject))
            {
                networkObject.Spawn(destroyWithScene: true);
            }
            spawned.Add(instance.GetComponent<Carryable>());
            return instance;
        }

        bool SpawnPointsClear(int count)
        {
            Physics.SyncTransforms();
            int mask = ~(1 << GameLayers.Player);
            for (int i = 0; i < count; i++)
            {
                if (spawnPoints[i] == null || Physics.CheckSphere(spawnPoints[i].position, spawnClearance, mask, QueryTriggerInteraction.Ignore)) return false;
            }
            return true;
        }

        void PruneSpawned()
        {
            spawned.RemoveAll(item => item == null || item.State == CarryState.Consumed);
        }

        void RefreshLabel()
        {
            if (statusLabel != null)
            {
                statusLabel.text = HasActiveBatch ? "Batch delivered" : "Ready for a delivery";
            }
        }
    }
}
