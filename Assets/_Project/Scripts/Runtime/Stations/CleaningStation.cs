using UnityEngine;

namespace ElfAndSafety
{
    /// <summary>
    /// When a dirty robot comes to rest in the tub, the station holds it still and scrubs it, then lets go.
    /// The body is reserved by the operation, so it can't be taken mid-scrub.
    /// </summary>
    public class CleaningStation : WorkStation, ICarryableOwner, IReservingStation
    {
        [SerializeField] float cleanDuration = 3f;
        [Tooltip("Scaled along X from 0 to 1 to show progress. Pivot it at the bar's left end.")]
        [SerializeField] Transform progressFill;
        [SerializeField] TextMesh statusLabel;

        RobotBody cleaning;
        float progress;

        public RobotBody Cleaning => cleaning;
        public bool IsCleaning => cleaning != null || mirrorActive;

        // Co-op client: the host runs the scrub; these mirror its progress for the bar and label.
        bool mirrorActive;

        internal void MirrorCleaning(bool active, float hostProgress)
        {
            mirrorActive = active;
            progress = hostProgress;
        }
        public float Progress => progress;
        public float CleanDuration => cleanDuration;

        public string BusyMessage => $"Scrubbing... {Mathf.RoundToInt(progress * 100f)}%";

        protected override bool AcceptsPlacement(Carryable item, out string reason)
        {
            var robot = item.GetComponent<RobotBody>();
            if (robot == null)
            {
                reason = "Only robot bodies can be scrubbed";
                return false;
            }

            if (!robot.IsDirty)
            {
                reason = "Already clean. No scrubbing needed";
                return false;
            }

            reason = null;
            return true;
        }

        void Update()
        {
            // The host runs the scrub; a co-op client just sees the robot's clean state change.
            if (NetworkSession.IsRemoteClient)
            {
                Refresh();
                return;
            }

            if (cleaning == null)
            {
                TryStartCleaning();
            }
            else
            {
                progress = Mathf.Clamp01(progress + Time.deltaTime / Mathf.Max(0.01f, cleanDuration));
                if (progress >= 1f)
                {
                    FinishCleaning();
                }
            }

            Refresh();
        }

        void TryStartCleaning()
        {
            RobotBody robot = FindRobot();
            if (robot == null || !robot.IsDirty || !robot.Carryable.IsResting)
            {
                return;
            }

            // Hold it upright at the scrub point while the brushes work.
            robot.Carryable.SetReserved(this, PlacePoint.position, PlacePoint.rotation);
            cleaning = robot;
            progress = 0f;
        }

        void FinishCleaning()
        {
            RobotBody robot = cleaning;
            cleaning = null;
            robot.SetDirty(false);
            robot.Carryable.SetLoose(PlacePoint.position, PlacePoint.rotation, Vector3.zero);
            WorkshopFeedback.Info($"{robot.DisplayName} is sparkling clean");
        }

        void ICarryableOwner.OnCarryableReleased(Carryable item)
        {
            // Released some other way (e.g. a reset destroyed it mid-scrub): the operation simply stops.
            if (cleaning != null && cleaning.Carryable == item)
            {
                cleaning = null;
                progress = 0f;
            }
        }

        void Refresh()
        {
            RobotBody robot = cleaning != null ? cleaning : FindRobot();

            if (progressFill != null)
            {
                float shown = IsCleaning ? progress : robot != null && !robot.IsDirty ? 1f : 0f;
                progressFill.localScale = new Vector3(Mathf.Max(0.001f, shown), 1f, 1f);
            }

            if (statusLabel != null)
            {
                statusLabel.text = IsCleaning ? BusyMessage : robot != null && !robot.IsDirty ? "Clean! Take it" : "Drop a dirty robot in";
            }
        }
    }
}
