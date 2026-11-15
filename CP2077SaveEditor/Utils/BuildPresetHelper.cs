using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using WolvenKit.RED4.Archive.Buffer;
using WolvenKit.RED4.Save.Classes;
using WolvenKit.RED4.Types;
using static WolvenKit.RED4.Types.Enums;

namespace CP2077SaveEditor.Utils
{
    public class BuildPreset
    {
        public int Version { get; set; } = 1;
        public string Name { get; set; }
        public Dictionary<string, int> Attributes { get; set; } = new();
        public Dictionary<string, int> Perks { get; set; } = new();
        public List<EquippedCyberwareEntry> Cyberware { get; set; } = new();
    }

    public class EquippedCyberwareEntry
    {
        public string Area { get; set; } = string.Empty;
        public int SlotIndex { get; set; }
        public string ItemId { get; set; } = string.Empty;
        public bool MakeMaxTier { get; set; }
    }

    public record BuildImportResult(int AttributesUpdated, int PerksApplied, int CyberwareEquipped, int CyberwareCreated, List<string> Warnings);

    /// <summary>
    /// Exports and imports character builds (Attributes, 2.0/Relic Perks, and equipped Cyberware) as portable JSON presets.
    /// </summary>
    public static class BuildPresetHelper
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        private static readonly gamedataStatType[] CoreAttributes =
        {
            gamedataStatType.Strength,
            gamedataStatType.Reflexes,
            gamedataStatType.TechnicalAbility,
            gamedataStatType.Intelligence,
            gamedataStatType.Cool
        };

        public static BuildPreset Export(SaveFileHelper save, string name = null)
        {
            if (save == null)
            {
                throw new ArgumentNullException(nameof(save));
            }

            var preset = new BuildPreset { Name = name };

            // 1. Attributes
            var playerData = save.GetPlayerDevelopmentData();
            if (playerData?.Attributes != null)
            {
                foreach (var attr in playerData.Attributes)
                {
                    if (CoreAttributes.Contains((gamedataStatType)attr.AttributeName))
                    {
                        preset.Attributes[attr.AttributeName.ToString()] = attr.Value;
                    }
                }
            }

            // 2. Perks (2.0 + Relic)
            if (playerData != null)
            {
                var perkHelper = new PerkHelper(playerData);
                foreach (var perk in ResourceHelper.Perks)
                {
                    var level = perkHelper.GetLevel(perk);
                    if (level > 0)
                    {
                        preset.Perks[perk.Type] = level;
                    }
                }
            }

            // 3. Equipped Cyberware
            var cyberwareHelper = new CyberwareHelper(save);
            foreach (var area in cyberwareHelper.GetCyberwareAreas())
            {
                var areaName = area.AreaType.ToString();
                for (var i = 0; i < area.EquipSlots.Count; i++)
                {
                    var slotItem = area.EquipSlots[i].ItemID;
                    if (slotItem == null || (ulong)slotItem.Id == 0)
                    {
                        continue;
                    }

                    var resolvedId = slotItem.Id.ResolvedText ?? ((ulong)slotItem.Id).ToString();
                    var stats = save.GetStatsFromItemId(slotItem);
                    var isMaxTier = IsMaxTier(stats);

                    preset.Cyberware.Add(new EquippedCyberwareEntry
                    {
                        Area = areaName,
                        SlotIndex = i,
                        ItemId = resolvedId,
                        MakeMaxTier = isMaxTier
                    });
                }
            }

            return preset;
        }

        public static string Serialize(BuildPreset preset) => JsonSerializer.Serialize(preset, JsonOptions);

        public static BuildPreset Deserialize(string json) => JsonSerializer.Deserialize<BuildPreset>(json, JsonOptions);

        public static BuildImportResult Import(SaveFileHelper save, BuildPreset preset, bool chargePerkPoints = false)
        {
            if (save == null)
            {
                throw new ArgumentNullException(nameof(save));
            }

            if (preset == null)
            {
                throw new ArgumentNullException(nameof(preset));
            }

            var warnings = new List<string>();
            var attributesUpdated = ApplyAttributes(save, preset.Attributes);
            var perksApplied = ApplyPerks(save, preset.Perks, chargePerkPoints, warnings);
            var (cyberwareEquipped, cyberwareCreated) = ApplyCyberware(save, preset.Cyberware, warnings);

            return new BuildImportResult(attributesUpdated, perksApplied, cyberwareEquipped, cyberwareCreated, warnings);
        }

        private static int ApplyAttributes(SaveFileHelper save, Dictionary<string, int> attributes)
        {
            if (attributes == null || attributes.Count == 0)
            {
                return 0;
            }

            var playerData = save.GetPlayerDevelopmentData();
            if (playerData?.Attributes == null)
            {
                return 0;
            }

            var modifiersBuffer = save.GetStatsFromEntityId(1)?.ForcedModifiersBuffer?.Data as ModifiersBuffer;
            var updated = 0;

            foreach (var attr in playerData.Attributes)
            {
                var key = attr.AttributeName.ToString();
                if (!attributes.TryGetValue(key, out var targetValue))
                {
                    continue;
                }

                var clamped = Math.Clamp(targetValue, 3, 20);
                attr.Value = clamped;

                if (modifiersBuffer != null)
                {
                    var stat = modifiersBuffer.Entries.FirstOrDefault(x => x.StatType == (gamedataStatType)attr.AttributeName);
                    if (stat is not gameConstantStatModifierData_Deprecated constantStat)
                    {
                        constantStat = new gameConstantStatModifierData_Deprecated
                        {
                            StatType = attr.AttributeName,
                            ModifierType = gameStatModifierType.Additive,
                            Value = 1
                        };
                        modifiersBuffer.Entries.Add(constantStat);
                    }

                    constantStat.Value = clamped;
                }

                updated++;
            }

            return updated;
        }

        private static int ApplyPerks(SaveFileHelper save, Dictionary<string, int> perks, bool chargePerkPoints, List<string> warnings)
        {
            var playerData = save.GetPlayerDevelopmentData();
            if (playerData == null)
            {
                warnings.Add("Player development data not found; skipped perks.");
                return 0;
            }

            var perkHelper = new PerkHelper(playerData);
            var perksByType = ResourceHelper.Perks.ToDictionary(p => p.Type, StringComparer.OrdinalIgnoreCase);

            // Reset current perks before applying the preset
            foreach (var perk in ResourceHelper.Perks)
            {
                if (perkHelper.GetLevel(perk) > 0)
                {
                    perkHelper.SetLevel(perk, 0, chargePerkPoints);
                }
            }

            if (perks == null || perks.Count == 0)
            {
                return 0;
            }

            var applied = 0;
            foreach (var (perkType, level) in perks)
            {
                if (!perksByType.TryGetValue(perkType, out var perk))
                {
                    warnings.Add($"Unknown perk: {perkType}");
                    continue;
                }

                var targetLevel = Math.Clamp(level, 0, perk.MaxLevel);
                if (targetLevel <= 0)
                {
                    continue;
                }

                if (perkHelper.SetLevel(perk, targetLevel, chargePerkPoints))
                {
                    applied++;
                }
                else
                {
                    warnings.Add($"Not enough perk points to apply {perkType} (level {targetLevel}).");
                }
            }

            return applied;
        }

        private static (int Equipped, int Created) ApplyCyberware(SaveFileHelper save, List<EquippedCyberwareEntry> entries, List<string> warnings)
        {
            var cyberwareHelper = new CyberwareHelper(save);
            var areas = cyberwareHelper.GetCyberwareAreas().ToDictionary(a => a.AreaType.ToString(), StringComparer.OrdinalIgnoreCase);

            // Unequip existing cyberware slots first
            foreach (var area in areas.Values)
            {
                foreach (var slot in area.EquipSlots)
                {
                    CyberwareHelper.Unequip(slot);
                }
            }

            if (entries == null || entries.Count == 0)
            {
                return (0, 0);
            }

            var inventory = save.GetInventory(1);
            if (inventory?.Items == null)
            {
                warnings.Add("Player inventory not found; skipped cyberware.");
                return (0, 0);
            }

            var usedItems = new HashSet<ItemData>();
            var equipped = 0;
            var created = 0;

            foreach (var entry in entries)
            {
                if (!areas.TryGetValue(entry.Area, out var area))
                {
                    warnings.Add($"Cyberware area not found on save: {entry.Area}");
                    continue;
                }

                if (entry.SlotIndex < 0 || entry.SlotIndex >= area.EquipSlots.Count)
                {
                    warnings.Add($"Slot {entry.SlotIndex} out of range for {entry.Area}");
                    continue;
                }

                var tweakId = ParseTweakId(entry.ItemId);
                if ((ulong)tweakId == 0)
                {
                    warnings.Add($"Invalid cyberware item ID: {entry.ItemId}");
                    continue;
                }

                var item = inventory.Items.FirstOrDefault(x => !usedItems.Contains(x) && (ulong)x.ItemInfo.ItemId.Id == (ulong)tweakId);
                if (item == null)
                {
                    if (!ResourceHelper.ItemClasses.TryGetValue((ulong)tweakId, out var itemRecord))
                    {
                        warnings.Add($"Unknown cyberware item: {entry.ItemId}");
                        continue;
                    }

                    item = save.CreateItem(tweakId, itemRecord, 1);
                    inventory.Items.Add(item);
                    created++;

                    if (entry.MakeMaxTier)
                    {
                        var stats = save.GetStatsFromItemId(item.ItemInfo.ItemId);
                        if (stats != null)
                        {
                            save.SetMaxTier(stats);
                        }
                    }
                }

                cyberwareHelper.Equip(area.EquipSlots[entry.SlotIndex], item);
                usedItems.Add(item);
                equipped++;
            }

            return (equipped, created);
        }

        private static TweakDBID ParseTweakId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return TweakDBID.Empty;
            }

            return ulong.TryParse(value, out var hash) ? (TweakDBID)hash : (TweakDBID)value;
        }

        private static bool IsMaxTier(gameSavedStatsData statsData)
        {
            if (statsData?.ModifiersBuffer?.Data is not ModifiersBuffer buffer)
            {
                return false;
            }

            var quality = buffer.Entries
                .OfType<gameConstantStatModifierData_Deprecated>()
                .Where(x => x.StatType == gamedataStatType.Quality && x.ModifierType == gameStatModifierType.Additive)
                .Sum(x => (float)x.Value);

            var isPlus = buffer.Entries
                .OfType<gameConstantStatModifierData_Deprecated>()
                .Where(x => x.StatType == gamedataStatType.IsItemPlus && x.ModifierType == gameStatModifierType.Additive)
                .Sum(x => (float)x.Value);

            return quality >= 4 && isPlus >= 2;
        }
    }
}
