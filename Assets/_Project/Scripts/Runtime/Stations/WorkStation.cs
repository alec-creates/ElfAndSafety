using System.Collections.Generic;
using UnityEngine;

namespace ElfAndSafety
{
    /// <summary>
    /// A station's work area: a trigger box over its work surface. Nothing is locked to the station; it works
    /// on whichever loose robot is resting inside the box, however it got there. Aiming at the area while
    /// carrying something offers to set it down neatly at the place point (it stays a normal physics object).
    /// Put this on the object that has the trigger BoxCollider.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public abstract class WorkStation : MonoBehaviour, IInteractable
    {
        static readonly List<WorkStation> active = new List<WorkStation>();

        [SerializeField] string displayName = "Bench";
        [Tooltip("Where 'Place on' sets items down. Its forward should face the room.")]
        [SerializeField] Transform placePoint;

        BoxCollider area;

        public string DisplayName => displayName;
        public Transform PlacePoint => placePoint != null ? placePoint : transform;

        BoxCollider Area => area != null ? area : area = GetComponent<BoxCollider>();

        protected virtual void OnEnable() => active.Add(this);

        protected virtual void OnDisable() => active.Remove(this);

        /// <summary>The station whose work area contains <paramref name="worldPosition"/>, if any.</summary>
        public static WorkStation Containing(Vector3 worldPosition)
        {
            foreach (WorkStation station in active)
            {
                if (station.Contains(worldPosition)) return station;
            }
            return null;
        }

        public bool Contains(Vector3 worldPosition)
        {
            Vector3 local = Area.transform.InverseTransformPoint(worldPosition) - Area.center;
            Vector3 half = Area.size * 0.5f;
            return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z;
        }

        /// <summary>
        /// The robot this station works on: the one in the area closest to the place point. Loose robots
        /// count, plus any robot this station itself has reserved.
        /// </summary>
        public RobotBody FindRobot()
        {
            RobotBody best = null;
            float bestDistance = float.MaxValue;

            foreach (RobotBody robot in RobotBody.All)
            {
                Carryable item = robot.Carryable;
                bool available = item.State == CarryState.Loose || (item.State == CarryState.Reserved && ReferenceEquals(item.Owner, this));
                if (!available || !Contains(robot.transform.position)) continue;

                float distance = (robot.transform.position - PlacePoint.position).sqrMagnitude;
                if (distance < bestDistance)
                {
                    best = robot;
                    bestDistance = distance;
                }
            }

            return best;
        }

        /// <summary>Override to say what this station wants placed on it. Give a reason when refusing.</summary>
        protected virtual bool AcceptsPlacement(Carryable item, out string reason)
        {
            reason = null;
            return true;
        }

        public virtual bool CanInteract(PlayerInteractor player, out string actionLabel)
        {
            Carryable held = player.Held;
            if (held == null)
            {
                // Empty-handed: aim at the robot or parts on the station instead.
                actionLabel = null;
                return false;
            }

            if (!AcceptsPlacement(held, out string reason))
            {
                actionLabel = reason;
                return false;
            }

            if (held.GetComponent<RobotBody>() != null && FindRobot() != null)
            {
                actionLabel = $"{displayName} already has a robot";
                return false;
            }

            actionLabel = $"Place {held.DisplayName} on {displayName}";
            return true;
        }

        public virtual void Interact(PlayerInteractor player)
        {
            if (CanInteract(player, out _))
            {
                player.RequestPlaceAt(PlacePoint);
            }
        }

        /// <summary>True if something solid already sits at <paramref name="point"/> (players excepted).</summary>
        protected static bool IsBlocked(Vector3 point, float radius)
        {
            Physics.SyncTransforms();
            int mask = ~(1 << GameLayers.Player);
            return Physics.CheckSphere(point, radius, mask, QueryTriggerInteraction.Ignore);
        }
    }
}
