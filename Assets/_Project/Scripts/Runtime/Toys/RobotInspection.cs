using System.Collections.Generic;

namespace ElfAndSafety
{
    /// <summary>
    /// The shipping rules for a wind-up robot: clean body, installed working battery, installed winding key.
    /// </summary>
    public static class RobotInspection
    {
        public const string DirtyBody = "body is dirty";
        public const string MissingBattery = "battery is missing";
        public const string DeadBattery = "battery is dead";
        public const string MissingKey = "winding key is missing";

        public static List<string> FindProblems(bool isDirty, bool hasBattery, bool batteryWorks, bool hasKey)
        {
            var problems = new List<string>();
            if (isDirty) problems.Add(DirtyBody);
            if (!hasBattery) problems.Add(MissingBattery);
            else if (!batteryWorks) problems.Add(DeadBattery);
            if (!hasKey) problems.Add(MissingKey);
            return problems;
        }

        public static List<string> FindProblems(RobotBody robot)
        {
            return FindProblems(robot.IsDirty, robot.Battery != null, robot.Battery != null && robot.Battery.IsWorking, robot.WindingKey != null);
        }

        /// <summary>True when the robot can ship; otherwise <paramref name="reason"/> lists every problem.</summary>
        public static bool IsShippable(RobotBody robot, out string reason)
        {
            List<string> problems = FindProblems(robot);
            reason = problems.Count == 0 ? null : string.Join(", ", problems);
            return problems.Count == 0;
        }
    }
}
