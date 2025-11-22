using System;
using System.IO;
using System.Threading.Tasks;

namespace EngineToolBRE.Models
{
    public sealed class EmptyProjectTemplate : TemplateBase
    {
        public EmptyProjectTemplate() : base(
            "Empty Project (Folders only)",
            "Lege Standardordner an: Assets/Scripts, Assets/Scenes, Assets/Prefabs, Assets/Materials"
        ) { }

        public override Task RunAsync(string _targetPath, UnityInstallation _unity, Action<string> _log)
        {
            var assets = Path.Combine(_targetPath, "Assets");
            EnsureDir(assets);
            EnsureDir(Path.Combine(assets, "Scripts"));
            EnsureDir(Path.Combine(assets, "Scenes"));
            EnsureDir(Path.Combine(assets, "Prefabs"));
            EnsureDir(Path.Combine(assets, "Materials"));
            //TODO: Weitere Standard-Ordner anlegen
            _log("Standard-Ordner erstellt.");
            return Task.CompletedTask;
        }
    }
}
