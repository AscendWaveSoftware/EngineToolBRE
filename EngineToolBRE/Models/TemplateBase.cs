using System.IO;
using System.Threading.Tasks;

namespace EngineToolBRE.Models
{
    public abstract class TemplateBase
    {
        public string Name { get; }
        public string Description { get; }

        protected TemplateBase(string _name, string _description)
        {
            Name = _name;
            Description = _description;
        }

        /// <summary>
        /// Führt das Template aus. TargetPath ist der Projekt-Root (enthält /Assets).
        /// </summary>
        public abstract Task RunAsync(string _targetPath, UnityInstallation _unity, System.Action<string> _log);

        protected static void EnsureDir(string _path)
        {
            if (!Directory.Exists(_path))
                Directory.CreateDirectory(_path);
        }
    }
}
