using System;
using System.Windows;
using EngineToolBRE.ViewModels;

namespace EngineToolBRE
{
    /// <summary>
    /// Interaktionslogik für "App.xaml"
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            var wnd = new MainWindow
            {
                DataContext = new MainViewModel()
            };
            wnd.Show();
        }
    }
}
