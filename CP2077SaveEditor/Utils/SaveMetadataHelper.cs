using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using static WolvenKit.RED4.Types.Enums;

namespace CP2077SaveEditor.Utils
{
    public static class SaveMetadataHelper
    {
        private static readonly JsonSerializerOptions s_jsonOptions = new() { WriteIndented = true };

        /// <summary>
        /// Keeps metadata.9.json in sync with edited level, street cred, and vanilla lifepath
        /// so the in-game Load Save screen and Save Browser show the updated values immediately.
        /// </summary>
        public static byte[] UpdateMetadata(byte[] metadataBytes, SaveFileHelper saveFile)
        {
            if (metadataBytes == null || metadataBytes.Length == 0 || saveFile == null)
            {
                return metadataBytes;
            }

            try
            {
                var devData = saveFile.GetPlayerDevelopmentData();
                if (devData == null)
                {
                    return metadataBytes;
                }

                int? level = devData.Proficiencies.FirstOrDefault(p => p.Type == gamedataProficiencyType.Level)?.CurrentLevel;
                int? streetCred = devData.Proficiencies.FirstOrDefault(p => p.Type == gamedataProficiencyType.StreetCred)?.CurrentLevel;
                var lifePath = (gamedataLifePath)devData.LifePath switch
                {
                    gamedataLifePath.Corporate => "Corporate",
                    gamedataLifePath.Nomad => "Nomad",
                    gamedataLifePath.StreetKid => "StreetKid",
                    _ => null
                };

                return UpdateMetadataFields(metadataBytes, level, streetCred, lifePath);
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to sync metadata.9.json with save data", ex);
                return metadataBytes;
            }
        }

        public static byte[] UpdateMetadataFields(byte[] metadataBytes, int? level, int? streetCred, string lifePath)
        {
            if (metadataBytes == null || metadataBytes.Length == 0)
            {
                return metadataBytes;
            }

            var root = JsonNode.Parse(metadataBytes) as JsonObject;
            var metadata = root?["Data"]?["metadata"] as JsonObject;
            if (metadata == null)
            {
                return metadataBytes;
            }

            if (level.HasValue)
            {
                metadata["level"] = (double)level.Value;
            }

            if (streetCred.HasValue)
            {
                metadata["streetCred"] = (double)streetCred.Value;
            }

            if (!string.IsNullOrEmpty(lifePath))
            {
                metadata["lifePath"] = lifePath;
            }

            return JsonSerializer.SerializeToUtf8Bytes(root, s_jsonOptions);
        }
    }
}
