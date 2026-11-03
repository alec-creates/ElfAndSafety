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
    }
}
