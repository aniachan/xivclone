// CustomizeIpc.cs
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using xivclone.Utils;
using System;
using System.Collections.Concurrent;
using System.Text;
using Dalamud.Utility;

namespace xivclone.Managers.Customize;

public partial class CustomizeIpc : IDisposable
{
    private readonly DalamudUtil _dalamudUtil;
    private readonly ConcurrentQueue<Action> _queue;

    private readonly ICallGateSubscriber<(int, int)> _ApiVersion;
    private readonly ICallGateSubscriber<ushort, (int, Guid?)> _GetActiveProfile;
    private readonly ICallGateSubscriber<Guid, (int, string?)> _GetProfileById;
    private readonly ICallGateSubscriber<ushort, Guid, object> _OnScaleUpdate;
    private readonly ICallGateSubscriber<ushort, int> _RevertCharacter;
    private readonly ICallGateSubscriber<ushort, string, (int, Guid?)> _SetBodyScaleToCharacter;
    private readonly ICallGateSubscriber<Guid, int> _DeleteByUniqueId;
    private readonly ICallGateSubscriber<string, string, int> _CreateTemplate;

    public CustomizeIpc(IDalamudPluginInterface pi, DalamudUtil dalamudUtil, ConcurrentQueue<Action> queue)
    {
        _dalamudUtil = dalamudUtil;
        _queue = queue;

        _ApiVersion = pi.GetIpcSubscriber<(int, int)>("CustomizePlus.General.GetApiVersion");
        _GetActiveProfile = pi.GetIpcSubscriber<ushort, (int, Guid?)>("CustomizePlus.Profile.GetActiveProfileIdOnCharacter");
        _GetProfileById = pi.GetIpcSubscriber<Guid, (int, string?)>("CustomizePlus.Profile.GetByUniqueId");
        _RevertCharacter = pi.GetIpcSubscriber<ushort, int>("CustomizePlus.Profile.DeleteTemporaryProfileOnCharacter");
        _SetBodyScaleToCharacter = pi.GetIpcSubscriber<ushort, string, (int, Guid?)>("CustomizePlus.Profile.SetTemporaryProfileOnCharacter");
        _OnScaleUpdate = pi.GetIpcSubscriber<ushort, Guid, object>("CustomizePlus.Profile.OnUpdate");
        _DeleteByUniqueId = pi.GetIpcSubscriber<Guid, int>("CustomizePlus.Profile.DeleteTemporaryProfileByUniqueId");
        _CreateTemplate = pi.GetIpcSubscriber<string, string, int>("Template.Import");

        _OnScaleUpdate.Subscribe(OnScaleChange);

    }

    public void Dispose() { }

    public (bool Available, bool IsAniVersion) CheckApi()
    {
        try
        {
            var (major, minor) = _ApiVersion.InvokeFunc();
            var available = major == 6 && minor >= 0;
            var isAni = minor >= 100;
            return (available, isAni);
        }
        catch
        {
            return (false, false);
        }
    }


    public string GetScaleFromCharacter(ICharacter c)
    {
        if (!CheckApi().Available) return string.Empty;

        var res = _GetActiveProfile.InvokeFunc(c.ObjectIndex);
        Logger.Debug($"CustomizePlus GetActiveProfile returned {res.Item2.ToString()}");
        if (res.Item1 != 0 || res.Item2 == null) return string.Empty;
        var scale = _GetProfileById.InvokeFunc(res.Item2.Value).Item2;

        if (string.IsNullOrEmpty(scale)) return string.Empty;
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(scale));
    }

    public void SetScale(IntPtr address, string scale)
    {
        if (!CheckApi().Available || string.IsNullOrEmpty(scale)) return;
        _queue.Enqueue(() =>
        {
            var gameObj = _dalamudUtil.CreateGameObject(address);
            if (gameObj is ICharacter c)
            {
                string decodedScale = Encoding.UTF8.GetString(Convert.FromBase64String(scale));

                Logger.Verbose("CustomizePlus applying for " + c.Address.ToString("X"));

                if (decodedScale.IsNullOrEmpty())
                {
                    _RevertCharacter!.InvokeFunc(c.ObjectIndex);
                    return;
                }

                _SetBodyScaleToCharacter!.InvokeAction(c.ObjectIndex, decodedScale);
            }
        });
    }

    public void Revert(IntPtr address)
    {
        if (!CheckApi().Available) return;
        _queue.Enqueue(() =>
        {
            var gameObj = _dalamudUtil.CreateGameObject(address);
            if (gameObj is ICharacter c)
            {
                Logger.Verbose("C+ revert for: " + c.Address.ToString("X"));
                var res = _GetActiveProfile.InvokeFunc(c.ObjectIndex);
                Logger.Debug("CustomizePlus GetActiveProfile returned {err}", res.Item1.ToString());
                if (res.Item1 != 0 || res.Item2 == null) return;

                _DeleteByUniqueId.InvokeFunc(res.Item2.Value);
            }
        });
    }

    private void OnScaleChange(ushort c, Guid g)
    {
        var scale = _GetProfileById.InvokeFunc(g).Item2;
        if (scale != null) scale = Convert.ToBase64String(Encoding.UTF8.GetBytes(scale));
        CustomizePlusScaleChange?.Invoke(scale);
    }

    public bool AddTemplate(string templateData, string name)
    {
        if (!CheckApi().Available) return false;
        Logger.Debug($"Creating template {name}");
        var res = _CreateTemplate.InvokeFunc(templateData, name);
        if (res != 0)
        {
            Logger.Error($"Failed to create template {name}");
            return false;
        }
        return true;
    }
}
