using System;
using System.Collections.Generic;
using System.IO;
using xivclone.Utils;
using System.Linq;
using System.Text.RegularExpressions;

namespace xivclone.PMP
{
    public partial class PMPExportManager
    {
        private PMPDefaultMod BuildCorrectedDefaultMod(Dictionary<string, List<string>> fileReplacements)
        {
            var defaultMod = new PMPDefaultMod();
            var allPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // First pass — collect all real files
            foreach (var file in fileReplacements)
            {
                foreach (var replacement in file.Value)
                {
                    defaultMod.Files.Add(replacement, file.Key);
                    allPaths.Add(replacement);
                }
            }

            // Second pass — add missing alternate prefix copies for 0101 <-> 0201
            foreach (var entry in defaultMod.Files.ToList())
            {
                string path = entry.Key;

                var match = Regex.Match(path, @"(c0[12]01)(e\d{4}_.+\.(mdl|mtrl))", RegexOptions.IgnoreCase);
                if (!match.Success)
                    continue;

                string currentPrefix = match.Groups[1].Value;
                string suffix = match.Groups[2].Value;

                string altPrefix = currentPrefix == "c0101" ? "c0201" : "c0101";
                string altPath = path.Replace(currentPrefix, altPrefix);

                if (!allPaths.Contains(altPath))
                {
                    defaultMod.Files[altPath] = entry.Value;
                    allPaths.Add(altPath);
                    Logger.Debug($"Added fallback replacement: {altPath} → {entry.Value}");
                }
            }

            return defaultMod;
        }
    }
}
