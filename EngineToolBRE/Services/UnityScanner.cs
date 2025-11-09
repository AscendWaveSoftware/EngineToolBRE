using EngineToolBRE.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace EngineToolBRE.Services
{
    public static class UnityScanner
    {
        public static List<UnityInstallation> Scan()
        {
            var results = new List<UnityInstallation>();
            var roots = new List<string>();

            foreach(var drive in DriveInfo.GetDrives())
            {
                if (!drive.IsReady || drive.DriveType != DriveType.Fixed) continue;
                string root = drive.RootDirectory.FullName.TrimEnd('\\');
                roots.Add(Path.Combine(root, @"Program Files\Unity\Hub\Editor"));
                roots.Add(Path.Combine(root, @"Unity\Hub\Editor"));
                roots.Add(Path.Combine(root, @"Programme\Unity\Hub\Editor"));
            }

            roots.AddRange(new[]
            {
                @"C:\Program Files\Unity\Hub\Editor",
                @"D:\Program Files\Unity\Hub\Editor",
                @"C:\Unity\Hub\Editor",
                @"D:\Unity\Hub\Editor"
            });

            roots = roots.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            foreach(var root in roots)
            {
                if (!Directory.Exists(root)) continue;

                foreach (var dir in Directory.GetDirectories(root))
                {
                    var editorExe = Path.Combine(dir, "Editor", "Unity.exe");
                    if (File.Exists(editorExe))
                    {
                        var version = new DirectoryInfo(dir).Name;
                        results.Add(new UnityInstallation
                        {
                            Version = version,
                            EditorPath = editorExe
                        });
                    }
                }
            }

            return results
                .GroupBy(x => x.EditorPath, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .OrderByDescending(x => x.Version, StringComparer.OrdinalIgnoreCase)
                .ToList();
                
        }
    }
}
