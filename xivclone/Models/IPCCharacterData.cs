using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace xivclone.Models
{
    public class IPCCharacterDataTuple
    {
        public string Name { get; set; } = string.Empty;
        public byte CharacterType { get; set; }
        public uint WorldId { get; set; }
        public ushort CharacterSubType { get; set; }
    }
}
