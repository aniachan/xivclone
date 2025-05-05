using Dalamud.Configuration;
using Dalamud.Plugin;
using System;

namespace xivclone
{
    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 0;

        public string WorkingDirectory { get; set; } = string.Empty;

        public string PenumbraDirectory { get; set; } = string.Empty;

        public bool CopyGlamourerString { get; set; } = false;

        // the below exist just to make saving less cumbersome
        [NonSerialized]
        private IDalamudPluginInterface? PluginInterface;

        public void Initialize(IDalamudPluginInterface pluginInterface)
        {
            this.PluginInterface = pluginInterface;
        }

        public void Save()
        {
            this.PluginInterface!.SavePluginConfig(this);
        }
    }
}
