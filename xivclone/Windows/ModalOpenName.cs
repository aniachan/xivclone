using ImGuiNET;
using System;
using System.Text;

namespace xivclone.Windows
{
    public partial class MainWindow
    {
        private byte[] _nameBuffer = new byte[100];
        private string _name = "";

        private bool OpenNameField(string windowTitle, ref string name)
        {
            if (!ImGui.IsPopupOpen(windowTitle)) // Only reset if the popup is not already open
            {
                _name = ""; // Clear previous input
                Array.Clear(_nameBuffer, 0, _nameBuffer.Length);
            }
            ImGui.OpenPopup(windowTitle);

            bool open = true;

            ImGui.SetNextWindowSize(new System.Numerics.Vector2(200, 110));
            if (ImGui.BeginPopupModal(windowTitle, ref open, ImGuiWindowFlags.NoResize))
            {
                ImGui.Text("Enter snapshot name (optional):");

                if (string.IsNullOrEmpty(_name))
                    _name = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");

                UpdateBufferFromName();

                unsafe
                {
                    fixed (byte* bufPtr = _nameBuffer)
                    {
                        if (ImGui.InputText("##name", (IntPtr)bufPtr, (uint)_nameBuffer.Length, ImGuiInputTextFlags.None))
                        {
                            int nullIndex = Array.IndexOf(_nameBuffer, (byte)0);
                            _name = Encoding.UTF8.GetString(_nameBuffer, 0, nullIndex >= 0 ? nullIndex : _nameBuffer.Length);

                        }
                    }
                }

                if (ImGui.Button("Save"))
                {
                    name = _name; // Pass final name back to caller
                    ImGui.CloseCurrentPopup();
                    return true;
                }
                ImGui.SameLine();
                if (ImGui.Button("Cancel"))
                {
                    ImGui.CloseCurrentPopup();
                    showSaveDialog = false; showAppendDialog = false;
                    return false;
                }

                ImGui.EndPopup();
            }
            return false;
        }

        private void UpdateBufferFromName()
        {
            Array.Clear(_nameBuffer, 0, _nameBuffer.Length);
            int len = System.Text.Encoding.UTF8.GetBytes(_name, 0, Math.Min(_name.Length, 99), _nameBuffer, 0);
            _nameBuffer[len] = 0; // Null-terminate
        }

    }
}

