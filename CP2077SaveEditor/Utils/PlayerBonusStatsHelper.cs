using System;
using System.Collections.Generic;
using System.Linq;
using WolvenKit.RED4.Archive.Buffer;
using WolvenKit.RED4.Save.Classes;
using WolvenKit.RED4.Types;
using static WolvenKit.RED4.Types.Enums;

namespace CP2077SaveEditor.Utils
{
    public record BonusStatDefinition(gamedataStatType StatType, string Label, string Unit, decimal Min, decimal Max, decimal Step);

    /// <summary>
    /// Reads and writes clean additive bonus stat modifiers on V's entity (Health, Armor, Stamina, RAM, Carry Capacity, Crit, Speed).
    /// </summary>
    public static class PlayerBonusStatsHelper
    {
        public static readonly IReadOnlyList<BonusStatDefinition> SupportedStats = new[]
        {
            new BonusStatDefinition(gamedataStatType.Health, "Bonus Max Health", "HP", 0, 100000, 50),
            new BonusStatDefinition(gamedataStatType.Armor, "Bonus Armor", "Armor", 0, 100000, 50),
            new BonusStatDefinition(gamedataStatType.Stamina, "Bonus Max Stamina", "Stamina", 0, 100000, 50),
            new BonusStatDefinition(gamedataStatType.Memory, "Bonus Cyberdeck RAM", "RAM", 0, 500, 1),
            new BonusStatDefinition(gamedataStatType.CarryCapacity, "Bonus Carry Capacity", "kg", 0, 100000, 50),
            new BonusStatDefinition(gamedataStatType.CritChance, "Bonus Crit Chance", "%", 0, 100, 5),
            new BonusStatDefinition(gamedataStatType.CritDamage, "Bonus Crit Damage", "%", 0, 1000, 10),
            new BonusStatDefinition(gamedataStatType.MaxSpeed, "Bonus Movement Speed", "m/s", 0, 50, 1)
        };

        public static float GetBonusStat(SaveFileHelper save, gamedataStatType statType)
        {
            if (save == null)
            {
                throw new ArgumentNullException(nameof(save));
            }

            var modifiers = save.GetStatsFromEntityId(1)?.ModifiersBuffer?.Data as ModifiersBuffer;
            if (modifiers == null)
            {
                return 0f;
            }

            return modifiers.Entries
                .OfType<gameConstantStatModifierData_Deprecated>()
                .Where(x => x.StatType == statType && x.ModifierType == gameStatModifierType.Additive)
                .Sum(x => (float)x.Value);
        }

        public static void SetBonusStat(SaveFileHelper save, gamedataStatType statType, float value)
        {
            if (save == null)
            {
                throw new ArgumentNullException(nameof(save));
            }

            var playerStats = save.GetStatsFromEntityId(1) ?? throw new InvalidOperationException("The save has no stats for V.");
            playerStats.ModifiersBuffer ??= new DataBuffer();
            playerStats.ModifiersBuffer.Data ??= new ModifiersBuffer();
            var modifiers = (ModifiersBuffer)playerStats.ModifiersBuffer.Data;

            var existing = modifiers.Entries
                .OfType<gameConstantStatModifierData_Deprecated>()
                .Where(x => x.StatType == statType && x.ModifierType == gameStatModifierType.Additive)
                .ToList();

            foreach (var mod in existing)
            {
                modifiers.Entries.Remove(mod);
            }

            if (Math.Abs(value) > 0.001f)
            {
                modifiers.Entries.Add(new gameConstantStatModifierData_Deprecated
                {
                    StatType = statType,
                    ModifierType = gameStatModifierType.Additive,
                    Value = value
                });
            }
        }
    }
}
