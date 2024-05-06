using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace xivclone.PMP
{
    internal class PMPMetadata
    {
        public int FileVersion { get; set; } = 3;
        public string Name { get; set; } = "";
        public string Author { get; set; } = "XIVxivclone";
        public string Description { get; set; } = "Mod generated from xivclone snapshot";
        public string Version { get; set; } = "1.0.0";
        public string Website { get; set; } = "";
        public string[] ModTags { get; set; } = { };
    }
}
