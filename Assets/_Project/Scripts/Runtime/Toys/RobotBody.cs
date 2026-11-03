using System;
using System.Collections.Generic;
using UnityEngine;

namespace ElfAndSafety
{
    /// <summary>
    /// A wind-up robot body with one battery slot and one winding-key slot. Each slot refers to the actual
    /// <see cref="RobotPart"/> installed there; installed parts are parented to a mount so they move with it.
    /// </summary>
    [RequireComponent(typeof(Carryable))]
    public class RobotBody : MonoBehaviour, ICarryableOwner, IDescribable
    {
        [SerializeField] bool isDirty = true;
        [SerializeField] Transform batteryMount;
        [SerializeField] Transform keyMount;
        [Tooltip("Shown while the body is dirty. Replace with any dirt art.")]
        [SerializeField] GameObject dirtVisual;

        static readonly List<RobotBody> all = new List<RobotBody>();

        RobotPart battery;
        RobotPart windingKey;
        Carryable carryable;

        /// <summary>Every robot currently in play, so stations can find the one in their work area.</summary>
        public static IReadOnlyList<RobotBody> All => all;

        public bool IsDirty => isDirty;
        public RobotPart Battery => battery;
        public RobotPart WindingKey => windingKey;
        public Carryable Carryable => carryable != null ? carryable : carryable = GetComponent<Carryable>();
        public string DisplayName => Carryable.DisplayName;

        /// <summary>Raised when a part is installed or removed, or the body is cleaned.</summary>
        public event Action<RobotBody> Changed;

        void Awake()
        {
            RefreshDirtVisual();
        }

        void OnEnable() => all.Add(this);

        void OnDisable() => all.Remove(this);

        public RobotPart GetPart(RobotPartKind slot) => slot == RobotPartKind.Battery ? battery : windingKey;

        public bool CanInstall(RobotPart part, out string reason)
        {
            if (part == null)
            {
                reason = "Only robot parts can be installed";
                return false;
            }

            if (part.InstalledIn == this)
            {
                reason = $"{part.DisplayName} is already installed";
                return false;
            }

            if (GetPart(part.Kind) != null)
            {
                reason = $"The {RobotPart.SlotName(part.Kind)} slot is already filled";
                return false;
            }

            reason = null;
            return true;
        }

        // --- State changes, called by stations and deliveries. ---

        /// <summary>Moves the actual part into its slot, releasing it from wherever it was.</summary>
        internal bool TryInstall(RobotPart part)
        {
            if (!CanInstall(part, out _) || part.Carryable.State == CarryState.Consumed)
            {
                return false;
            }

            part.Carryable.SetInstalled(this, MountFor(part.Kind));
            if (part.Kind == RobotPartKind.Battery) battery = part;
            else windingKey = part;

            Changed?.Invoke(this);
            return true;
        }

        internal void SetDirty(bool dirty)
        {
            if (isDirty == dirty)
            {
                return;
            }

            isDirty = dirty;
            RefreshDirtVisual();
            Changed?.Invoke(this);
        }

        void ICarryableOwner.OnCarryableReleased(Carryable item)
        {
            // A part leaving its slot always comes through here, however it was moved.
            if (battery != null && battery.Carryable == item) battery = null;
            else if (windingKey != null && windingKey.Carryable == item) windingKey = null;
            else return;

            if (this != null)
            {
                Changed?.Invoke(this);
            }
        }

        public string Describe()
        {
            string body = isDirty ? "DIRTY" : "clean";
            string batteryText = battery == null ? "missing" : battery.IsWorking ? "working" : "DEAD";
            string keyText = windingKey == null ? "missing" : "OK";
            string verdict = RobotInspection.IsShippable(this, out _) ? "   (ready to ship)" : string.Empty;
            return $"{DisplayName}: body {body} | battery {batteryText} | key {keyText}{verdict}";
        }

        Transform MountFor(RobotPartKind slot) => slot == RobotPartKind.Battery ? batteryMount : keyMount;

        void RefreshDirtVisual()
        {
            if (dirtVisual != null)
            {
                dirtVisual.SetActive(isDirty);
            }
        }
    }
}
