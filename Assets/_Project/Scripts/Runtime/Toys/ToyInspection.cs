using System.Collections.Generic;
using System.Linq;

namespace ElfAndSafety
{
    /// <summary>
    /// Shipping rules: a toy passes against a recipe when it's the right toy, its body is clean, every part
    /// the recipe needs is installed and working, and nothing else is installed.
    /// </summary>
    public static class ToyInspection
    {
        public const string DirtyBody = "body is dirty";
        public const string MissingBattery = "battery is missing";
        public const string DeadBattery = "battery is dead";
        public const string MissingKey = "winding key is missing";

        public static List<string> FindProblems(ToyBody toy, ToyRecipe recipe)
        {
            var problems = new List<string>();
            if (toy.Type != recipe.Toy)
            {
                problems.Add($"this is a {ToyRecipe.ToyName(toy.Type)}, not a {recipe.Name}");
                return problems;
            }

            if (toy.IsDirty) problems.Add(DirtyBody);

            List<ToyPart> installed = toy.InstalledParts.ToList();
            foreach (PartKind kind in recipe.Parts.Distinct())
            {
                int needed = recipe.Count(kind);
                List<ToyPart> ofKind = installed.FindAll(p => p.Kind == kind);
                string name = ToyRecipe.PartName(kind);

                int missing = needed - ofKind.Count;
                if (missing > 0)
                {
                    problems.Add(needed == 1 ? $"{name} is missing" : $"{missing} of {needed} {name}s missing");
                }

                int broken = ofKind.Take(needed).Count(p => !p.IsWorking);
                if (broken > 0)
                {
                    problems.Add(needed == 1 ? $"{name} is {ToyRecipe.BrokenWord(kind)}" : $"{broken} {name}{(broken > 1 ? "s" : "")} {ToyRecipe.BrokenWord(kind)}");
                }
            }

            foreach (PartKind kind in installed.Select(p => p.Kind).Distinct())
            {
                if (installed.Count(p => p.Kind == kind) > recipe.Count(kind))
                {
                    problems.Add($"{ToyRecipe.PartName(kind)} wasn't on the wish list");
                }
            }

            return problems;
        }

        /// <summary>The milestone 2 robot rules from plain flags (kept for the rule tests).</summary>
        public static List<string> FindProblems(bool isDirty, bool hasBattery, bool batteryWorks, bool hasKey)
        {
            var problems = new List<string>();
            if (isDirty) problems.Add(DirtyBody);
            if (!hasBattery) problems.Add(MissingBattery);
            else if (!batteryWorks) problems.Add(DeadBattery);
            if (!hasKey) problems.Add(MissingKey);
            return problems;
        }

        public static bool Passes(ToyBody toy, ToyRecipe recipe, out string reason)
        {
            List<string> problems = FindProblems(toy, recipe);
            reason = problems.Count == 0 ? null : string.Join(", ", problems);
            return problems.Count == 0;
        }

        /// <summary>Outside a shift: does the toy pass as its ordinary version?</summary>
        public static bool IsShippable(ToyBody toy, out string reason) => Passes(toy, ToyRecipe.Standard(toy.Type), out reason);

        /// <summary>The recipe this toy currently completes exactly (e.g. cyborg bear), or null.</summary>
        public static ToyRecipe CompleteRecipe(ToyBody toy)
        {
            foreach (ToyRecipe recipe in ToyRecipe.All)
            {
                if (recipe.Toy == toy.Type && FindProblems(toy, recipe).Count == 0) return recipe;
            }
            return null;
        }
    }
}
