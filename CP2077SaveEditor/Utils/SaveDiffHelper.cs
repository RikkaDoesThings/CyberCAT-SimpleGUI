using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WolvenKit.RED4.Save;
using WolvenKit.RED4.Save.Classes;
using WolvenKit.RED4.Types;

namespace CP2077SaveEditor.Utils
{
    public record DiffEntry(string Category, string Name, string Before, string After);

    public class SaveDiffReport
    {
        public List<DiffEntry> Entries { get; } = new();

        public string FormatText()
        {
            if (Entries.Count == 0)
            {
                return "No differences found in Attributes, Perks, Quest Facts, Player Inventory, or Vehicles.";
            }

            var sb = new StringBuilder();
            foreach (var group in Entries.GroupBy(e => e.Category))
            {
                sb.AppendLine($"=== {group.Key} ({group.Count()}) ===");
                foreach (var item in group)
                {
                    if (string.IsNullOrEmpty(item.Before))
                    {
                        sb.AppendLine($"  + {item.Name}: {item.After}");
                    }
                    else if (string.IsNullOrEmpty(item.After))
                    {
                        sb.AppendLine($"  - {item.Name}: {item.Before}");
                    }
                    else
                    {
                        sb.AppendLine($"  ~ {item.Name}: {item.Before} -> {item.After}");
                    }
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }
    }

    /// <summary>
    /// Compares two loaded saves across Attributes, Perks, Quest Facts, Player Inventory, and Vehicles.
    /// </summary>
    public static class SaveDiffHelper
    {
        public static SaveDiffReport Compare(SaveFileHelper before, SaveFileHelper after)
        {
            if (before == null) throw new ArgumentNullException(nameof(before));
            if (after == null) throw new ArgumentNullException(nameof(after));

            var report = new SaveDiffReport();
            CompareAttributes(before, after, report);
            ComparePerks(before, after, report);
            CompareFacts(before, after, report);
            CompareInventory(before, after, report);
            CompareVehicles(before, after, report);
            return report;
        }

        private static void CompareAttributes(SaveFileHelper before, SaveFileHelper after, SaveDiffReport report)
        {
            var a = BuildPresetHelper.Export(before).Attributes;
            var b = BuildPresetHelper.Export(after).Attributes;

            foreach (var key in a.Keys.Union(b.Keys).OrderBy(k => k))
            {
                a.TryGetValue(key, out var va);
                b.TryGetValue(key, out var vb);
                if (va != vb)
                {
                    report.Entries.Add(new DiffEntry("Attributes", key, va.ToString(), vb.ToString()));
                }
            }
        }

        private static void ComparePerks(SaveFileHelper before, SaveFileHelper after, SaveDiffReport report)
        {
            var p1 = BuildPresetHelper.Export(before).Perks;
            var p2 = BuildPresetHelper.Export(after).Perks;

            foreach (var key in p1.Keys.Union(p2.Keys).OrderBy(k => k))
            {
                p1.TryGetValue(key, out var v1);
                p2.TryGetValue(key, out var v2);
                if (v1 != v2)
                {
                    report.Entries.Add(new DiffEntry("Perks", key, v1.ToString(), v2.ToString()));
                }
            }
        }

        private static void CompareFacts(SaveFileHelper before, SaveFileHelper after, SaveDiffReport report)
        {
            var f1 = ReadFacts(before);
            var f2 = ReadFacts(after);

            foreach (var key in f1.Keys.Union(f2.Keys).OrderBy(k => k))
            {
                var has1 = f1.TryGetValue(key, out var v1);
                var has2 = f2.TryGetValue(key, out var v2);

                if (!has1 && has2)
                {
                    report.Entries.Add(new DiffEntry("Quest Facts", key, "", v2.ToString()));
                }
                else if (has1 && !has2)
                {
                    report.Entries.Add(new DiffEntry("Quest Facts", key, v1.ToString(), ""));
                }
                else if (has1 && has2 && v1 != v2)
                {
                    report.Entries.Add(new DiffEntry("Quest Facts", key, v1.ToString(), v2.ToString()));
                }
            }
        }

        private static Dictionary<string, uint> ReadFacts(SaveFileHelper save)
        {
            var dict = new Dictionary<string, uint>(StringComparer.Ordinal);
            foreach (FactsTable.FactEntry entry in save.GetKnownFacts())
            {
                var name = save.KnownFacts.TryGetValue(entry.FactName, out var resolved)
                    ? resolved
                    : $"0x{entry.FactName:X}";
                dict[name] = entry.Value;
            }
            return dict;
        }

        private static void CompareInventory(SaveFileHelper before, SaveFileHelper after, SaveDiffReport report)
        {
            var c1 = CountItems(before.GetInventory(StashHelper.PlayerInventoryId));
            var c2 = CountItems(after.GetInventory(StashHelper.PlayerInventoryId));

            foreach (var key in c1.Keys.Union(c2.Keys).OrderBy(k => k))
            {
                c1.TryGetValue(key, out var n1);
                c2.TryGetValue(key, out var n2);
                if (n1 != n2)
                {
                    report.Entries.Add(new DiffEntry("Player Inventory", key, n1 == 0 ? "" : $"x{n1}", n2 == 0 ? "" : $"x{n2}"));
                }
            }
        }

        private static Dictionary<string, int> CountItems(SubInventory inv)
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            if (inv?.Items == null)
            {
                return counts;
            }

            foreach (var item in inv.Items)
            {
                var id = item.ItemInfo.ItemId.Id;
                var display = ResourceHelper.GetDisplayName(id);
                var tech = id.ResolvedText ?? $"0x{(ulong)id:X}";
                var label = string.IsNullOrWhiteSpace(display) ? tech : $"{display} ({tech})";
                counts[label] = counts.GetValueOrDefault(label) + 1;
            }
            return counts;
        }

        private static void CompareVehicles(SaveFileHelper before, SaveFileHelper after, SaveDiffReport report)
        {
            var v1 = GetUnlockedVehicles(before);
            var v2 = GetUnlockedVehicles(after);

            foreach (var id in v2.Except(v1).OrderBy(x => x))
            {
                report.Entries.Add(new DiffEntry("Vehicles", id, "", "Unlocked"));
            }

            foreach (var id in v1.Except(v2).OrderBy(x => x))
            {
                report.Entries.Add(new DiffEntry("Vehicles", id, "Unlocked", ""));
            }
        }

        private static HashSet<string> GetUnlockedVehicles(SaveFileHelper save)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            var ps = save.GetPSDataContainer();
            var garage = ps?.Entries.FirstOrDefault(x => x.Data is vehicleGarageComponentPS)?.Data as vehicleGarageComponentPS;
            var list = garage?.UnlockedVehicleArray;
            if (list == null)
            {
                return set;
            }

            foreach (var v in list)
            {
                var name = v.VehicleID.RecordID.ResolvedText ?? $"0x{(ulong)v.VehicleID.RecordID:X}";
                set.Add(name);
            }
            return set;
        }
    }
}
