// For Customize+ integration
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
