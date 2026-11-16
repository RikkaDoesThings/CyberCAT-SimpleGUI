using System;
using System.IO;
using System.Linq;
using CP2077SaveEditor.Utils;
using WolvenKit.Common.Services;
using WolvenKit.Core.Compression;
using WolvenKit.RED4.Save;
using WolvenKit.RED4.Save.IO;
using WolvenKit.RED4.Types;
using Xunit;

namespace CP2077SaveEditor.Tests;

public class BuildAndIconicTests
{
    private const string SaveVariable = "CP2077_TEST_SAVE";

    private static readonly Lazy<HashService> s_hashService = new(() =>
    {
        CompressionSettings.Get().UseOodle = false;
        return new HashService();
    });

    private static SaveFileHelper LoadSave()
    {
        var path = Environment.GetEnvironmentVariable(SaveVariable);
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            Assert.Skip($"Set {SaveVariable} to a sav.dat to run this test");
        }

        _ = s_hashService.Value;

        using var stream = File.OpenRead(path);
        return Load(stream);
    }

    private static SaveFileHelper Load(Stream stream)
    {
        using var reader = new CyberpunkSaveReader(stream);
        Assert.Equal(EFileReadErrorCodes.NoError, reader.ReadFile(out var save));
        return new SaveFileHelper { SaveFile = save };
    }

    private static SaveFileHelper Reload(SaveFileHelper saveFile)
    {
        using var stream = new MemoryStream(saveFile.Serialize());
        return Load(stream);
    }

    [Fact]
    public void ResourceHelper_IconicWeapons_LoadedAndCanonical()
    {
        Assert.True(ResourceHelper.IconicWeaponIds.Count >= 300);
        Assert.True(ResourceHelper.CanonicalIconicWeaponIds.Count >= 100);

        foreach (var id in ResourceHelper.CanonicalIconicWeaponIds)
        {
            Assert.True(ResourceHelper.IsIconicWeapon((TweakDBID)id));
            Assert.True(ResourceHelper.ItemClasses.ContainsKey(id));
        }
    }

    [Fact]
    public void IconicWeapons_AddMissingToStash_RoundTrips()
    {
        var save = LoadSave();
        var missingBefore = IconicWeaponsHelper.GetMissingIconicIds(save);

        var (added, _) = IconicWeaponsHelper.AddMissingIconicsToStash(save, makeMaxTier: true);
        Assert.Equal(missingBefore.Count, added);

        var reloaded = Reload(save);
        var missingAfter = IconicWeaponsHelper.GetMissingIconicIds(reloaded);
        Assert.Empty(missingAfter);
    }

    [Fact]
    public void BuildPreset_ExportAndImport_RoundTrips()
    {
        var save = LoadSave();
        var exported = BuildPresetHelper.Export(save, "manual-777");
        Assert.Equal(5, exported.Attributes.Count);

        var json = BuildPresetHelper.Serialize(exported);
        var deserialized = BuildPresetHelper.Deserialize(json);
        Assert.NotNull(deserialized);
        Assert.Equal("manual-777", deserialized.Name);

        // Mutate an attribute before re-importing to verify import restores it
        var playerData = save.GetPlayerDevelopmentData();
        var firstAttr = playerData.Attributes.First(a => a.AttributeName.ToString() == "Strength");
        firstAttr.Value = firstAttr.Value == 20 ? 3 : 20;

        var result = BuildPresetHelper.Import(save, deserialized, chargePerkPoints: false);
        Assert.Equal(5, result.AttributesUpdated);
        Assert.Equal(deserialized.Perks.Count, result.PerksApplied);
        Assert.Equal(deserialized.Cyberware.Count, result.CyberwareEquipped);

        var reloaded = Reload(save);
        var reExported = BuildPresetHelper.Export(reloaded, "manual-777");

        Assert.Equal(exported.Attributes, reExported.Attributes);
        Assert.Equal(exported.Perks, reExported.Perks);
        Assert.Equal(exported.Cyberware.Count, reExported.Cyberware.Count);
    }

    [Fact]
    public void BulkPacks_QuickhacksModsAndClothingSets_RoundTrip()
    {
        var save = LoadSave();

        Assert.True(ResourceHelper.QuickhacksAndMods.IconicQuickhacks.Count >= 10);
        Assert.True(ResourceHelper.QuickhacksAndMods.Tier5Quickhacks.Count >= 10);
        Assert.True(ResourceHelper.QuickhacksAndMods.IconicWeaponMods.Count >= 4);
        Assert.True(ResourceHelper.ClothingSets.Count >= 5);

        BulkPackHelper.AddMissingQuickhacks(save, iconic: true);
        BulkPackHelper.AddMissingIconicWeaponMods(save);
        BulkPackHelper.AddMissingClothingSet(save, "Johnny Silverhand", makeMaxTier: true);

        var reloaded = Reload(save);
        Assert.Equal(0, BulkPackHelper.AddMissingQuickhacks(reloaded, iconic: true));
        Assert.Equal(0, BulkPackHelper.AddMissingIconicWeaponMods(reloaded));
        Assert.Equal(0, BulkPackHelper.AddMissingClothingSet(reloaded, "Johnny Silverhand", makeMaxTier: true).Added);
    }

    [Fact]
    public void Cyberware_UpgradeAllAndSaveDiff_RoundTrip()
    {
        var original = LoadSave();
        var modified = Reload(original);

        var cyberware = new CyberwareHelper(modified);
        cyberware.UpgradeEquippedToMaxTier();
        modified.SetFactByName("mq007_skippy_aim_at_head", 1);

        var diff = SaveDiffHelper.Compare(original, modified);
        Assert.Contains(diff.Entries, e => e.Category == "Quest Facts" && e.Name == "mq007_skippy_aim_at_head");

        var reloaded = Reload(modified);
        Assert.Equal(0, new CyberwareHelper(reloaded).UpgradeEquippedToMaxTier());
    }

    [Fact]
    public void PlayerBonusStats_AndIconicCyberware_RoundTrip()
    {
        var save = LoadSave();

        Assert.True(BulkPackHelper.IconicCyberwareIds.Count >= 15);
        BulkPackHelper.AddMissingIconicCyberware(save);

        PlayerBonusStatsHelper.SetBonusStat(save, Enums.gamedataStatType.Health, 500f);
        PlayerBonusStatsHelper.SetBonusStat(save, Enums.gamedataStatType.CarryCapacity, 1000f);

        var reloaded = Reload(save);
        Assert.Equal(0, BulkPackHelper.AddMissingIconicCyberware(reloaded));
        Assert.Equal(500f, PlayerBonusStatsHelper.GetBonusStat(reloaded, Enums.gamedataStatType.Health));
        Assert.Equal(1000f, PlayerBonusStatsHelper.GetBonusStat(reloaded, Enums.gamedataStatType.CarryCapacity));
    }
}
