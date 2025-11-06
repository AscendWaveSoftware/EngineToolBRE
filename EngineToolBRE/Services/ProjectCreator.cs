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

            _log($"Projektordner erstellt {projectRoot}");

            await _template.RunAsync(projectRoot, _unity, _log);

            _log("Projektstruktur erstellt.");

            await LaunchUnityOnceAsync(_unity.EditorPath, projectRoot, _log);
        }

        private static Task LaunchUnityOnceAsync(string _unityExe, string _projectPath, Action<string> _log)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = _unityExe,
                    Arguments = $"-batchmode -quit -projectPath \"{_projectPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                var p = Process.Start(psi);
                _log($"Unity (Batchmode) gestartet, um Projekt zu initialisieren...");
                p.WaitForExit();
                _log($"Unity beendet (ExitCode {p.ExitCode}).");
            }
            catch (Exception ex)
            {
                _log($"Unity-Start übersprungen/fehlgeschlagen: {ex.Message}");
            }
            return Task.CompletedTask;
        }
    }
}
