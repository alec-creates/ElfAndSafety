using System;
using UnityEngine;

namespace ElfAndSafety
{
    /// <summary>
    /// Standalone count of accepted robots. The workday and quota system will build on this later.
    /// </summary>
    public class ShipmentCounter : MonoBehaviour
    {
        public int Shipped { get; private set; }

        public event Action<int> Changed;

        internal void RecordShipment()
        {
            Shipped++;
            Changed?.Invoke(Shipped);
        }

        /// <summary>Co-op client: show the host's count.</summary>
        internal void MirrorShipped(int shipped)
        {
            if (Shipped == shipped) return;
            Shipped = shipped;
            Changed?.Invoke(Shipped);
        }

        /// <summary>Clears practice shipments when a scored shift starts.</summary>
        internal void ResetCount()
        {
            Shipped = 0;
            Changed?.Invoke(Shipped);
        }
    }
}
