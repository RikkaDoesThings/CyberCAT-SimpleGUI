using System;
using System.Collections.Generic;
using System.Linq;
using WolvenKit.RED4.Save.Classes;
using WolvenKit.RED4.Types;

namespace CP2077SaveEditor.Utils
{
    /// <summary>
    /// Finds iconic weapons missing from V's inventory and stash, and adds them to the stash.
    /// </summary>
    public static class IconicWeaponsHelper
    {
        public static List<ulong> GetMissingIconicIds(SaveFileHelper save)
        {
            if (save == null)
            {
                throw new ArgumentNullException(nameof(save));
            }

            var ownedIds = new HashSet<ulong>();
            var ownedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var inventoryId in new[] { StashHelper.PlayerInventoryId, StashHelper.StashInventoryId, StashHelper.CarStashInventoryId })
            {
                var inventory = save.GetInventory(inventoryId);
                if (inventory?.Items == null)
                {
                    continue;
                }

                foreach (var item in inventory.Items)
                {
                    var id = (ulong)item.ItemInfo.ItemId.Id;
                    if (!ResourceHelper.IconicWeaponIds.Contains(id))
                    {
                        continue;
                    }

                    ownedIds.Add(id);
                    var displayName = ResourceHelper.GetDisplayName(id);
                    if (!string.IsNullOrWhiteSpace(displayName))
                    {
                        ownedNames.Add(displayName);
                    }
                }
            }

            var missing = new List<ulong>();
            foreach (var id in ResourceHelper.CanonicalIconicWeaponIds)
            {
                if (!ResourceHelper.ItemClasses.ContainsKey(id))
                {
                    continue;
                }

                if (ownedIds.Contains(id))
                {
                    continue;
                }

                var displayName = ResourceHelper.GetDisplayName(id);
                if (!string.IsNullOrWhiteSpace(displayName) && ownedNames.Contains(displayName))
                {
                    continue;
                }

                missing.Add(id);
            }

            return missing;
        }

        public static (int Added, bool StashCreated) AddMissingIconicsToStash(SaveFileHelper save, bool makeMaxTier = true)
        {
            if (save == null)
            {
                throw new ArgumentNullException(nameof(save));
            }

            var missing = GetMissingIconicIds(save);
            if (missing.Count == 0)
            {
                return (0, false);
            }

            var stash = StashHelper.GetOrCreateStash(save, out var stashCreated);
            if (stash == null)
            {
                return (0, false);
            }

            var added = 0;
            foreach (var id in missing)
            {
                if (!ResourceHelper.ItemClasses.TryGetValue(id, out var itemRecord))
                {
                    continue;
                }

                var item = save.CreateItem((TweakDBID)id, itemRecord, 1);
                stash.Items.Add(item);

                if (makeMaxTier)
                {
                    var stats = save.GetStatsFromItemId(item.ItemInfo.ItemId);
                    if (stats != null)
                    {
                        save.SetMaxTier(stats);
                    }
                }

                added++;
            }

            return (added, stashCreated);
        }
    }
}
