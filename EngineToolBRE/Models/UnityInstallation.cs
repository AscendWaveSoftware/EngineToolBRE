namespace EngineToolBRE.Models
{
    public class UnityInstallation
    {
        public string Version { get; set; }
        public string EditorPath { get; set; }
        public string DisplayString => $"{Version} ({EditorPath})";
    }
}
