using Unity.Netcode;
using UnityEngine;

namespace ElfAndSafety
{
    /// <summary>
    /// Syncs a robot's dirty state and which actual part sits in each slot. Clients attach the referenced
    /// part to the matching mount themselves, so installed parts move with the robot on every screen.
    /// </summary>
    [RequireComponent(typeof(RobotBody), typeof(NetworkObject))]
    public class NetworkRobot : NetworkBehaviour
    {
        readonly NetworkVariable<bool> dirty = new NetworkVariable<bool>(true);
        readonly NetworkVariable<NetworkObjectReference> battery = new NetworkVariable<NetworkObjectReference>();
        readonly NetworkVariable<NetworkObjectReference> windingKey = new NetworkVariable<NetworkObjectReference>();

        RobotBody robot;

        internal bool SyncedDirty => dirty.Value;
        internal NetworkObjectReference SyncedBattery => battery.Value;
        internal NetworkObjectReference SyncedWindingKey => windingKey.Value;

        void Awake()
        {
            robot = GetComponent<RobotBody>();
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

            // Let installed parts go first, so they aren't destroyed along with this robot's GameObject
            // before their own despawn arrives.
            foreach (RobotPart part in new[] { robot.Battery, robot.WindingKey })
            {
                if (part != null) part.transform.SetParent(null, true);
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
            if (dirty.Value != robot.IsDirty) dirty.Value = robot.IsDirty;

            NetworkObjectReference batteryRef = Reference(robot.Battery);
            if (!battery.Value.Equals(batteryRef)) battery.Value = batteryRef;

            NetworkObjectReference keyRef = Reference(robot.WindingKey);
            if (!windingKey.Value.Equals(keyRef)) windingKey.Value = keyRef;
        }

        void ApplyState()
        {
            if (robot.IsDirty != dirty.Value) robot.SetDirty(dirty.Value);
            AttachIfNeeded(battery.Value, RobotPartKind.Battery);
            AttachIfNeeded(windingKey.Value, RobotPartKind.WindingKey);
        }

        void AttachIfNeeded(NetworkObjectReference reference, RobotPartKind slot)
        {
            // Removals arrive through the part's own state (it turns loose or held), which frees the slot.
            if (!reference.TryGet(out NetworkObject networkObject) || !networkObject.TryGetComponent(out RobotPart part))
            {
                return;
            }

            if (part.Kind == slot && robot.GetPart(slot) != part && robot.GetPart(slot) == null)
            {
                robot.TryInstall(part);
            }
        }

        static NetworkObjectReference Reference(RobotPart part)
        {
            return part != null && part.TryGetComponent(out NetworkObject networkObject) && networkObject.IsSpawned
                ? new NetworkObjectReference(networkObject)
                : default;
        }
    }
}
