using Dalamud.Interface;
using Dalamud.Interface.Colors;
using ImGuiNET;
using System;
using System.IO;
using System.Numerics;
using System.Xml.Linq;
using xivclone.Managers.AutoInstaller;

namespace xivclone.Windows
{
    public partial class MainWindow
    {
        private const uint RedHeaderColor = 0xFF1818C0;
        private const uint GreenHeaderColor = 0xFF18C018;
        public bool IsInGpose { get; private set; } = false;

        private void DrawPlayerHeader()
        {
            var color =player == null ? RedHeaderColor : GreenHeaderColor;
            var buttonColor = ImGui.GetColorU32(ImGuiCol.FrameBg);
            ImGui.Button($"{currentLabel}##playerHeader", -Vector2.UnitX * 0.0001f);
        }

        private string saveDialogName = "";
        private string appendDialogName = "";
        private string autoModName = "";
        private bool showSaveDialog = false;
        private bool showAppendDialog = false;
        private bool showPreInstallDialog = false;

        AutoMod autoMod = new AutoMod();
        
        private void DrawPlayerPanel()
        {
            bool cust = Plugin.IpcManager.IsCustomizePlusAvailable().IsAniVersion;
            bool glam = Plugin.IpcManager.IsGlamourerAvailable();
            if (cust && glam)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.ParsedGreen);
                ImGui.Text("all prerequisites satisfied - happy cloning <3");
                ImGui.PopStyleColor();
            } else if (!glam)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DalamudRed);
                ImGui.Text("ERROR:");
                ImGui.Text("You do not have the latest Glamourer version installed. Update or risk crashes.");
                ImGui.PopStyleColor();
            }
            else
            {
                ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.ParsedOrange);
                ImGui.Text("WARNING:");
                ImGui.PopStyleColor();
                ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DalamudYellow);
                ImGui.Text("You do not have Ani's Customize+ plugin installed. Scales will not be captured.");
                ImGui.PopStyleColor();
            }

            ImGui.Text("Capture Glamourer String for Selected Player");
            ImGui.SameLine();
            ImGui.PushFont(UiBuilder.IconFont);
            try
            {
                string glamourerIcon = FontAwesomeIcon.PaintBrush.ToIconString();
                if (ImGui.Button(glamourerIcon))
                {
                    if (player != null)
                        Plugin.SnapshotManager.CopyGlamourerStringToClipboard(player);
                }
            }
            finally
            {
                ImGui.PopFont();
            }

            ImGui.Text("Save snapshot of player ");
            ImGui.SameLine();
            ImGui.PushFont(UiBuilder.IconFont);

            try
            {
                string saveIcon = FontAwesomeIcon.Save.ToIconString();

                if (ImGui.Button(saveIcon))
                {
                    // Set flag to show save dialog
                    showSaveDialog = true;
                }
            }
            finally
            {
                ImGui.PopFont();
            }

            if (showSaveDialog)
            {
                if (OpenNameField("Save Snapshot", ref saveDialogName))
                {
                    // Save snapshot with optional name
                    if (player != null)
                        Plugin.SnapshotManager.SaveSnapshot(player, saveDialogName);

                    // Reset flag and name
                    showSaveDialog = false;
                    saveDialogName = "";
                }
            }

            if (Plugin.IpcManager.IsCustomizePlusAvailable().IsAniVersion)
            {
                ImGui.Text("Copy c+ to clipboard uwu");
                ImGui.SameLine();
                ImGui.PushFont(UiBuilder.IconFont);

                try
                {
                    string copyIcon = FontAwesomeIcon.Clipboard.ToIconString();
                    if (ImGui.Button(copyIcon))
                    {
                        var customizeString = Plugin.IpcManager.GetCustomizePlusScale(player!);
                        ImGui.SetClipboardText(customizeString);
                    }
                }
                finally
                {
                    ImGui.PopFont();
                }
            }

            if (IsInGpose)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DalamudYellow);
                ImGui.Text("Saving snapshots while GPose is active may result in broken/incorrect snapshots. For best results, leave GPose first.");
                ImGui.PopStyleColor();
                ImGui.Spacing();
            }

            ImGui.Text("Append to existing snapshot");
            ImGui.SameLine();
            ImGui.PushFont(UiBuilder.IconFont);

            try
            {
                string addIcon = FontAwesomeIcon.Plus.ToIconString();
                if (ImGui.Button(addIcon))
                {
                    // Set flag to show append dialog
                    showAppendDialog = true;
                }
            }
            finally
            {
                ImGui.PopFont();
            }

            // Direct Install
            if (Plugin.Configuration.PenumbraDirectory != String.Empty)
            {
                ImGui.Text("Direct Install");
                ImGui.SameLine();
                ImGui.PushFont(UiBuilder.IconFont);

                try
                {
                    string arrowsIcon = FontAwesomeIcon.ArrowsTurnRight.ToIconString();
                    if (ImGui.Button(arrowsIcon))
                    {
                        showPreInstallDialog = true;
                    }
                }
                finally
                {
                    ImGui.PopFont();
                }
            } else
            {
                ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DalamudRed);
                ImGui.Text("ERROR:");
                ImGui.Text("You do not have your Penumbra directory set. Direct install is not available.");
                ImGui.PopStyleColor();
            }

            if (showPreInstallDialog)
            {
                if (OpenNameField("Auto Name", ref autoModName))
                {
                    // Save snapshot with optional name
                    if (player != null)
                    {
                        
                        autoMod.Name = autoModName;
                        StartInstallationProcess();
                        showPreInstallDialog = false;
                        autoModName = "";
                    }
                }
            }
            if (showInstallDialog)
            {
                DrawInstallDialog();
            }

            // Append

            if (showAppendDialog)
            {
                if (OpenNameField("Append Snapshot", ref appendDialogName))
                {
                    // Append snapshot with optional name
                    if (player != null)
                        Plugin.SnapshotManager.AppendSnapshot(player, appendDialogName);

                    // Reset flag and name
                    showAppendDialog = false;
                    appendDialogName = "";
                }
            }

            if (this.modifiable)
            {
                ImGui.Spacing();
                ImGui.Text("Load snapshot onto ");
                ImGui.SameLine();
                ImGui.PushFont(UiBuilder.IconFont);

                try
                {
                    string loadIcon = FontAwesomeIcon.FileImport.ToIconString();
                    if (ImGui.Button(loadIcon))
                    {
                        Plugin.FileDialogManager.OpenFolderDialog("Snapshot selection", (status, path) =>
                        {
                            if (!status)
                            {
                                return;
                            }

                            if (Directory.Exists(path))
                            {
                                if (player != null && objIdxSelected.HasValue)
                                    Plugin.SnapshotManager.LoadSnapshot(player, objIdxSelected.Value, path);
                            }
                        }, Plugin.Configuration.WorkingDirectory);
                    }
                }
                finally
                {
                    ImGui.PopFont();
                }
            }
            else
            {
                ImGui.Text("Loading snapshots can only be done on GPose actors");
            }
        }

        private void DrawMonsterPanel()
        {

        }

        private void DrawActorPanel()
        {
            using var raii = ImGuiRaii.NewGroup();
            DrawPlayerHeader();
            if (!ImGui.BeginChild("##playerData", -Vector2.One, true))
            {
                ImGui.EndChild();
                return;
            }

            if (player != null || player.ModelType() == 0)
                DrawPlayerPanel();
            else
                DrawMonsterPanel();

            ImGui.EndChild();
        }

    }
}
