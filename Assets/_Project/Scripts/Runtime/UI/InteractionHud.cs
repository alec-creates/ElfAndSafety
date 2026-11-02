using UnityEngine;
using UnityEngine.UI;

namespace ElfAndSafety
{
    /// <summary>
    /// Crosshair plus prompts for the local player: what Interact will do, and what is being carried.
    /// </summary>
    public class InteractionHud : MonoBehaviour
    {
        [SerializeField] PlayerInteractor player;
        [SerializeField] Text promptLabel;
        [SerializeField] Text carryLabel;
        [SerializeField] Graphic crosshair;
        [SerializeField] Color idleCrosshairColor = new Color(1f, 1f, 1f, 0.5f);
        [SerializeField] Color activeCrosshairColor = new Color(1f, 0.82f, 0.2f, 1f);

        public void Bind(PlayerInteractor target) => player = target;

        void Update()
        {
            bool hasTarget = player != null && player.Target != null;
            promptLabel.text = hasTarget ? $"[E] {player.TargetAction}" : string.Empty;

            Carryable held = player != null ? player.Held : null;
            carryLabel.text = held != null ? $"Carrying: {held.DisplayName}    [Q] Drop" : string.Empty;

            crosshair.color = hasTarget ? activeCrosshairColor : idleCrosshairColor;
        }
    }
}
