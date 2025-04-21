using System;
using System.IO;
using System.Linq;
using System.Numerics;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using xivclone.Utils;

namespace xivclone.Windows;

public partial class MainWindow : Window, IDisposable
{
    private const float SelectorWidth = 200;

    private Plugin Plugin;

    public MainWindow(Plugin plugin) : base(
        "xivclone", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        this.SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(375, 330),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        this.Plugin = plugin;
    }

    public void Dispose()
    {
    }

    public override void Draw()
    {
        if (ImGui.Button("Show Settings"))
        {
            this.Plugin.DrawConfigUI();
        }

        ImGui.SameLine();
        if(ImGui.Button("Revert snapshots"))
        {
            this.Plugin.SnapshotManager.RevertAllSnapshots();
        }

        ImGui.SameLine();
        if(ImGui.Button("Import MCDF file"))
        {
            Plugin.FileDialogManager.OpenFileDialog("Snapshot selection", ".mcdf", (status, path) =>
            {
                if (!status)
                {
                    return;
                }

                if (File.Exists(path[0]))
                {
                    this.Plugin.MCDFManager.LoadMareCharaFile(path[0]);
                    this.Plugin.MCDFManager.ExtractMareCharaFile();
                }
            }, 1, Plugin.Configuration.WorkingDirectory);
        }

        ImGui.SameLine();
        if(ImGui.Button("Export snapshot as PMP"))
        {
            Plugin.FileDialogManager.OpenFolderDialog("Snapshot selection", (status, path) =>
            {
                if (!status)
                {
                    return;
                }

                if (Directory.Exists(path))
                {
                    var glamourerString = Plugin.PMPExportManager.SnapshotToPMP(path);
                    if (glamourerString != "")
                    {
                        if (Plugin.Configuration.CopyGlamourerString)
                        {
                            ImGui.SetClipboardText(glamourerString);
                        }
                    }
                    else
                    {
                        Logger.Error("Failed to export snapshot as PMP");
                    }
                }
            }, Plugin.Configuration.WorkingDirectory);
        }

        ImGui.Spacing();

        this.DrawPlayerSelector();
        if (!currentLabel.Any())
            return;

        ImGui.SameLine();
        this.DrawActorPanel();
    }

}
