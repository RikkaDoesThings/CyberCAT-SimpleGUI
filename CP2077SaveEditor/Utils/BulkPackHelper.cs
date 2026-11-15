using System;
using System.Collections.Generic;
using System.Linq;
using WolvenKit.RED4.Save.Classes;
using WolvenKit.RED4.Types;

namespace CP2077SaveEditor.Utils
{
    /// <summary>
    /// Bulk-adds missing Iconic/Tier 5 Quickhacks, Iconic Weapon Mods, and Wardrobe Clothing Sets.
    /// </summary>
    public static class BulkPackHelper
    {
        public static int AddMissingQuickhacks(SaveFileHelper save, bool iconic = true)
        {
            var ids = iconic ? ResourceHelper.QuickhacksAndMods.IconicQuickhacks : ResourceHelper.QuickhacksAndMods.Tier5Quickhacks;
            return AddMissingToInventory(save, save.GetInventory(StashHelper.PlayerInventoryId), ids, checkStashToo: false, makeMaxTier: false);
        }

        public static int AddMissingIconicWeaponMods(SaveFileHelper save)
        {
            return AddMissingToInventory(save, save.GetInventory(StashHelper.PlayerInventoryId), ResourceHelper.QuickhacksAndMods.IconicWeaponMods, checkStashToo: false, makeMaxTier: false);
        }

        public static (int Added, bool StashCreated) AddMissingClothingSet(SaveFileHelper save, string setName, bool makeMaxTier = true)
        {
            if (save == null)
            {
                throw new ArgumentNullException(nameof(save));
            }

            IEnumerable<ulong> ids = string.IsNullOrEmpty(setName)
                ? ResourceHelper.ClothingSets.Values.SelectMany(x => x).Distinct()
                : ResourceHelper.ClothingSets.TryGetValue(setName, out var list) ? list : Enumerable.Empty<ulong>();

            var stash = StashHelper.GetOrCreateStash(save, out var stashCreated);
            if (stash == null)
            {
                return (0, false);
            }

            var added = AddMissingToInventory(save, stash, ids, checkStashToo: true, makeMaxTier: makeMaxTier);
            return (added, stashCreated);
        }

        private static int AddMissingToInventory(SaveFileHelper save, SubInventory target, IEnumerable<ulong> ids, bool checkStashToo, bool makeMaxTier)
        {
            if (save == null)
            {
                throw new ArgumentNullException(nameof(save));
            }

            if (target?.Items == null)
            {
                return 0;
            }

            var ownedIds = new HashSet<ulong>();
            var ownedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var inventoriesToCheck = checkStashToo
                ? new[] { StashHelper.PlayerInventoryId, StashHelper.StashInventoryId, StashHelper.CarStashInventoryId }
                : new[] { target.InventoryId };

            foreach (var invId in inventoriesToCheck)
            {
                var inv = save.GetInventory(invId);
                if (inv?.Items == null)
                {
                    continue;
                }

                foreach (var item in inv.Items)
                {
                    var itemId = (ulong)item.ItemInfo.ItemId.Id;
                    ownedIds.Add(itemId);
                    var name = ResourceHelper.GetDisplayName(itemId);
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        ownedNames.Add(name);
                    }
                }
            }

            var added = 0;
            foreach (var id in ids)
            {
                if (!ResourceHelper.ItemClasses.TryGetValue(id, out var itemRecord))
                {
                    continue;
                }

                if (ownedIds.Contains(id))
                {
                    continue;
                }

                var name = ResourceHelper.GetDisplayName(id);
                if (!string.IsNullOrWhiteSpace(name) && ownedNames.Contains(name))
                {
                    continue;
                }

                var created = save.CreateItem((TweakDBID)id, itemRecord, 1);
                target.Items.Add(created);
                ownedIds.Add(id);
                if (!string.IsNullOrWhiteSpace(name))
                {
                    ownedNames.Add(name);
                }

                if (makeMaxTier)
                {
                    var stats = save.GetStatsFromItemId(created.ItemInfo.ItemId);
                    if (stats != null)
                    {
                        save.SetMaxTier(stats);
                    }
                }

                added++;
            }

            return added;
        }
    }
}
