using System;
using System.Threading.Tasks;
using System.IO;

namespace EngineToolBRE.Models
{
    public sealed class Urp2DTemplate : TemplateBase
    {
        public Urp2DTemplate() : base(
            "URP 2D",
            "Installiert die URP, setzt 2D-Default, erzeugt URP-Asset und 2D-Startszene automatisch beim ersten Editor-Start."
        ) { }

        public override Task RunAsync(string _targetPath, UnityInstallation _unity, Action<string> _log)
        {
            var assets = Path.Combine(_targetPath, "Assets");
            var scripts = Path.Combine(assets, "Scripts");
            var scenes = Path.Combine(assets, "Scenes");
            var editor = Path.Combine(assets, "Editor");
            var settings = Path.Combine(assets, "Settings");

            EnsureDir(assets);
            EnsureDir(scripts);
            EnsureDir(scenes);
            EnsureDir(editor);
            EnsureDir(settings);

            var autoSetup = Path.Combine(editor, "BRE_AutoUrpSetup2D.cs");
            File.WriteAllText(autoSetup, EDITOR_AUTO_SETUP_URP_2D);

            _log("URP 2D: Editor-Auto-Setup geschrieben. URP wird beim ersten Öffnen installiert und konfiguriert.");
            return Task.CompletedTask;
        }

        private const string EDITOR_AUTO_SETUP_URP_2D = @"using System;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class BRE_AutoUrpSetup2D
{
    private const string GuardKey = ""BRE_URP_2D_DONE"";
    private static AddRequest _addUrp;
    private static AddRequest _add2dSprite;
    private static AddRequest _add2dPpu;

    static BRE_AutoUrpSetup2D()
    {
        if (SessionState.GetBool(GuardKey, false)) return;
        SessionState.SetBool(GuardKey, true);

        // 2D-Default
        EditorSettings.defaultBehaviorMode = EditorBehaviorMode.Mode2D;

        // Pakete hinzufügen
        _addUrp = Client.Add(""com.unity.render-pipelines.universal"");
        _add2dSprite = Client.Add(""com.unity.2d.sprite"");
        _add2dPpu = Client.Add(""com.unity.2d.pixel-perfect"");

        EditorApplication.update += WaitForPackages;
    }

    private static void WaitForPackages()
    {
        if (!IsDone(_addUrp) || !IsDone(_add2dSprite) || !IsDone(_add2dPpu)) return;

        EditorApplication.update -= WaitForPackages;

        if (HasFailure(_addUrp) || HasFailure(_add2dSprite) || HasFailure(_add2dPpu))
        {
            Debug.LogError(""Paketinstallation fehlgeschlagen. Prüfe Console-Log."");
            return;
        }

        EditorApplication.delayCall += CreateUrpAssetsAndScene;
    }

    private static bool IsDone(AddRequest r) => r != null && r.IsCompleted;
    private static bool HasFailure(AddRequest r) => r.Status == StatusCode.Failure;

    private static void CreateUrpAssetsAndScene()
    {
        var urpAssetType = Type.GetType(""UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset, Unity.RenderPipelines.Universal.Runtime"");
        if (urpAssetType == null)
        {
            EditorApplication.delayCall += CreateUrpAssetsAndScene;
            return;
        }

        System.IO.Directory.CreateDirectory(""Assets/Settings"");

        var asset = ScriptableObject.CreateInstance(urpAssetType) as RenderPipelineAsset;
        if (asset == null) { Debug.LogError(""Konnte URP Asset nicht erzeugen.""); return; }

        AssetDatabase.CreateAsset(asset, ""Assets/Settings/UniversalRenderPipelineAsset_2D.asset"");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        GraphicsSettings.renderPipelineAsset = asset;

        // 2D-Szene erzeugen (Orthographic Camera)
        var newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camGO = new GameObject(""Main Camera"");
        camGO.tag = ""MainCamera"";
        var cam = camGO.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 5f;
        camGO.transform.position = new Vector3(0, 0, -10f);

        // 2D Grid als Beispiel
        var grid = new GameObject(""Grid"");
        grid.AddComponent<UnityEngine.Grid>();

        System.IO.Directory.CreateDirectory(""Assets/Scenes"");
        string scenePath = ""Assets/Scenes/Main_URP2D.unity"";
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), scenePath);
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(""URP 2D Setup"", ""URP + 2D-Pakete wurden installiert, Pipeline konfiguriert und 'Main_URP2D' erstellt."", ""OK"");
    }
}";
    }
}
