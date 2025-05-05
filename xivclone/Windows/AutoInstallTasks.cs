using System.Threading.Tasks;
using xivclone.Utils;
using xivclone.Managers.AutoInstaller;
using Newtonsoft.Json.Linq;
using Dalamud.Utility;
using System;
using System.IO;
using System.IO.Compression;

namespace xivclone.Windows
{
    public partial class MainWindow
    {
        private async Task<bool> AutoCreateSnapshot()
        {
            if (autoInstallExistingPath != string.Empty)
            {
                if (player != null)
                {
                    LogAndStore($"[auto step 1] Creating snapshot for {autoMod.Name}...", msg => Logger.Debug(msg));

                    try
                    {
                        var success = await Plugin.DalamudUtil.RunOnFrameworkThread(() =>
                            Plugin.SnapshotManager.SaveSnapshot(player, autoMod.Name));

                        if (success)
                        {
                            autoMod.Name = player.Name.ToString() + "_" + autoMod.Name;
                            autoMod.SnapshotPath = Path.Combine(Plugin.Configuration.WorkingDirectory, autoMod.Name);
                            LogAndStore($"[auto step 1] Snapshot for {autoMod.Name} created successfully.", msg => Logger.Debug(msg));
                            return true;
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.Error($"[auto step 1] Exception during snapshot creation: {e.Message}");
                    }

                    Cleanup();
                    LogAndStore($"[auto step 1] Failed to create snapshot for {autoMod.Name}.", msg => Logger.Error(msg));
                    return false;
                }

                Cleanup();
                LogAndStore($"[auto step 1] Failed to create snapshot for {autoMod.Name}.", msg => Logger.Error(msg));
                return false;
            }
            else
            {
                LogAndStore("[auto step 1] Skipped snapshot creation due to existing snapshot path provided.", msg => Logger.Debug(msg));
                autoMod.Name = Path.GetFileName(autoInstallExistingPath);
                autoMod.SnapshotPath = autoInstallExistingPath;
                return true;
            }
        }

        private async Task<bool> AutoConvertSnapshot()
        {
            if (Directory.Exists(autoMod.SnapshotPath))
            {
                try
                {
                    var (glamourerString, pmpName, design, customize) = await Task.Run(() =>
                        Plugin.PMPExportManager.SnapshotToPMP(autoMod.SnapshotPath));

                    if (design != null)
                        autoMod.Design = design;

                    autoMod.Customize = customize;
                    autoMod.PackFilename = pmpName;

                    if (!string.IsNullOrEmpty(glamourerString))
                    {
                        LogAndStore($"[auto step 2] modpack for {autoMod.Name} created successfully: {pmpName}.", msg => Logger.Debug(msg));
                        return true;
                    }
                }
                catch (Exception e)
                {
                    Logger.Error($"[auto step 2] Exception during snapshot conversion: {e.Message}");
                }
            }

            LogAndStore("[auto step 2] Failed to export snapshot as PMP", msg => Logger.Error(msg));
            Cleanup();
            return false;
        }

        private async Task<bool> AutoInstallMod()
        {
            string fullPath = Path.Combine(Plugin.Configuration.WorkingDirectory, autoMod.PackFilename);
            if (File.Exists(fullPath))
            {
                LogAndStore($"[auto step 3] Decompressing mod {autoMod.PackFilename}...", msg => Logger.Debug(msg));

                try
                {
                    ZipFile.ExtractToDirectory(fullPath, Path.Combine(Plugin.Configuration.PenumbraDirectory, autoMod.Name));
                }
                catch (Exception e)
                {
                    Logger.Error($"[auto step 3] Failed to decompress modpack: {e.Message}");
                    Cleanup();
                    return false;
                }

                LogAndStore($"[auto step 3] Installing mod {autoMod.PackFilename}...", msg => Logger.Debug(msg));
                try
                {
                    bool installed = await Plugin.DalamudUtil.RunOnFrameworkThread(() =>
                        Plugin.SnapshotManager.AddMod(autoMod.Name));

                    if (installed)
                    {
                        LogAndStore($"[auto step 3] Mod {autoMod.Name} installed successfully.", msg => Logger.Debug(msg));
                        LogAndStore($"[auto step 3] Setting path to /Snapshots/auto/{autoMod.Name}", msg => Logger.Debug(msg));

                        bool pathSet = await Plugin.DalamudUtil.RunOnFrameworkThread(() =>
                            Plugin.SnapshotManager.SetModPath(autoMod.Name, $"/Snapshots/auto/{autoMod.Name}"));

                        if (pathSet)
                        {
                            LogAndStore($"[auto step 3] Mod {autoMod.Name} path set successfully.", msg => Logger.Debug(msg));
                            return true;
                        }
                    }
                }
                catch (Exception e)
                {
                    Logger.Error($"[auto step 3] Exception during mod installation: {e.Message}");
                }
            }

            LogAndStore("[auto step 3] Failed to install modpack in Penumbra", msg => Logger.Error(msg));
            Cleanup();
            return false;
        }

        private async Task<bool> AutoBuildDesign()
        {
            try
            {
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

                LogAndStore($"[auto step 4] Adding design {autoMod.Name}...", msg => Logger.Debug(msg));
                autoMod.DesignGuid = await Plugin.DalamudUtil.RunOnFrameworkThread(() =>
                    Plugin.SnapshotManager.AddDesign(autoMod.Design, autoMod.Name));

                if (autoMod.DesignGuid != Guid.Empty)
                {
                    LogAndStore($"[auto step 4] Design {autoMod.Name} added successfully.", msg => Logger.Debug(msg));
                    return true;
                }
            }
            catch (Exception e)
            {
                Logger.Error($"[auto step 4] Exception while building design: {e.Message}");
            }

            LogAndStore("[auto step 4] Failed to add design to Glamourer", msg => Logger.Error(msg));
            Cleanup();
            return false;
        }

        private async Task<bool> AutoImportCustomize()
        {
            if (Plugin.IpcManager.IsCustomizePlusAvailable().IsAniVersion)
            {
                if (!autoMod.Customize.IsNullOrEmpty())
                {
                    LogAndStore($"[auto step 5] Importing customize for {autoMod.Name}...", msg => Logger.Debug(msg));
                    try
                    {
                        bool imported = await Plugin.DalamudUtil.RunOnFrameworkThread(() =>
                            Plugin.SnapshotManager.AddCustomizeTemplate(autoMod.Customize, $"Snapshots/auto/{autoMod.Name}"));

                        if (imported)
                        {
                            LogAndStore($"[auto step 5] Customize for {autoMod.Name} imported successfully.", msg => Logger.Debug(msg));
                            return true;
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.Error($"[auto step 5] Customize+ IPC error: {e.Message}");
                    }

                    LogAndStore("[auto step 5] Failed to add Customize+ template", msg => Logger.Error(msg));
                    Cleanup();
                    return false;
                }
            }

            LogAndStore("[auto step 5] skipping customize+ import as Customize+ (ani version) is missing", msg => Logger.Warn(msg));
            return false;
        }

        private void Cleanup()
        {
            Logger.Debug("[auto cleanup] AutoMod contents:");
            Logger.Debug($"[auto cleanup]   Name: {autoMod.Name}");
            Logger.Debug($"[auto cleanup]   PackFilename: {autoMod.PackFilename}");
            Logger.Debug($"[auto cleanup]   SnapshotPath: {autoMod.SnapshotPath}");

            autoMod = new AutoMod();
            installComplete = false;
            installSuccess = false;
            installStatusMessage = string.Empty;
            autoInstallExistingPath = string.Empty;
        }

        private void LogAndStore(string message, Action<string> logMethod)
        {
            logMethod(message);
            installStatusMessage = message;
            Task.Delay(1000);
        }
    }
}
