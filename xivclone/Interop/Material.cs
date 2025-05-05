using System.Runtime.InteropServices;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;

namespace xivclone.Interop;

[StructLayout( LayoutKind.Explicit )]
public unsafe struct Material
{
    [FieldOffset( 0x10 )]
    public ResourceHandle* ResourceHandle;
}

[StructLayout( LayoutKind.Explicit )]
public unsafe struct MaterialData
{
    [FieldOffset( 0x0 )]
    public byte* Data;
}
