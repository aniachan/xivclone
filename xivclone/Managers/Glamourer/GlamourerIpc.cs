// GlamourerIpc.cs
using Dalamud.Plugin;
using Dalamud.Game.ClientState.Objects.Types;
using Glamourer.Api.IpcSubscribers;
using Glamourer.Api.Enums;
using System;
using System.Collections.Concurrent;
using xivclone.Utils;
using Dalamud.Utility;
using Glamourer.Api.Api;
using Newtonsoft.Json.Linq;
using System.Text;

namespace xivclone.Managers.Glamourer;

public partial class GlamourerIpc : IDisposable
{
    private readonly DalamudUtil _dalamudUtil;
    private readonly ConcurrentQueue<Action> _queue;
    private readonly ApplyState _apply;
    private readonly RevertState _revert;
    private readonly GetStateBase64 _get;
    private readonly GetState _getJson;
    private readonly UnlockState _unlock;
    private readonly UnlockStateName _unlockName;
    private readonly ApiVersion _version;
    private readonly AddDesign _addDesign;
    private readonly string _backupBase64 = "";
    private Func<ICharacter, string?>? _getBase64FromCharacter;
    private readonly IDalamudPluginInterface _pluginInterface;

    private readonly uint lockCode = 0x6D617265;

    public GlamourerIpc(IDalamudPluginInterface pi, DalamudUtil dalamudUtil, ConcurrentQueue<Action> queue)
    {
        _dalamudUtil = dalamudUtil;
        _pluginInterface = pi;
        _queue = queue;
        _version = new ApiVersion(pi);
        _get = new GetStateBase64(pi);
        _getJson = new GetState(pi);
        _unlock = new UnlockState(pi);
        _unlockName = new UnlockStateName(pi);
        _apply = new ApplyState(pi);
        _revert = new RevertState(pi);
        _addDesign = new AddDesign(pi);

        // Defer IPC hook via Framework.Update
        _dalamudUtil.FrameworkUpdate += WaitForGlamourer;
    }

    private void WaitForGlamourer()
    {
        try
        {
            if (_getBase64FromCharacter == null)
            {
                _getBase64FromCharacter = _pluginInterface
                    .GetIpcSubscriber<ICharacter, string?>("Glamourer.GetStateBase64FromCharacter")
                    .InvokeFunc!;
                Logger.Info("Glamourer IPC hooked!");
            }
        }
        catch
        {
            // Try again next frame
            return;
        }

        // IPC is now available, stop checking
        _dalamudUtil.FrameworkUpdate -= WaitForGlamourer;
    }

    public void Dispose() { }

    public void ApplyState(string? base64, ICharacter obj)
    {
        if (!Check() || string.IsNullOrEmpty(base64)) return;
        Logger.Verbose("Glamourer applying for " + obj.Address.ToString("X"));
        _apply.Invoke(base64, obj.ObjectIndex, lockCode);
    }

    public void RevertState(IGameObject obj)
    {
        if (!Check()) return;
        if (obj is ICharacter c)
        {
            _unlock!.Invoke(c.ObjectIndex, lockCode);
            _revert!.Invoke(c.ObjectIndex);
        }
        else
        {
            Logger.Error("Tried to revert non-character with Glamourer");
        }
    }

    public string GetCharacterCustomization(IntPtr ptr)
    {
        if (!Check()) return _backupBase64;
        try
        {
            if (_dalamudUtil.CreateGameObject(ptr) is ICharacter c)
            {
                Logger.Verbose($"Unlocking customization string for {c.Name}");
                _unlock!.Invoke(c.ObjectIndex, lockCode);

                Logger.Debug($"Getting customizations for {c.Name} (Index {c.ObjectIndex})");
                var (_, result) = _get!.Invoke(c.ObjectIndex);

                Logger.Verbose($"Received customizations: {result}");

                var base64 = result.IsNullOrEmpty() ? "ZXJy" : result;

                try { Convert.FromBase64String(base64); }
                catch { base64 = "ZXJy"; }

                return Convert.ToBase64String(Convert.FromBase64String(base64));
            }
        }
        catch (Exception ex)
        {
            Logger.Warn($"Glamourer IPC error: {ex.Message}");
        }

        Logger.Warn("Falling back to stored base64");
        return SafeBase64(_backupBase64);
    }

    public JObject? GetCharacterCustomizationJson(IntPtr ptr)
    {
        if (!Check()) return null;
        try
        {
            if (_dalamudUtil.CreateGameObject(ptr) is ICharacter c)
            {
                Logger.Verbose($"Unlocking customization string for {c.Name}");
                _unlock!.Invoke(c.ObjectIndex, lockCode);

                Logger.Debug($"Getting customizations for {c.Name} (Index {c.ObjectIndex})");
                var (_, result) = _getJson!.Invoke(c.ObjectIndex);

                Logger.Verbose("Received customization JSON object");

                if (result is JObject jObject)
                {
                    return jObject;
                }
                return null;
            }
        }
        catch (Exception ex)
        {
            Logger.Warn($"Glamourer IPC error: {ex.Message}");
        }
        return null;
    }

    public Guid AddDesign(JObject design, string name)
    {
        if (!Check() || design == null)
            return Guid.Empty;
        try
        {
            Logger.Debug($"Creating design {name}...");
            byte[] bytes = Encoding.UTF8.GetBytes(design.ToString());
            string base64 = Convert.ToBase64String(bytes);
            if (_addDesign.Invoke(base64, name, out Guid guid) == GlamourerApiEc.Success)
                return guid;
        }
        catch (Exception ex)
        {
            Logger.Warn($"Glamourer IPC error: {ex.Message}");
        }
        return Guid.Empty;
    }

    public bool Check()
    {
        try
        {
            return _version.Invoke() is { Major: 1, Minor: >= 6 };
        }
        catch
        {
            Logger.Warn("Glamourer not available");
            return false;
        }
    }

    private string SafeBase64(string input)
    {
        try
        {
            var bytes = Convert.FromBase64String(input);
            return Convert.ToBase64String(bytes);
        }
        catch
        {
            return _backupBase64;
        }
    }

}
