using EngineToolBRE.Models;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace EngineToolBRE.Services
{
    public static class ProjectCreator
    {
        public static async Task CreateProjectAsync(string _baseDir, string _projectName, UnityInstallation _unity, TemplateBase _template, Action<string> _log)
        {
            if (string.IsNullOrWhiteSpace(_baseDir)) throw new ArgumentException("Kein Zielordner.");
            if (string.IsNullOrWhiteSpace(_projectName)) throw new ArgumentException("Kein Projektname.");
            if (_unity == null) throw new ArgumentException("Keine Unity-Version gewählt.");
            if (_template == null) throw new ArgumentException("Kein Template gewählt.");

            var projectRoot = Path.Combine(_baseDir, _projectName);
            if (!Directory.Exists(projectRoot))
                Directory.CreateDirectory(projectRoot);

            var assets = Path.Combine(projectRoot, "Assets");
            if (!Directory.Exists(assets))
                Directory.CreateDirectory(assets);

            EnsureUnityProjectMetadata(projectRoot, _unity.Version, _log);

            _log($"Projektordner erstellt {projectRoot}");

            await _template.RunAsync(projectRoot, _unity, _log);

            _log("Projektstruktur erstellt.");

            await LaunchUnityOnceAsync(_unity.EditorPath, projectRoot, _log);
        }

        private static void EnsureUnityProjectMetadata(string _projectRoot, string _editorVersion, Action<string> _log)
        {
            var projectSettingsDir = Path.Combine(_projectRoot, "ProjectSettings");
            var packagesDir = Path.Combine(_projectRoot, "Packages");
            if (!Directory.Exists(projectSettingsDir)) Directory.CreateDirectory(projectSettingsDir);
            if (!Directory.Exists(packagesDir)) Directory.CreateDirectory(packagesDir);

            var versionTxt = Path.Combine(projectSettingsDir, "ProjectVersion.txt");
            if (!File.Exists(versionTxt))
            {
                var content = $"m_EditorVersion: {_editorVersion}\n";
                File.WriteAllText(versionTxt ,content);
                _log($"ProjectVersion.txt geschrieben: {_editorVersion}");
            }

            var manifestPath = Path.Combine(packagesDir, "manifest.json");
            if (!File.Exists(manifestPath))
            {
                var manifest = "{\n  \"dependencies\": {}\n}\n";
                File.WriteAllText(manifestPath, manifest);
                _log("Leeres Packages/manifest.json angelegt.");
            }
        }

        private static Task LaunchUnityOnceAsync(string _unityExe, string _projectPath, Action<string> _log)
        {
            var tcs = new TaskCompletionSource<int>();

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = _unityExe,
                    Arguments = $"-batchmode -quit -projectPath \"{_projectPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                var p = new Process();
                p.StartInfo = psi;
                p.EnableRaisingEvents = true;

                p.Exited += (sender, args) =>
                {
                    try
                    {
                        _log($"Unity beendet (ExitCode {p.ExitCode}).");
                        p.Dispose();
                        tcs.TrySetResult(p.ExitCode);
                    }
                    catch
                    {
                        tcs.TrySetResult(-1);
                    }
                };

                _log("Unity (Batchmode) gestartet, um Projekt zu initialisieren...");
                p.Start();
            }
            catch (Exception ex)
            {
                _log($"Unity-Start übersprungen/fehlgeschlagen: {ex.Message}");
                tcs.TrySetResult(-1);
            }

            return tcs.Task;
        }
    }
}
