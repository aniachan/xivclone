// PenumbraIpc.Defs.cs
using xivclone.Managers;
using xivclone.Utils;

namespace xivclone.Managers.Penumbra;

public partial class PenumbraIpc
{
    public event VoidDelegate? PenumbraModSettingChanged;
    public event VoidDelegate? PenumbraInitialized;
    public event VoidDelegate? PenumbraDisposed;
    public event PenumbraRedrawEvent? PenumbraRedrawEvent;
    public event PenumbraResourceLoadEvent? PenumbraResourceLoadEvent;
}
