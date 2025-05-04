using Newtonsoft.Json.Linq;
using System;

namespace xivclone.Managers.AutoInstaller
{
    internal class AutoMod
    {
        public string Name { get; set; } = "";
        public string PackFilename { get; set; } = "";
        public string SnapshotPath { get; set; } = "";
        public Guid DesignGuid { get; set; } = Guid.Empty;

        public JObject Design { get; set; } = new();
        public string Customize { get; set; } = "";
    }
}
