using System.Collections.Generic;
using UnityEngine;

namespace ElfAndSafety
{
    /// <summary>
    /// Drops delivery waves of broken toys and loose parts onto the delivery table as loose physics objects.
    /// In practice, Request Delivery drops the current day's first wave, one batch at a time; during a shift
    /// the workday delivers the day's waves on schedule. The development reset removes everything delivered,
    /// wherever it ended up.
    /// </summary>
    public class DeliveryChute : MonoBehaviour
    {
        [Tooltip("One prefab per toy type.")]
        [SerializeField] ToyBody[] toyPrefabs;
        [Tooltip("One prefab per part kind and condition (e.g. Battery, Dead Battery, Leaky Battery).")]
        [SerializeField] ToyPart[] partPrefabs;
        [Tooltip("One per delivered item. Items drop in slightly above these and settle with physics.")]
        [SerializeField] Transform[] spawnPoints;
        [SerializeField] float spawnClearance = 0.12f;
        [SerializeField] TextMesh statusLabel;
        [Tooltip("Supplies the practice wave, and closes Request Delivery and DEV reset during a shift.")]
        [SerializeField] WorkdayManager workday;

        readonly List<Carryable> spawned = new List<Carryable>();

        /// <summary>Everything delivered that is still in play, in spawn order.</summary>
        public IReadOnlyList<Carryable> SpawnedItems
        {
            get
            {
                PruneSpawned();
                return spawned;
            }
        }

        public int SpawnPointCount => spawnPoints != null ? spawnPoints.Length : 0;

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

            return CanSpawn(PracticeWave, out reason);
        }

        /// <summary>DEV reset is a practice tool: refused during (and after) a scored shift.</summary>
        public bool CanDevReset(out string reason) => PracticeToolsAllowed(out reason);

        DeliveryWave PracticeWave => workday != null ? workday.PracticeWave : DeliveryWave.Robots();

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

        bool CanSpawn(DeliveryWave wave, out string reason)
        {
            if (wave == null)
            {
                reason = "Nothing to deliver";
                return false;
            }

            foreach (DeliveredToy toy in wave.toys)
            {
                if (FindToyPrefab(toy.toy) == null)
                {
                    reason = $"Delivery chute has no {ToyRecipe.ToyName(toy.toy)} prefab";
                    return false;
                }
                foreach (PartSpec part in toy.installed)
                {
                    if (FindPartPrefab(part) == null) return MissingPart(part, out reason);
                }
            }
            foreach (PartSpec part in wave.looseParts)
            {
                if (FindPartPrefab(part) == null) return MissingPart(part, out reason);
            }

            if (SpawnPointCount < wave.ItemCount)
            {
                reason = $"The {wave.label} delivery has {wave.ItemCount} items but the table only has {SpawnPointCount} spots";
                return false;
            }

            if (!SpawnPointsClear(wave.ItemCount))
            {
                reason = "Delivery table is cluttered. Clear it first";
                return false;
            }

            reason = null;
            return true;
        }

        static bool MissingPart(PartSpec part, out string reason)
        {
            string condition = !part.working ? ToyRecipe.BrokenWord(part.kind) + " " : part.fault != PartFault.None ? ToyPart.TellWord(part.fault) + " " : "";
            reason = $"Delivery chute has no {condition}{ToyRecipe.PartName(part.kind)} prefab";
            return false;
        }

        /// <summary>Practice: drops the day's first wave onto the table. Refused while a batch is still in play.</summary>
        public bool TryRequestDelivery()
        {
            if (!CanRequestDelivery(out string reason))
            {
                WorkshopFeedback.Problem(reason);
                return false;
            }

            SpawnWave(PracticeWave);
            return true;
        }

        /// <summary>
        /// Shift start: clears whatever practice items are around and delivers the first wave.
        /// Bypasses the practice-only gate, which is about to close.
        /// </summary>
        internal bool DeliverForShift(DeliveryWave wave, out string reason)
        {
            ClearBatch();
            if (!CanSpawn(wave, out reason))
            {
                return false;
            }

            SpawnWave(wave);
            return true;
        }

        /// <summary>A later wave during the shift: adds to what's already out without clearing it.</summary>
        internal bool TryDeliverWave(DeliveryWave wave, out string reason)
        {
            if (!CanSpawn(wave, out reason))
            {
                return false;
            }

            SpawnWave(wave);
            return true;
        }

        void SpawnWave(DeliveryWave wave)
        {
            int next = 0;
            foreach (DeliveredToy delivered in wave.toys)
            {
                Transform point = spawnPoints[next++];
                ToyBody toy = Spawn(FindToyPrefab(delivered.toy), point);
                toy.SetDirty(delivered.dirty);
                foreach (PartSpec part in delivered.installed)
                {
                    toy.TryInstall(Spawn(FindPartPrefab(part), point));
                }
            }

            foreach (PartSpec part in wave.looseParts)
            {
                Spawn(FindPartPrefab(part), spawnPoints[next++]);
            }

            WorkshopFeedback.Info($"Delivery! {wave.label} on the delivery table");
            RefreshLabel();
        }

        ToyBody FindToyPrefab(ToyType type)
        {
            if (toyPrefabs == null) return null;
            foreach (ToyBody prefab in toyPrefabs)
            {
                if (prefab != null && prefab.Type == type) return prefab;
            }
            return null;
        }

        ToyPart FindPartPrefab(PartSpec spec)
        {
            if (partPrefabs == null) return null;
            foreach (ToyPart prefab in partPrefabs)
            {
                if (prefab != null && prefab.Kind == spec.kind && prefab.IsWorking == spec.working && prefab.Fault == spec.fault) return prefab;
            }
            return null;
        }

        /// <summary>
        /// Development action: removes everything delivered so a new practice batch can be requested.
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

        /// <summary>Going online or starting over: remove everything delivered, quietly.</summary>
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
