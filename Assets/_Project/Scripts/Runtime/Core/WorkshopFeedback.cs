using System;
using UnityEngine;

namespace ElfAndSafety
{
    /// <summary>
    /// Short on-screen messages from stations ("Shipped!", "Rejected: battery is dead"). The HUD listens.
    /// Every message is also written to the Console, so refusals are visible when testing.
    /// </summary>
    public static class WorkshopFeedback
    {
        public static event Action<string, bool> Posted;

        public static void Info(string message)
        {
            Debug.Log($"[Workshop] {message}");
            Posted?.Invoke(message, false);
        }

        public static void Problem(string message)
        {
            Debug.Log($"[Workshop] PROBLEM: {message}");
            Posted?.Invoke(message, true);
        }
    }
}
