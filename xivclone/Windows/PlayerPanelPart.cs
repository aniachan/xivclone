using Dalamud.Interface;
using Dalamud.Interface.Colors;
using ImGuiNET;
using System;
using System.IO;
using System.Numerics;

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

        private string saveDialogName = ""; // Variable to store the name for save operation
        private string appendDialogName = ""; // Variable to store the name for append operation
        private bool showSaveDialog = false; // Flag to indicate whether to show the save dialog
        private bool showAppendDialog = false; // Flag to indicate whether to show the append dialog

        private void DrawPlayerPanel()
        {
            ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.ParsedOrange);
            ImGui.Text("WARNING:");
            ImGui.PopStyleColor();
            ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DalamudYellow);
            ImGui.Text("Glamourer API currently does not allow you to get their Glamourer design automatically like before when synced with mare.");
            ImGui.Text("As a temporary workaround, copy their Glamourer design to clipboard and edit the file it creates.");
            ImGui.PopStyleColor();
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

            if (!IsInGpose)
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
                ImGui.Text("Load snapshot onto ");
                ImGui.SameLine();
                ImGui.PushFont(UiBuilder.IconFont);

                try
                {
                    string loadIcon = FontAwesomeIcon.Clipboard.ToIconString();
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

        private bool OpenNameField(string windowTitle, ref string name)
        {
            ImGui.OpenPopup(windowTitle);

            bool open = true; // Flag to keep the popup open

            ImGui.SetNextWindowSize(new System.Numerics.Vector2(200, 110));
            if (ImGui.BeginPopupModal(windowTitle, ref open, ImGuiWindowFlags.NoResize))
            {
                ImGui.Text("Enter snapshot name (optional):");

                // Generate default name if name is empty
                if (string.IsNullOrEmpty(name))
                {
                    name = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                }

                ImGui.InputText("", ref name, 100);

                if (ImGui.Button("Save"))
                {
                    ImGui.CloseCurrentPopup();
                    return true; // Return true when Save button is clicked
                }
                ImGui.SameLine();
                if (ImGui.Button("Cancel"))
                {
                    ImGui.CloseCurrentPopup();
                }

                ImGui.EndPopup();
            }

            return false; // Return false if the dialog is canceled
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
