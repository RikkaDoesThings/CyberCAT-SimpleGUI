using System;
using System.IO;
using System.Text;
using System.Text.Json;
using CP2077SaveEditor.Utils;
using Xunit;

namespace CP2077SaveEditor.Tests;

public class SaveMetadataAndConfigTests
{
    [Fact]
    public void UpdateMetadataFields_UpdatesLevelStreetCredAndLifePathPreservingOtherFields()
    {
        var json = """
            {
              "RootType": "saveMetadataContainer",
              "Data": {
                "metadata": {
                  "level": 2.0,
                  "streetCred": 1.0,
                  "lifePath": "Nomad",
                  "bodyGender": "Female",
                  "playTime": 1234.5
                }
              }
            }
            """;

        var updatedBytes = SaveMetadataHelper.UpdateMetadataFields(Encoding.UTF8.GetBytes(json), 60, 50, "Corporate");
        using var doc = JsonDocument.Parse(updatedBytes);
        var metadata = doc.RootElement.GetProperty("Data").GetProperty("metadata");

        Assert.Equal(60.0, metadata.GetProperty("level").GetDouble());
        Assert.Equal(50.0, metadata.GetProperty("streetCred").GetDouble());
        Assert.Equal("Corporate", metadata.GetProperty("lifePath").GetString());
        Assert.Equal("Female", metadata.GetProperty("bodyGender").GetString());
        Assert.Equal(1234.5, metadata.GetProperty("playTime").GetDouble());
    }

    [Fact]
    public void UpdateMetadataFields_NullLifePath_LeavesModdedLifePathUntouched()
    {
        var json = """
            {
              "Data": {
                "metadata": {
                  "level": 10.0,
                  "streetCred": 5.0,
                  "lifePath": "FreshStart"
                }
              }
            }
            """;

        var updatedBytes = SaveMetadataHelper.UpdateMetadataFields(Encoding.UTF8.GetBytes(json), 25, 30, null);
        using var doc = JsonDocument.Parse(updatedBytes);
        var metadata = doc.RootElement.GetProperty("Data").GetProperty("metadata");

        Assert.Equal(25.0, metadata.GetProperty("level").GetDouble());
        Assert.Equal(30.0, metadata.GetProperty("streetCred").GetDouble());
        Assert.Equal("FreshStart", metadata.GetProperty("lifePath").GetString());
    }

    [Fact]
    public void AppConfigSave_WritesValidJsonAndUpdatesProperties()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"cybercat_test_config_{Guid.NewGuid():N}.json");
        try
        {
            AppConfig.Save(theme: 1, undoSteps: 0, checkForUpdates: false, path: tempFile);

            Assert.True(File.Exists(tempFile));
            Assert.Equal(1, AppConfig.Theme);
            Assert.Equal(0, AppConfig.UndoSteps);
            Assert.False(AppConfig.CheckForUpdates);

            using var doc = JsonDocument.Parse(File.ReadAllText(tempFile));
            Assert.Equal(1, doc.RootElement.GetProperty("Theme").GetInt32());
            Assert.Equal(0, doc.RootElement.GetProperty("UndoSteps").GetInt32());
            Assert.Equal(0, doc.RootElement.GetProperty("CheckForUpdates").GetInt32());
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
            // Restore defaults in memory
            AppConfig.Save(theme: 2, undoSteps: 10, checkForUpdates: true, path: tempFile + ".restore");
            if (File.Exists(tempFile + ".restore"))
            {
                File.Delete(tempFile + ".restore");
            }
        }
    }
}
