using System.Windows.Forms;

namespace EngineToolBRE.Infrastructure
{
    public static class DialogService
    {
        public static string PickFolder(string _initial = "")
        {
            using (var dlg = new FolderBrowserDialog())
            {
                if (!string.IsNullOrEmpty(_initial))
                    dlg.SelectedPath = _initial;
                var result = dlg.ShowDialog();
                return result == DialogResult.OK ? dlg.SelectedPath : null;
            }
        }
    }
}
