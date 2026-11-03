using Unity.Netcode;
using UnityEngine;

namespace ElfAndSafety
{
    /// <summary>
    /// Syncs a toy's dirty state and which actual part sits in each slot (up to <see cref="ToyBody.MaxSlots"/>).
    /// Clients attach the referenced part to the matching mount themselves, so installed parts move with
    /// the toy on every screen.
    /// </summary>
    [RequireComponent(typeof(ToyBody), typeof(NetworkObject))]
    public class NetworkToy : NetworkBehaviour
    {
        readonly NetworkVariable<bool> dirty = new NetworkVariable<bool>(true);
        readonly NetworkVariable<NetworkObjectReference> slot0 = new NetworkVariable<NetworkObjectReference>();
        readonly NetworkVariable<NetworkObjectReference> slot1 = new NetworkVariable<NetworkObjectReference>();
        readonly NetworkVariable<NetworkObjectReference> slot2 = new NetworkVariable<NetworkObjectReference>();
        readonly NetworkVariable<NetworkObjectReference> slot3 = new NetworkVariable<NetworkObjectReference>();

        ToyBody toy;
        NetworkVariable<NetworkObjectReference>[] slots;

        internal bool SyncedDirty => dirty.Value;
        internal NetworkObjectReference SyncedSlot(int slotIndex) => slots[slotIndex].Value;

        void Awake()
        {
            toy = GetComponent<ToyBody>();
            slots = new[] { slot0, slot1, slot2, slot3 };
            if (toy.SlotCount > ToyBody.MaxSlots)
            {
                Debug.LogError($"{name} has {toy.SlotCount} slots; co-op syncs at most {ToyBody.MaxSlots}");
            }
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                Publish();
                return;
            }

            ApplyState();
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer) return;

            // Let installed parts go first, so they aren't destroyed along with this toy's GameObject
            // before their own despawn arrives.
            foreach (ToyPart part in new System.Collections.Generic.List<ToyPart>(toy.InstalledParts))
            {
                part.transform.SetParent(null, true);
            }
        }

        void Update()
        {
            if (!IsSpawned) return;

            if (IsServer) Publish();
            else ApplyState();
        }

        void Publish()
        {
            if (dirty.Value != toy.IsDirty) dirty.Value = toy.IsDirty;

            for (int i = 0; i < SyncedSlotCount; i++)
            {
                NetworkObjectReference current = Reference(toy.GetSlotPart(i));
                if (!slots[i].Value.Equals(current)) slots[i].Value = current;
            }
        }

        void ApplyState()
        {
            if (toy.IsDirty != dirty.Value) toy.SetDirty(dirty.Value);

            for (int i = 0; i < SyncedSlotCount; i++)
            {
                AttachIfNeeded(slots[i].Value, i);
            }
        }

        int SyncedSlotCount => Mathf.Min(toy.SlotCount, ToyBody.MaxSlots);

        void AttachIfNeeded(NetworkObjectReference reference, int slotIndex)
        {
            // Removals arrive through the part's own state (it turns loose or held), which frees the slot.
            if (!reference.TryGet(out NetworkObject networkObject) || !networkObject.TryGetComponent(out ToyPart part))
            {
                return;
            }

            if (toy.GetSlotPart(slotIndex) == null && part.InstalledIn != toy)
            {
                toy.TryInstallAt(slotIndex, part);
            }
        }

        static NetworkObjectReference Reference(ToyPart part)
        {
            return part != null && part.TryGetComponent(out NetworkObject networkObject) && networkObject.IsSpawned
                ? new NetworkObjectReference(networkObject)
                : default;
        }
    }
}
