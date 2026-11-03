using UnityEngine;

namespace ElfAndSafety
{
    /// <summary>
    /// Physics layers the gameplay code relies on. The editor setup tool creates them in the Tag Manager.
    /// </summary>
    public static class GameLayers
    {
        public const string PlayerName = "Player";
        public const string CarryableName = "Carryable";

        public static int Player => LayerMask.NameToLayer(PlayerName);
        public static int Carryable => LayerMask.NameToLayer(CarryableName);

        /// <summary>
        /// Players walk through loose toys instead of kicking them around or standing on them.
        /// Applied at runtime so it never depends on a hand-edited collision matrix.
        /// </summary>
        public static void ApplyCollisionRules()
        {
            if (Player < 0 || Carryable < 0)
            {
                Debug.LogWarning("Elf & Safety: 'Player' or 'Carryable' layer is missing. Run Elf & Safety > Build Workshop Scene.");
                return;
            }

            Physics.IgnoreLayerCollision(Player, Carryable, true);
        }
    }
}
