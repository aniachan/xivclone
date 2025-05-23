using xivclone.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using xivclone.Utils;
using System.Text.Json;
using System.IO.Compression;
using Newtonsoft.Json.Linq;
using Dalamud.Utility;
using xivclone.Interop;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace xivclone.PMP
{
    public partial class PMPExportManager
    {
        private Plugin plugin;
        public PMPExportManager(Plugin plugin)
        {
            this.plugin = plugin;
        }

        public (string glamourerString, string pmpName, JObject? design, string customize) SnapshotToPMP(string snapshotPath)
        {
            Logger.Debug($"Operating on {snapshotPath}");
            //read snapshot
            string infoJson = File.ReadAllText(Path.Combine(snapshotPath, "snapshot.json"));
            if (infoJson == null)
            {
                Logger.Warn("No snapshot json found, aborting");
                return (String.Empty, String.Empty, null, String.Empty);
            }
            SnapshotInfo? snapshotInfo = JsonSerializer.Deserialize<SnapshotInfo>(infoJson);
            if (snapshotInfo == null)
            {
                Logger.Warn("Failed to deserialize snapshot json, aborting");
                return (String.Empty, String.Empty, null, String.Empty);
            }

            //Deserialize JObject of design
            JObject? design = null;
            try
            {
                if (snapshotInfo.GlamourerJSON.IsNullOrEmpty())
                {
                    // Attempt to convert the base64 string to JSON
                    try
                    {
                        DesignParser parser = new DesignParser();
                        design = parser.FromBase64(snapshotInfo.GlamourerString);
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn($"Failed to parse glamourer base64 as json: {ex.Message}");
                    }
                } else
                {
                    // Use the available JSON
                    design = JObject.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(snapshotInfo.GlamourerJSON)));
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"Failed to parse glamourer json: {ex.Message}");
            }

            //begin building PMP
            string snapshotName = new DirectoryInfo(snapshotPath).Name;
            string pmpFileName = $"{snapshotName}_{Guid.NewGuid()}";

            string workingDirectory = Path.Combine(plugin.Configuration.WorkingDirectory, $"temp_{pmpFileName}");
            if (!Directory.Exists(workingDirectory))
            {
                Directory.CreateDirectory(workingDirectory);
            }

            //meta.json
            PMPMetadata metadata = new PMPMetadata();
            metadata.Name = snapshotName;
            metadata.Author = $"not {snapshotName} anymore :3";
            using (FileStream stream = new FileStream(Path.Combine(workingDirectory, "meta.json"), FileMode.Create))
            {
                JsonSerializer.Serialize(stream, metadata);
            }

            //default_mod.json
            PMPDefaultMod defaultMod = BuildCorrectedDefaultMod(snapshotInfo.FileReplacements);

            List<PMPManipulationEntry>? manipulations;
            FromCompressedBase64<List<PMPManipulationEntry>>(snapshotInfo.ManipulationString, out manipulations);

            if (!snapshotInfo.ManipulationString.IsNullOrEmpty())
            {
                File.WriteAllText(Path.Combine(workingDirectory, "meta.txt"), snapshotInfo.ManipulationString);
            }

            if (manipulations != null)
            {
                defaultMod.Manipulations = manipulations;
            }
            using (FileStream stream = new FileStream(Path.Combine(workingDirectory, "default_mod.json"), FileMode.Create))
            {
                JsonSerializer.Serialize(stream, defaultMod, new JsonSerializerOptions { WriteIndented = true});
            }

            //mods
            foreach(var file in snapshotInfo.FileReplacements)
            {
                string modPath = Path.Combine(snapshotPath, file.Key);
                string destPath = Path.Combine(workingDirectory, file.Key);
                Logger.Debug($"Copying {modPath}");
                Directory.CreateDirectory(Path.GetDirectoryName(destPath) ?? "");
                File.Copy(modPath, destPath);
            }

            //zip and make pmp file
            ZipFile.CreateFromDirectory(workingDirectory, Path.Combine(plugin.Configuration.WorkingDirectory, $"{pmpFileName}.pmp"));

            //cleanup
            Directory.Delete(workingDirectory, true);

            //return glamourer string
            return (snapshotInfo.GlamourerString, $"{pmpFileName}.pmp", design, snapshotInfo.CustomizeData);
        }

        // Decompress a base64 encoded string to the given type and a prepended version byte if possible.
        // On failure, data will be default and version will be byte.MaxValue.
        internal static byte FromCompressedBase64<T>(string base64, out T? data)
        {
            var version = byte.MaxValue;
            try
            {
                var bytes = Convert.FromBase64String(base64);
                using var compressedStream = new MemoryStream(bytes);
                using var zipStream = new GZipStream(compressedStream, CompressionMode.Decompress);
                using var resultStream = new MemoryStream();
                zipStream.CopyTo(resultStream);
                bytes = resultStream.ToArray();
                version = bytes[0];
                var json = Encoding.UTF8.GetString(bytes, 1, bytes.Length - 1);
                data = JsonSerializer.Deserialize<T>(json);
            }
            catch
            {
                data = default;
            }

            return version;
        }
    }
}
