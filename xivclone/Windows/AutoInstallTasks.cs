using System.IO;
using System.Threading.Tasks;
using xivclone.Utils;
using xivclone.Managers.AutoInstaller;
using Newtonsoft.Json.Linq;
using Penumbra.String.Classes;
using System;
using Dalamud.Utility;

namespace xivclone.Windows
{
    public partial class MainWindow
    {
        // Step 1 - create the snapshot
        private async Task<bool> AutoCreateSnapshot()
        {
            if (player != null)
            {
                LogAndStore($"[auto step 1] Creating snapshot for {autoMod.Name}...", msg => Logger.Debug(msg));
                var (success, design, customize) = Plugin.SnapshotManager.SaveSnapshot(player, autoMod.Name);
                if (success)
                {
                    // Update autoModName to include player name
                    autoMod.Name = player.Name.ToString() + "_" + autoMod.Name;
                    if (design != null)
                    {
                        autoMod.Design = design;
                    }
                    autoMod.Customize = customize;
                    LogAndStore($"[auto step 1] Snapshot for {autoMod.Name} created successfully.", msg => Logger.Debug(msg));
                    return true;
                }
            }
            Cleanup();
            LogAndStore($"[auto step 1] Failed to create snapshot for {autoMod.Name}.", msg => Logger.Error(msg));
            return false;
        }

        // Step 2 - convert the snapshot
        private async Task<bool> AutoConvertSnapshot()
        {
            autoMod.SnapshotPath = Path.Combine(Plugin.Configuration.WorkingDirectory, autoMod.Name);
            if (Directory.Exists(autoMod.SnapshotPath))
            {
                var (glamourerString, pmpName) = Plugin.PMPExportManager.SnapshotToPMP(autoMod.SnapshotPath);
                if (glamourerString != "")
                {
                    LogAndStore($"[auto step 2] modpack for {autoMod.Name} created successfully: {pmpName}.", msg => Logger.Debug(msg));
                    return true;
                }
                autoMod.PackFilename = pmpName;
            }
            LogAndStore("[auto step 2] Failed to export snapshot as PMP", msg => Logger.Error(msg));
            Cleanup();
            return false;
        }

        // Step 3 - install the mod
        private async Task<bool> AutoInstallMod()
        {
            string fullPath = Path.Combine(Plugin.Configuration.WorkingDirectory, autoMod.PackFilename);
            if (File.Exists(fullPath))
            {
                LogAndStore($"[auto step 3] Installing mod {autoMod.PackFilename}...", msg => Logger.Debug(msg));
                if (Plugin.SnapshotManager.InstallMod(fullPath))
                {
                    LogAndStore($"[auto step 3] Mod {autoMod.Name} installed successfully.", msg => Logger.Debug(msg));
                    return true;
                }
                LogAndStore($"[auto step 3] Setting path to Snapshots/auto/{autoMod.Name}", msg => Logger.Debug(msg));
                if (Plugin.SnapshotManager.SetModPath(autoMod.Name, "Snapshots/auto/" + autoMod.Name))
                {
                    LogAndStore($"[auto step 3] Mod {autoMod.Name} path set successfully.", msg => Logger.Debug(msg));
                    return true;
                }
            }
            LogAndStore("[auto step 3] Failed to install modpack in Penumbra", msg => Logger.Error(msg));
            Cleanup();
            return false;
        }

        // Step 4 - build the design
        private async Task<bool> AutoBuildDesign()
        {
            // Build the mod associations and new settings for the design
            var modEntry = new JObject
            {
                ["Name"] = autoMod.Name,
                ["Directory"] = autoMod.Name,
                ["Enabled"] = true,
                ["Priority"] = 99,
                ["Settings"] = new JObject()
            };

            autoMod.Design["Mods"] = new JArray { modEntry };

            autoMod.Design["ResetAdvancedDyes"] = true;
            autoMod.Design["ResetTemporarySettings"] = true;

            // Add the design to Glamourer
            LogAndStore($"[auto step 4] Adding design {autoMod.Name}...", msg => Logger.Debug(msg));
            autoMod.DesignGuid = Plugin.SnapshotManager.AddDesign(autoMod.Design, autoMod.Name);
            if (autoMod.DesignGuid != Guid.Empty)
            {
                LogAndStore($"[auto step 4] Design {autoMod.Name} added successfully.", msg => Logger.Debug(msg));
                return true;
            }
            LogAndStore("[auto step 4] Failed to add design to Glamourer", msg => Logger.Error(msg));
            Cleanup();
            return false;
        }

        // Step 5 - import the customize
        private async Task<bool> AutoImportCustomize()
        {
            if (Plugin.IpcManager.IsCustomizePlusAvailable().IsAniVersion)
            {
                if (!autoMod.Customize.IsNullOrEmpty())
                {
                    LogAndStore($"[auto step 5] Importing customize for {autoMod.Name}...", msg => Logger.Debug(msg));
                    if (Plugin.SnapshotManager.AddCustomizeTemplate(autoMod.Customize, $"Snapshots/auto/{autoMod.Name}"))
                    {
                        LogAndStore($"[auto step 5] Customize for {autoMod.Name} imported successfully.", msg => Logger.Debug(msg));
                        return true;
                    }
                }
                LogAndStore("[auto step 5] Failed to add Customize+ template", msg => Logger.Error(msg));
                Cleanup();
                return false;
            }
            LogAndStore("[auto step 5] skipping customize+ import as Customize+ (ani version) is missing", msg => Logger.Warn(msg));
            return false;
        }

        // Cleanup
        private void Cleanup()
        {
            // Log the contents of AutoMod for debugging
            Logger.Debug("[auto cleanup] AutoMod contents:");
            Logger.Debug($"[auto cleanup]   Name: {autoMod.Name}");
            Logger.Debug($"[auto cleanup]   PackFilename: {autoMod.PackFilename}");
            Logger.Debug($"[auto cleanup]   SnapshotPath: {autoMod.SnapshotPath}");
            Logger.Verbose($"[auto cleanup]   Design: {autoMod.Design.ToString()}");

            // Perform any necessary cleanup here
            autoMod = new AutoMod();
            installComplete = false;
            installSuccess = false;
            installStatusMessage = string.Empty;
        }

        // Logger
        private void LogAndStore(string message, Action<string> logMethod)
        {
            logMethod(message);
            installStatusMessage = message;
        }

    }
}
