using Dalamud.Interface.Colors;
using ImGuiNET;
using System;
using System.Threading.Tasks;
using System.Numerics;

namespace xivclone.Windows
{
    public partial class MainWindow
    {
        // Installation Dialog
        private bool showInstallDialog = false;
        private bool installComplete = false;
        private bool installSuccess = false;
        private string installStatusMessage = "";
        private double currentStepTime = 0.0;
        private int currentStep = 0; // Track the current installation step

        private void DrawInstallDialog()
        {
            if (!showInstallDialog)
                return;

            bool open = true;
            ImGui.SetNextWindowSize(new Vector2(300, 150), ImGuiCond.FirstUseEver);
            if (ImGui.BeginPopupModal("Installing Mod", ref open, ImGuiWindowFlags.NoResize))
            {
                if (!installComplete)
                {
                    // Show spinner while installation is happening
                    ImGui.Text(installStatusMessage);
                    UiExtensions.Spinner("##spinner", 10f, 4f, ImGui.GetColorU32(ImGuiCol.ButtonHovered));
                }
                else
                {
                    // Show final result after install
                    ImGui.PushStyleColor(ImGuiCol.Text, installSuccess ? ImGuiColors.ParsedGreen : ImGuiColors.DalamudRed);
                    ImGui.TextWrapped(installStatusMessage);
                    ImGui.PopStyleColor();

                    if (ImGui.Button("OK"))
                    {
                        ImGui.CloseCurrentPopup();
                        showInstallDialog = false;
                    }
                }

                ImGui.EndPopup();
            }
        }

        // Start the installation process and handle progress updates
        private async void StartInstallationProcess()
        {
            showInstallDialog = true;
            installStatusMessage = "Installing mod...";
            ImGui.OpenPopup("Installing Mod");

            installComplete = false;
            installSuccess = false;
            installStatusMessage = "Starting installation...";
            currentStep = 0;

            // Begin the installation in the background (on a separate thread)
            await Task.Run(async () =>
            {
                bool success = await PerformInstallationStepsAsync();
                installSuccess = success;
                installComplete = true;
                installStatusMessage = installSuccess
                    ? "Mod instawwed successfuwwy~! <3"
                    : "Mod instaaww faiwed >_< Pwease twy again.";
            });
        }

        // Perform the installation steps asynchronously
        private async Task<bool> PerformInstallationStepsAsync()
        {
            bool success = true;
            success &= await PerformStepAsync(1, "Creating snapshot... pwease wait~", AutoCreateSnapshot);
            success &= await PerformStepAsync(2, "Converting snapshot... pwease wait~", AutoConvertSnapshot);
            success &= await PerformStepAsync(3, "Installing mod... pwease wait~", AutoInstallMod);
            success &= await PerformStepAsync(4, "Building design... pwease wait~", AutoBuildDesign);
            success &= await PerformStepAsync(5, "Importing customize... pwease wait~", AutoImportCustomize);
            return success;
        }

        // Generic function to handle each installation step
        private async Task<bool> PerformStepAsync(int step, string statusMessage, Func<Task<bool>> stepAction)
        {
            currentStep = step;
            installStatusMessage = statusMessage;

            bool result = await stepAction(); // Perform the actual step logic in the background
            return result;
        }
    }
}
