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
        [SerializeField] float warningSeconds = 30f;
        [SerializeField] Color normalColor = Color.white;
        [SerializeField] Color warningColor = new Color(1f, 0.4f, 0.3f);

        void Awake()
        {
            restartButton.onClick.AddListener(workday.Restart);
            verdictPanel.SetActive(false);
        }

        void Update()
        {
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
                verdictMessage.text = $"\"{workday.VerdictMessage}\"\n- Santa\n\nShipped {workday.Shipped} / {workday.Quota}";
                restartButtonLabel.text = workday.State == ShiftState.Succeeded ? "Play Again (R)" : "Retry (R)";

                if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
                {
                    workday.Restart();
                }
            }
        }

        string StatusText()
        {
            string time = WorkdayManager.FormatTime(workday.State == ShiftState.NotStarted ? workday.ShiftLengthSeconds : workday.RemainingSeconds);
            switch (workday.State)
            {
                case ShiftState.NotStarted:
                    return $"PRACTICE: nothing counts yet. Press the green CLOCK IN button to start your shift ({time}, quota {workday.Quota})";
                case ShiftState.Running:
                    return $"{time} left        Shipped {workday.Shipped} / {workday.Quota}";
                default:
                    return $"Shift over        Shipped {workday.Shipped} / {workday.Quota}";
            }
        }
    }
}
