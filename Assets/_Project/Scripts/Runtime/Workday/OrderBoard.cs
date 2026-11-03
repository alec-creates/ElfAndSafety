using System.Text;
using UnityEngine;

namespace ElfAndSafety
{
    /// <summary>
    /// The wish list on the workshop wall: today's orders, ticked off as they ship.
    /// </summary>
    public class OrderBoard : MonoBehaviour
    {
        [SerializeField] WorkdayManager workday;
        [SerializeField] TextMesh label;

        string shown;

        void Update()
        {
            if (workday == null || label == null) return;

            string text = BuildText(workday);
            if (text != shown)
            {
                shown = text;
                label.text = text;
            }
        }

        /// <summary>The board's text; also used by the HUD-free tests.</summary>
        public static string BuildText(WorkdayManager workday)
        {
            var text = new StringBuilder($"WISH LIST  (Day {workday.DayNumber})\n");
            for (int i = 0; i < workday.Orders.Count; i++)
            {
                WishOrder order = workday.Orders[i];
                text.Append(workday.IsOrderFilled(i) ? "[x] " : "[  ] ");
                text.Append(order.Describe());
                text.Append('\n');
            }
            return text.ToString();
        }
    }
}
