using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ElfAndSafety
{
    /// <summary>
    /// Shift status line (time left, shipped versus quota) and Santa's verdict panel with the restart button.
    /// </summary>
    public class WorkdayHud : MonoBehaviour
    {
        [SerializeField] WorkdayManager workday;
        [SerializeField] Text shiftLabel;
        [SerializeField] GameObject verdictPanel;
        [SerializeField] Text verdictTitle;
        [SerializeField] Text verdictMessage;
        [SerializeField] Button restartButton;
        [SerializeField] Text restartButtonLabel;
        [SerializeField] Button mainMenuButton;
        [Tooltip("Today's wish list on screen (Settings: Show wish list).")]
        [SerializeField] Text wishListLabel;
        [Tooltip("The controls hint (Settings: Show controls).")]
        [SerializeField] GameObject controlsHint;
        [SerializeField] float warningSeconds = 30f;
        [SerializeField] Color normalColor = Color.white;
        [SerializeField] Color warningColor = new Color(1f, 0.4f, 0.3f);

        void Awake()
        {
            restartButton.onClick.AddListener(workday.Continue);
            if (mainMenuButton != null) mainMenuButton.onClick.AddListener(() => GameLaunch.ReturnToMainMenu());
            verdictPanel.SetActive(false);
        }

        void Update()
        {
            if (wishListLabel != null)
            {
                bool showList = GameSettings.ShowWishList;
                if (wishListLabel.gameObject.activeSelf != showList) wishListLabel.gameObject.SetActive(showList);
                if (showList) wishListLabel.text = OrderBoard.BuildText(workday);
            }

            if (controlsHint != null && controlsHint.activeSelf != GameSettings.ShowControls)
            {
                controlsHint.SetActive(GameSettings.ShowControls);
            }

            shiftLabel.text = StatusText();
            shiftLabel.color = workday.State == ShiftState.Running && workday.RemainingSeconds <= warningSeconds ? warningColor : normalColor;

            bool showVerdict = workday.VerdictVisible;
            if (verdictPanel.activeSelf != showVerdict)
            {
                verdictPanel.SetActive(showVerdict);
            }

            if (showVerdict)
            {
                verdictTitle.text = workday.VerdictTitle;
                verdictMessage.text = $"\"{workday.VerdictMessage}\"\n- Santa\n\nWishes filled {workday.Shipped} / {workday.Quota}";
                restartButtonLabel.text = $"{workday.VerdictActionLabel} (R)";

                if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
                {
                    workday.Continue();
                }
            }
        }

        string StatusText()
        {
            string time = WorkdayManager.FormatTime(workday.State == ShiftState.NotStarted ? workday.ShiftLengthSeconds : workday.RemainingSeconds);
            string day = $"DAY {workday.DayNumber} · {workday.DayTitle}";
            switch (workday.State)
            {
                case ShiftState.NotStarted:
                    string note = string.IsNullOrEmpty(workday.SantaNote) ? string.Empty : $"\nSanta: \"{workday.SantaNote}\"";
                    return $"{day}        PRACTICE: nothing counts yet. Press the green CLOCK IN button ({time}, {workday.Quota} wishes on the WISH LIST){note}";
                case ShiftState.Running:
                    string waves = workday.DeliveryWaves > 1 ? $"        Deliveries {workday.WavesDelivered} / {workday.DeliveryWaves}" : string.Empty;
                    return $"{day}        {time} left        Wishes filled {workday.Shipped} / {workday.Quota}{waves}";
                default:
                    return $"{day}        Shift over        Wishes filled {workday.Shipped} / {workday.Quota}";
            }
        }
    }
}
