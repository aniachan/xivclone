using Dalamud.Game.ClientState.Objects.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using FFXIVClientStructs.FFXIV.Client.System.Resource;
using xivclone.Models;
using xivclone.Utils;
using System.Threading;
using Penumbra.String;
using System.IO;
using System.Text.Json;
using Dalamud.Utility;
using System.Text.Encodings.Web;
using ImGuiNET;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using System.Text;

namespace xivclone.Managers
{
    public class SnapshotManager
    {
        private Plugin Plugin;
        private List<ICharacter> tempCollections = new();
        public SnapshotManager(Plugin plugin)
        {
            this.Plugin = plugin;
        }

        public void RevertAllSnapshots()
        {
            foreach(var character in tempCollections)
            {
                Logger.Warn($"Removing collection for character {character.Name.TextValue}");
                Plugin.IpcManager.PenumbraRemoveTemporaryCollection(character.Name.TextValue);
                Plugin.IpcManager.RevertGlamourerState(character);
                Plugin.IpcManager.RevertCustomizePlusScale(character.Address);
            }
            tempCollections.Clear();
        }

        public bool AppendSnapshot(ICharacter character, string snapshotName)
        {
            var charaName = character.Name.TextValue;
            var path = Path.Combine(Plugin.Configuration.WorkingDirectory, charaName);
            string infoJson = File.ReadAllText(Path.Combine(path, "snapshot.json"));
            if (infoJson == null)
            {
                Logger.Warn("No snapshot json found, aborting");
                return false;
            }
            SnapshotInfo? snapshotInfo = JsonSerializer.Deserialize<SnapshotInfo>(infoJson);
            if (snapshotInfo == null)
            {
                Logger.Warn("Failed to deserialize snapshot json, aborting");
                return false;
            }

                if (!Directory.Exists(path))
                {
                    //no existing snapshot for character, just use save mode
                    this.SaveSnapshot(character, snapshotName);
                }

            //Merge file replacements
            List<FileReplacement> replacements = GetFileReplacementsForCharacter(character);

            Logger.Debug($"Got {replacements.Count} replacements");

            foreach (var replacement in replacements)
            {
                FileInfo replacementFile = new FileInfo(replacement.ResolvedPath);
                FileInfo fileToCreate = new FileInfo(Path.Combine(path, replacement.GamePaths[0]));
                if (!fileToCreate.Exists)
                {
                    //totally new file
                    fileToCreate.Directory.Create();
                    replacementFile.CopyTo(fileToCreate.FullName);
                    foreach (var gamePath in replacement.GamePaths)
                    {
                        var collisions = snapshotInfo.FileReplacements.Where(src => src.Value.Any(path => path == gamePath));
                        //gamepath already exists in snapshot, overwrite with new file
                        foreach (var collision in collisions)
                        {
                            collision.Value.Remove(gamePath);
                            if (collision.Value.Count == 0)
                            {
                                //delete file if it no longer has any references
                                snapshotInfo.FileReplacements.Remove(collision.Key);
                                File.Delete(Path.Combine(path, collision.Key));
                            }
                        }
                    }
                    snapshotInfo.FileReplacements.Add(replacement.GamePaths[0], replacement.GamePaths);
                }
            }

            //Merge meta manips
            //Meta manipulations seem to be sent containing every mod a character has enabled, regardless of whether it's actively being used.
            //This may end up shooting me in the foot, but a newer snapshot should contain the info of an older one.
            snapshotInfo.ManipulationString = Plugin.IpcManager.GetMetaManipulations(character.ObjectIndex);

            // Save the glamourer string
            snapshotInfo.GlamourerString = Plugin.IpcManager.GetGlamourerState(character);

            string infoJsonWrite = JsonSerializer.Serialize(snapshotInfo);
            File.WriteAllText(Path.Combine(path, "snapshot.json"), infoJsonWrite);

            return true;
        }

        public void CopyGlamourerStringToClipboard(ICharacter character)
        {
            var glamourerString = Plugin.IpcManager.GlamourerIpc.GetCharacterCustomization(character.Address);
            if (string.IsNullOrEmpty(glamourerString))
            {
                Logger.Warn("Failed to get Glamourer string for clipboard.");
                return;
            }
            ImGui.SetClipboardText(glamourerString);
            Logger.Info($"Copied Glamourer string for {character.Name.TextValue} to clipboard.");
        }

        public bool SaveSnapshot(ICharacter character, string snapshotName)
        {
            var charaName = character.Name.TextValue;

            var cstring = "";

            var snapName = charaName + "_" + snapshotName;
            var path = Path.Combine(Plugin.Configuration.WorkingDirectory,snapName);
            SnapshotInfo snapshotInfo = new();

            if (Directory.Exists(path))
            {
                Logger.Warn("Snapshot already existed, deleting");
                Directory.Delete(path, true);
            }
            Directory.CreateDirectory(path);

            //Get glamourer string
            snapshotInfo.GlamourerString = Plugin.IpcManager.GetGlamourerState(character);
            Logger.Debug($"Got glamourer string {snapshotInfo.GlamourerString}");

            //Get glamourer json
            JObject obj = Plugin.IpcManager.GetGlamourerStateJSON(character);
            if (obj != null)
            {
                snapshotInfo.GlamourerJSON = Convert.ToBase64String(Encoding.UTF8.GetBytes(obj.ToString()));
            } else
            {
                Logger.Warn("Failed to get Glamourer JSON");
            }

            //Save all file replacements
            List<FileReplacement> replacements = GetFileReplacementsForCharacter(character);

            Logger.Debug($"Got {replacements.Count} replacements");

            Logger.Debug($"Path: {path}");

            foreach(var replacement in replacements)
            {
                FileInfo replacementFile = new FileInfo(replacement.ResolvedPath);
                FileInfo fileToCreate = new FileInfo(Path.Combine(path, replacement.GamePaths[0]));
                fileToCreate.Directory.Create();
                try
                {
                    replacementFile.CopyTo(fileToCreate.FullName);
                }
                catch (Exception ex)
                {
                   Logger.Debug($"An error occurred while copying the file: {ex.Message}");
                }
                snapshotInfo.FileReplacements.Add(replacement.GamePaths[0], replacement.GamePaths);
            }

            snapshotInfo.ManipulationString = Plugin.IpcManager.GetMetaManipulations(character.ObjectIndex);

            //Get customize+ data, if applicable
            if (Plugin.IpcManager.IsCustomizePlusAvailable().Available)
            {
                Logger.Debug("C+ api loaded");
                var data = Plugin.IpcManager.GetCustomizePlusScale(character);

                Logger.Debug(Plugin.DalamudUtil.PlayerName);
                Logger.Debug(character.Name.TextValue);
                if (!data.IsNullOrEmpty())
                {
                    snapshotInfo.CustomizeData = data;
                    cstring = data;
                }
            }
                var serializerOptions = new JsonSerializerOptions
                {
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                    WriteIndented = true
                };
            string infoJson = JsonSerializer.Serialize(snapshotInfo, serializerOptions);
            File.WriteAllText(Path.Combine(path, "snapshot.json"), infoJson);

            return true;
        }

        public bool LoadSnapshot(ICharacter characterApplyTo, int objIdx, string path)
        {
            Logger.Info($"Applying snapshot to {characterApplyTo.Address}");
            string infoJson = File.ReadAllText(Path.Combine(path, "snapshot.json"));
            if (infoJson == null)
            {
                Logger.Warn("No snapshot json found, aborting");
                return false;
            }
            var serializerOptions = new JsonSerializerOptions
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                WriteIndented = true
            };
            SnapshotInfo? snapshotInfo = JsonSerializer.Deserialize<SnapshotInfo>(infoJson, serializerOptions);
            if (snapshotInfo == null)
            {
                Logger.Warn("Failed to deserialize snapshot json, aborting");
                return false;
            }

            //Apply mods
            Dictionary<string, string> moddedPaths = new();
            foreach (var replacement in snapshotInfo.FileReplacements)
            {
                foreach (var gamePath in replacement.Value)
                {
                    moddedPaths.Add(gamePath, Path.Combine(path, replacement.Key));
                }
            }
            Logger.Debug($"Applied {moddedPaths.Count} replacements");

            Plugin.IpcManager.PenumbraRemoveTemporaryCollection(characterApplyTo.Name.TextValue);
            Plugin.IpcManager.PenumbraSetTempMods(characterApplyTo, objIdx, moddedPaths, snapshotInfo.ManipulationString);
            if (!tempCollections.Contains(characterApplyTo))
            {
                tempCollections.Add(characterApplyTo);
            }

            //Apply Customize+ if it exists and C+ is installed
            if (Plugin.IpcManager.IsCustomizePlusAvailable().Available)
            {
                if (!snapshotInfo.CustomizeData.IsNullOrEmpty())
                {
                    Plugin.IpcManager.SetCustomizePlusScale(characterApplyTo.Address, snapshotInfo.CustomizeData);
                }
            }

            //Apply glamourer string
            Plugin.IpcManager.ApplyGlamourerState(snapshotInfo.GlamourerString, characterApplyTo);

            //Redraw
            Plugin.IpcManager.PenumbraRedraw(objIdx);

            return true;
        }

        private int? GetObjIDXFromCharacter(ICharacter character)
        {
            for (var i = 0; i <= Plugin.Objects.Length; i++)
            {
                global::Dalamud.Game.ClientState.Objects.Types.IGameObject current = Plugin.Objects[i];
                if (!(current == null) && current.GameObjectId == character.GameObjectId)
                {
                    return i;
                }
            }
            return null;
        }

        public unsafe List<FileReplacement> GetFileReplacementsForCharacter(ICharacter character)
        {
            List<FileReplacement> replacements = new List<FileReplacement>();
            var charaPointer = character.Address;
            var objectKind = character.ObjectKind;
            var charaName = character.Name.TextValue;
            int? objIdx = GetObjIDXFromCharacter(character);

            Logger.Debug($"Character name {charaName}");
            if (objIdx == null)
            {
                Logger.Error("Unable to find character in object table, aborting search for file replacements");
                return replacements;
            }
            Logger.Debug($"Object IDX {objIdx}");

            var chara = Plugin.DalamudUtil.CreateGameObject(charaPointer)!;
            while (!Plugin.DalamudUtil.IsObjectPresent(chara))
            {
                Logger.Verbose("Character is null but it shouldn't be, waiting");
                Thread.Sleep(50);
            }

            Plugin.DalamudUtil.WaitWhileCharacterIsDrawing(objectKind.ToString(), charaPointer, 15000);

            var baseCharacter = (FFXIVClientStructs.FFXIV.Client.Game.Character.Character*)(void*)charaPointer;
            var human = (Human*)baseCharacter->GameObject.GetDrawObject();
            for (var mdlIdx = 0; mdlIdx < human->CharacterBase.SlotCount; ++mdlIdx)
            {
                var mdl = (xivclone.Interop.RenderModel*)human->CharacterBase.Models[mdlIdx];
                if (mdl == null || mdl->ResourceHandle == null || mdl->ResourceHandle->Category != ResourceCategory.Chara)
                {
                    continue;
                }

                AddReplacementsFromRenderModel(mdl, replacements, objIdx.Value, 0);
            }

            AddPlayerSpecificReplacements(replacements, charaPointer, human, objIdx.Value);

            return replacements;
        }

        private unsafe void AddReplacementsFromRenderModel(xivclone.Interop.RenderModel* mdl, List<FileReplacement> replacements, int objIdx, int inheritanceLevel = 0)
        {
            if (mdl == null || mdl->ResourceHandle == null || mdl->ResourceHandle->Category != ResourceCategory.Chara)
            {
                return;
            }

            string mdlPath;
            try
            {
                mdlPath = new ByteString(mdl->ResourceHandle->FileName()).ToString();
            }
            catch
            {
                Logger.Warn("Could not get model data");
                return;
            }
            Logger.Verbose("Checking File Replacement for Model " + mdlPath);

            FileReplacement mdlFileReplacement = CreateFileReplacement(mdlPath, objIdx);

            AddFileReplacement(replacements, mdlFileReplacement);

            for (var mtrlIdx = 0; mtrlIdx < mdl->MaterialCount; mtrlIdx++)
            {
                var mtrl = (xivclone.Interop.Material*)mdl->Materials[mtrlIdx];
                if (mtrl == null) continue;

                AddReplacementsFromMaterial(mtrl, replacements, objIdx, inheritanceLevel + 1);
            }
        }

        private unsafe void AddReplacementsFromMaterial(xivclone.Interop.Material* mtrl, List<FileReplacement> replacements, int objIdx, int inheritanceLevel = 0)
        {
            string fileName;
            try
            {
                fileName = new ByteString(mtrl->ResourceHandle->FileName()).ToString();

            }
            catch
            {
                Logger.Warn("Could not get material data");
                return;
            }

            Logger.Verbose("Checking File Replacement for Material " + fileName);
            var mtrlArray = fileName.Split("|");
            string mtrlPath;
            if (mtrlArray.Count() >= 3)
            {
                mtrlPath = fileName.Split("|")[2];
            }
            else
            {
                Logger.Warn($"Material {fileName} did not split into at least 3 parts");
                return;
            }

            if (replacements.Any(c => c.ResolvedPath.Contains(mtrlPath, StringComparison.Ordinal)))
            {
                return;
            }

            var mtrlFileReplacement = CreateFileReplacement(mtrlPath, objIdx);

            AddFileReplacement(replacements, mtrlFileReplacement);

            var mtrlResourceHandle = (xivclone.Interop.MtrlResource*)mtrl->ResourceHandle;
            for (var resIdx = 0; resIdx < mtrlResourceHandle->NumTex; resIdx++)
            {
                string? texPath = null;
                try
                {
                    texPath = new ByteString(mtrlResourceHandle->TexString(resIdx)).ToString();
                }
                catch
                {
                    Logger.Warn("Could not get Texture data for Material " + fileName);
                }

                if (string.IsNullOrEmpty(texPath)) continue;

                Logger.Verbose("Checking File Replacement for Texture " + texPath);

                AddReplacementsFromTexture(texPath, replacements, objIdx, inheritanceLevel + 1);
            }

            try
            {
                var shpkPath = "shader/sm5/shpk/" + new ByteString(mtrlResourceHandle->ShpkString).ToString();
                Logger.Verbose("Checking File Replacement for Shader " + shpkPath);
                AddReplacementsFromShader(shpkPath, replacements, objIdx, inheritanceLevel + 1);
            }
            catch
            {
                Logger.Verbose("Could not find shpk for Material " + fileName);
            }
        }

        private void AddReplacementsFromTexture(string texPath, List<FileReplacement> replacements, int objIdx, int inheritanceLevel = 0, bool doNotReverseResolve = true)
        {
            if (string.IsNullOrEmpty(texPath) || texPath.Any(c => c < 32 || c > 126)) // Check for invalid characters
            {
                Logger.Warn($"Invalid texture path: {texPath}");
                return;
            }

            Logger.Debug($"Adding replacement for texture {texPath}");

            if (replacements.Any(c => c.GamePaths.Contains(texPath, StringComparer.Ordinal)))
            {
                Logger.Debug($"Replacements already contain {texPath}, skipping");
                return;
            }

            var texFileReplacement = CreateFileReplacement(texPath, objIdx, doNotReverseResolve);
            AddFileReplacement(replacements, texFileReplacement);

            if (texPath.Contains("/--", StringComparison.Ordinal)) return;

            var texDx11Replacement = CreateFileReplacement(texPath.Insert(texPath.LastIndexOf('/') + 1, "--"), objIdx, doNotReverseResolve);
            AddFileReplacement(replacements, texDx11Replacement);
        }

        private void AddReplacementsFromShader(string shpkPath, List<FileReplacement> replacements, int objIdx, int inheritanceLevel = 0)
        {
            if (string.IsNullOrEmpty(shpkPath)) return;

            if (replacements.Any(c => c.GamePaths.Contains(shpkPath, StringComparer.Ordinal)))
            {
                return;
            }

            var shpkFileReplacement = CreateFileReplacement(shpkPath, objIdx);
            AddFileReplacement(replacements, shpkFileReplacement);
        }

        // TODO: Figure out whether this causes crashes...
        private unsafe void AddPlayerSpecificReplacements(List<FileReplacement> replacements, IntPtr charaPointer, Human* human, int objIdx)
        {
            var weaponObject = (Interop.Weapon*)((FFXIVClientStructs.FFXIV.Client.Graphics.Scene.Object*)human)->ChildObject;

            if ((IntPtr)weaponObject != IntPtr.Zero)
            {
                var mainHandWeapon = weaponObject->WeaponRenderModel->RenderModel;

                AddReplacementsFromRenderModel(mainHandWeapon, replacements, objIdx, 0);

                if (weaponObject->NextSibling != (IntPtr)weaponObject)
                {
                    var offHandWeapon = ((Interop.Weapon*)weaponObject->NextSibling)->WeaponRenderModel->RenderModel;

                    AddReplacementsFromRenderModel(offHandWeapon, replacements, objIdx, 1);
                }
            }

            AddReplacementSkeleton(((Interop.HumanExt*)human)->Human.RaceSexId, objIdx, replacements);
            try
            {
                var decal = ((Interop.HumanExt*)human)->Decal;
                if (decal != null)
                {
                    var fileName = decal->FileName();
                    if (fileName != null)
                    {
                        AddReplacementsFromTexture(new ByteString(fileName).ToString(), replacements, objIdx, 0, false);
                    }
                    else
                    {
                        Logger.Debug("Decal FileName was null");
                    }
                }
                else
                {
                    Logger.Debug("Decal pointer was null");
                }
            }
            catch
            {
                Logger.Warn("Could not get Decal data. Possible memory access issue?");
            }

            try
            {
                var legacyDecal = ((Interop.HumanExt*)human)->LegacyBodyDecal;
                if (legacyDecal != null)
                {
                    var fileName = legacyDecal->FileName();
                    if (fileName != null)
                    {
                        AddReplacementsFromTexture(new ByteString(fileName).ToString(), replacements, objIdx, 0, false);
                    }
                    else
                    {
                        Logger.Debug("Legacy Body Decal FileName was null");
                    }
                }
                else
                {
                    Logger.Debug("Legacy Body Decal pointer was null");
                }
            }
            catch
            {
                Logger.Warn("Could not get Legacy Body Decal data. Possible memory access issue?");
            }
        }

        private void AddReplacementSkeleton(ushort raceSexId, int objIdx, List<FileReplacement> replacements)
        {
            string raceSexIdString = raceSexId.ToString("0000");

            string skeletonPath = $"chara/human/c{raceSexIdString}/skeleton/base/b0001/skl_c{raceSexIdString}b0001.sklb";

            var replacement = CreateFileReplacement(skeletonPath, objIdx, true);
            AddFileReplacement(replacements, replacement);
        }

        private void AddFileReplacement(List<FileReplacement> replacements, FileReplacement newReplacement)
        {
            if (!newReplacement.HasFileReplacement)
            {
                Logger.Debug($"Replacement for {newReplacement.ResolvedPath} does not have a file replacement, skipping");
                foreach (var path in newReplacement.GamePaths)
                {
                    Logger.Debug(path);
                }
                return;
            }

            var existingReplacement = replacements.SingleOrDefault(f => string.Equals(f.ResolvedPath, newReplacement.ResolvedPath, System.StringComparison.OrdinalIgnoreCase));
            if (existingReplacement != null)
            {
                Logger.Debug($"Added replacement for existing path {existingReplacement.ResolvedPath}");
                existingReplacement.GamePaths.AddRange(newReplacement.GamePaths.Where(e => !existingReplacement.GamePaths.Contains(e, System.StringComparer.OrdinalIgnoreCase)));
            }
            else
            {
                Logger.Debug($"Added new replacement {newReplacement.ResolvedPath}");
                replacements.Add(newReplacement);
            }
        }

        private FileReplacement CreateFileReplacement(string path, int objIdx, bool doNotReverseResolve = false)
        {
            var fileReplacement = new FileReplacement(Plugin);

            if (!doNotReverseResolve)
            {
                fileReplacement.ReverseResolvePathObject(path, objIdx);
            }
            else
            {
                fileReplacement.ResolvePathObject(path, objIdx);
            }

            Logger.Debug($"Created file replacement for resolved path {fileReplacement.ResolvedPath}, hash {fileReplacement.Hash}, gamepath {fileReplacement.GamePaths[0]}");
            return fileReplacement;
        }

        public bool InstallMod(string fullPath)
        {
            Logger.Debug($"Triggering install of mod {fullPath}");
            return Plugin.IpcManager.PenumbraInsallMod(fullPath);
        }

        public bool SetModPath(string modName, string newPath)
        {
            Logger.Debug($"Triggering set mod path {modName} to {newPath}");
            return Plugin.IpcManager.PenumbraSetModPath(modName, newPath);
        }

        public Guid AddDesign(JObject design, string name)
        {
            Logger.Debug($"Triggering add design {name}");
            return Plugin.IpcManager.AddGlamourerDesign(design, name);
        }

        public bool AddCustomizeTemplate(string templateData, string name)
        {
            Logger.Debug($"Triggering add c+ template {name}");
            return Plugin.IpcManager.AddCustomizePlusTemplate(templateData, name);
        }

        public bool AddMod(string modname)
        {
            Logger.Debug($"Triggering add mod {modname}");
            return Plugin.IpcManager.PenumbraAddMod(modname);
        }
    }
}
