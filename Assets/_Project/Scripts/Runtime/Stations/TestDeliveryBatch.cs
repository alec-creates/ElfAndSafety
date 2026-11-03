using System;
using System.Collections.Generic;
using UnityEngine;

namespace ElfAndSafety
{
    /// <summary>
    /// What one test delivery contains. The defaults are deliberately solvable: three robot bodies, two
    /// working batteries and two winding keys in total, so exactly two robots can be completed.
    /// </summary>
    [CreateAssetMenu(menuName = "Elf & Safety/Test Delivery Batch", fileName = "Test Delivery Batch")]
    public class TestDeliveryBatch : ScriptableObject
    {
        public enum BatteryCondition { None, Working, Dead }
        public enum LoosePart { WorkingBattery, DeadBattery, WindingKey }

        [Serializable]
        public class RobotEntry
        {
            public bool dirty = true;
            public BatteryCondition battery = BatteryCondition.None;
            public bool hasKey;
        }

        public List<RobotEntry> robots = new List<RobotEntry>
        {
            new RobotEntry { dirty = true, battery = BatteryCondition.Dead, hasKey = true },
            new RobotEntry { dirty = true, battery = BatteryCondition.None, hasKey = false },
            new RobotEntry { dirty = false, battery = BatteryCondition.Working, hasKey = false },
        };

        public List<LoosePart> looseParts = new List<LoosePart>
        {
            LoosePart.WorkingBattery,
            LoosePart.WindingKey,
            LoosePart.DeadBattery,
        };

        public int ItemCount => robots.Count + looseParts.Count;

        /// <summary>How many valid robots the batch can produce if every usable part is used.</summary>
        public int CompletableRobots()
        {
            int workingBatteries = 0;
            int keys = 0;

            foreach (RobotEntry robot in robots)
            {
                if (robot.battery == BatteryCondition.Working) workingBatteries++;
                if (robot.hasKey) keys++;
            }

            foreach (LoosePart part in looseParts)
            {
                if (part == LoosePart.WorkingBattery) workingBatteries++;
                if (part == LoosePart.WindingKey) keys++;
            }

            return Mathf.Min(robots.Count, Mathf.Min(workingBatteries, keys));
        }
    }
}
