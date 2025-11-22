using EngineToolBRE.Infrastructure;
using EngineToolBRE.Models;
using EngineToolBRE.Services;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;

namespace EngineToolBRE.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<UnityInstallation> UnityInstallations { get; } = new ObservableCollection<UnityInstallation>();
        public ObservableCollection<TemplateBase> Templates { get; } = new ObservableCollection<TemplateBase>();

        private UnityInstallation _selectedUnity;
        public UnityInstallation SelectedUnity { get => _selectedUnity; set { _selectedUnity = value; OnPropertyChanged(); UpdateCanExec(); } }

        private TemplateBase _selectedTemplate;
        public TemplateBase SelectedTemplate { get => _selectedTemplate; set { _selectedTemplate = value; OnPropertyChanged(); UpdateCanExec(); } }

        private string _projectName = "MyUnityProject";
        public string ProjectName { get => _projectName; set { _projectName = value; OnPropertyChanged(); UpdateCanExec(); } }

        private string _targetDirectory;
        public string TargetDirectory { get => _targetDirectory; set { _targetDirectory = value; OnPropertyChanged(); UpdateCanExec(); } }

        private string _logText = "";
        public string LogText { get => _logText; set { _logText = value; OnPropertyChanged(); } }

        public RelayCommand RescanCommand { get; }
        public RelayCommand BrowseFolderCommand { get; }
        public RelayCommand CreateCommand { get; }
        public RelayCommand ExitCommand { get; }

        public MainViewModel()
        {
            // Templates registrieren
            Templates.Add(new EmptyProjectTemplate());
            Templates.Add(new Urp3DTemplate());
            Templates.Add(new Urp2DTemplate());
            Templates.Add(new FirstPersonShooterTemplate());
            //TODO: Weitere Templates hier einfügen

            RescanCommand = new RelayCommand(Rescan);
            BrowseFolderCommand = new RelayCommand(BrowseFolder);
            CreateCommand = new RelayCommand(async () => await CreateAsync(), CanCreate);
            ExitCommand = new RelayCommand(() => Application.Current.Shutdown());

            // Defaults
            TargetDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

            // Initialer Scan
            Rescan();
        }

        private void Rescan()
        {
            UnityInstallations.Clear();
            foreach (var u in UnityScanner.Scan())
                UnityInstallations.Add(u);

            if (UnityInstallations.Count > 0 && SelectedUnity == null)
                SelectedUnity = UnityInstallations[0];

            Append("Unity-Installationen aktualisiert.");
        }

        private void BrowseFolder()
        {
            var picked = DialogService.PickFolder(TargetDirectory);
            if (!string.IsNullOrEmpty(picked)) TargetDirectory = picked;
        }

        private bool CanCreate()
        {
            return SelectedUnity != null
                && SelectedTemplate != null
                && !string.IsNullOrWhiteSpace(ProjectName)
                && !string.IsNullOrWhiteSpace(TargetDirectory);
        }

        private async Task CreateAsync()
        {
            try
            {
                Append("Starte Erstellung…");
                await ProjectCreator.CreateProjectAsync(TargetDirectory, ProjectName, SelectedUnity, SelectedTemplate, Append);
                Append("Fertig. Du kannst das Projekt nun in Unity öffnen.");
            }
            catch (Exception ex)
            {
                Append("FEHLER: " + ex.Message);
            }
        }

        private void Append(string msg)
        {
            LogText += $"[{DateTime.Now:HH:mm:ss}] {msg}{Environment.NewLine}";
        }

        private void UpdateCanExec() => CreateCommand?.RaiseCanExecuteChanged();

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
