using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Interface.Windowing;
using xivclone.Windows;
using Dalamud.Interface.ImGuiFileDialog;
using xivclone.Utils;
using xivclone.Managers;
using MareSynchronos.Export;
using xivclone.PMP;
using Snapper.Managers;

namespace xivclone
{
    public sealed class Plugin : IDalamudPlugin
    {
        public string Name => "xivclone";
        private const string CommandName = "/clone";

        public Configuration Configuration { get; init; }
        public IObjectTable Objects { get; init; }
        public WindowSystem WindowSystem = new("xivclone");
        public FileDialogManager FileDialogManager = new FileDialogManager();
        public DalamudUtil DalamudUtil { get; init; }
        public IpcManager IpcManager { get; init; }
        public SnapshotManager SnapshotManager { get; init; }
        public MareCharaFileManager MCDFManager { get; init; }
        public PMPExportManager PMPExportManager { get; init; }

        private ConfigWindow ConfigWindow { get; init; }
        private MainWindow MainWindow { get; init; }

        [PluginService] public static IPluginLog Log { get; private set; } = null!;
        [PluginService] public static ICommandManager CommandManager { get; private set; } = null!;
        [PluginService] public static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
        //[PluginService] public static Configuration Configuration { get; private set; } = null!;

        //public IPluginLog PluginLog { get; private set; } = null!;
        //public static object Log { get; internal set; }

        public Plugin(
            IFramework framework,
            IObjectTable objectTable,
            IClientState clientState,
            ICondition condition,
            IChatGui chatGui)
        {
            this.Objects = objectTable;

            this.DalamudUtil = new DalamudUtil(clientState, objectTable, framework, condition, chatGui);
            this.IpcManager = new IpcManager(PluginInterface, this.DalamudUtil);

            Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            Configuration.Initialize(PluginInterface);

            this.SnapshotManager = new SnapshotManager(this);
            this.MCDFManager = new MareCharaFileManager(this);
            this.PMPExportManager = new PMPExportManager(this);

            ConfigWindow = new ConfigWindow(this);
            MainWindow = new MainWindow(this);
            WindowSystem.AddWindow(ConfigWindow);
            WindowSystem.AddWindow(MainWindow);

            CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "Opens main xivclone interface"
            });

            PluginInterface.UiBuilder.Draw += DrawUI;
            PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;
            PluginInterface.UiBuilder.DisableGposeUiHide = true;
            PluginInterface.UiBuilder.OpenMainUi += ToggleMainUI;
        }

        public void Dispose()
        {
            this.WindowSystem.RemoveAllWindows();
            CommandManager.RemoveHandler(CommandName);
            this.SnapshotManager.RevertAllSnapshots();
        }

        private void OnCommand(string command, string args)
        {
            // in response to the slash command, just display our main ui
            ToggleMainUI();
        }

        private void DrawUI()
        {
            this.WindowSystem.Draw();
            this.FileDialogManager.Draw();
        }

        public void DrawConfigUI()
        {
            ConfigWindow.IsOpen = true;
        }

        public void ToggleMainUI() => MainWindow.Toggle();
    }
}
