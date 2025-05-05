using System;
using System.IO;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace xivclone.Windows;

public class ConfigWindow : Window, IDisposable
{
    private Configuration Configuration;
    private FileDialogManager FileDialogManager;

    public ConfigWindow(Plugin plugin) : base(
        "xivclone Settings",
        ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar |
        ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoResize)
    {
        this.Size = new Vector2(600, 115);
        this.SizeCondition = ImGuiCond.Always;

        this.Configuration = plugin.Configuration;
        this.FileDialogManager = plugin.FileDialogManager;
    }

    public void Dispose() { }

    public override void Draw()
    {
        var workingDirectory = Configuration.WorkingDirectory;
        ImGui.InputText("Snapshot Directory", ref workingDirectory, 255, ImGuiInputTextFlags.ReadOnly);
        ImGui.SameLine();
        ImGui.PushFont(UiBuilder.IconFont);
        string folderIcon = FontAwesomeIcon.Folder.ToIconString();
        if (ImGui.Button(folderIcon))
        {
            FileDialogManager.OpenFolderDialog("xivclone snapshot directory", (status, path) =>
            {
                if (!status)
                {
                    return;
                }

                if (Directory.Exists(path))
                {
                    this.Configuration.WorkingDirectory = path;
                    this.Configuration.Save();
                }
            });
        }
        ImGui.PopFont();

        var penumbraDirectory = Configuration.PenumbraDirectory;
        ImGui.InputText("Penumbra Directory", ref penumbraDirectory, 255, ImGuiInputTextFlags.ReadOnly);
        ImGui.SameLine();
        ImGui.PushFont(UiBuilder.IconFont);
        string heartIcon = FontAwesomeIcon.Heart.ToIconString();
        if (ImGui.Button(heartIcon))
        {
            FileDialogManager.OpenFolderDialog("penumbra mod directory", (status, path) =>
            {
                if (!status)
                {
                    return;
                }

                if (Directory.Exists(path))
                {
                    this.Configuration.PenumbraDirectory = path;
                    this.Configuration.Save();
                }
            });
        }
        ImGui.PopFont();

        // Store current config value in a local variable
        bool copyGlamourerString = Configuration.CopyGlamourerString;

        // Render checkbox and detect changes
        if (ImGui.Checkbox("Copy Glamourer string to clipboard", ref copyGlamourerString))
        {
            // If changed, update config and save
            Configuration.CopyGlamourerString = copyGlamourerString;
            Configuration.Save();
        }

        // Description text
        if (Configuration.CopyGlamourerString)
        {
            ImGui.TextWrapped("Glamourer string will be copied automatically.");
        }
        else
        {
            ImGui.TextWrapped("Glamourer string copy disabled.");
        }

    }
}
