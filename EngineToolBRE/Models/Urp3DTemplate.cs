using System;
using System.IO;
using System.Threading.Tasks;

namespace EngineToolBRE.Models
{
    public sealed class Urp3DTemplate : TemplateBase
    {
        public Urp3DTemplate() : base(
            "URP 3D",
            "Installiert die URP, setzt 3D-Default, erzeugt URP-Asset und Startszene automatisch beim ersten Editor-Start."
        ) { }

        public override Task RunAsync(string _targetPath, UnityInstallation _unity, Action<string> _log)
        {
            var assets = Path.Combine(_targetPath, "Assets");
            var scripts = Path.Combine(assets, "Scripts");
            var materials = Path.Combine(assets, "Materials");
            var scenes = Path.Combine(assets, "Scenes");
            var editor = Path.Combine(assets, "Editor");
            var settings = Path.Combine(assets, "Settings");

            EnsureDir(assets);
            EnsureDir(scripts);
            EnsureDir(scenes);
            EnsureDir(editor);
            EnsureDir(materials);
            EnsureDir(settings);

            var autoSetup = Path.Combine(editor, "BRE_AutoUrpSetup3D.cs");
            File.WriteAllText(autoSetup, EDITOR_AUTO_SETUP_URP_3D);

            _log("URP 3D: Editor-Auto-Setup geschrieben. URP wird beim ersten Öffnen installiert und konfiguriert.");
            return Task.CompletedTask;
        }

        private const string EDITOR_AUTO_SETUP_URP_3D = @"using System;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class BRE_AutoUrpSetup3D
{
    private const string GuardKey = ""BRE_URP_3D_DONE"";
    private static AddRequest _addUrp;

    static BRE_AutoUrpSetup3D()
    {
        if (SessionState.GetBool(GuardKey, false)) return;
        SessionState.SetBool(GuardKey, true);

        // 3D-Default setzen
        EditorSettings.defaultBehaviorMode = EditorBehaviorMode.Mode3D;

        // URP hinzufügen (Version automatisch passend zu Unity)
        _addUrp = Client.Add(""com.unity.render-pipelines.universal"");
        EditorApplication.update += WaitForUrpInstall;
    }

    private static void WaitForUrpInstall()
    {
        if (_addUrp == null) { EditorApplication.update -= WaitForUrpInstall; return; }

        if (!_addUrp.IsCompleted) return;

        EditorApplication.update -= WaitForUrpInstall;

        if (_addUrp.Status == StatusCode.Failure)
        {
            Debug.LogError(""URP-Installation fehlgeschlagen: "" + _addUrp.Error.message);
            return;
        }

        // Domain-Reload abwarten, dann Asset & Szene erstellen
        EditorApplication.delayCall += CreateUrpAssetsAndScene;
    }

    private static void CreateUrpAssetsAndScene()
    {
        // Reflection-Typname des URP-Assets
        var urpAssetType = Type.GetType(""UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset, Unity.RenderPipelines.Universal.Runtime"");
        if (urpAssetType == null)
        {
            // evtl. noch nicht reimportiert -> später erneut probieren
            EditorApplication.delayCall += CreateUrpAssetsAndScene;
            return;
        }

        System.IO.Directory.CreateDirectory(""Assets/Settings"");

        // Pipeline Asset anlegen
        var asset = ScriptableObject.CreateInstance(urpAssetType) as RenderPipelineAsset;
        if (asset == null) { Debug.LogError(""Konnte URP Asset nicht erzeugen.""); return; }

        AssetDatabase.CreateAsset(asset, ""Assets/Settings/UniversalRenderPipelineAsset.asset"");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // GraphicsSettings setzen
        GraphicsSettings.renderPipelineAsset = asset;

        // Quality-Levels setzen
        GraphicsSettings.renderPipelineAsset = asset;

        // Szene erzeugen
        var newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Boden
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = ""Ground"";
        ground.transform.position = Vector3.zero;

        // Licht
        var lightGO = new GameObject(""Directional Light"");
        var light = lightGO.AddComponent<Light>();
        light.type = LightType.Directional;
        light.transform.rotation = Quaternion.Euler(50, -30, 0);

        // Kamera
        var camGO = new GameObject(""Main Camera"");
        camGO.tag = ""MainCamera"";
        var cam = camGO.AddComponent<Camera>();
        camGO.transform.position = new Vector3(0, 1.6f, -4f);
        camGO.transform.LookAt(Vector3.zero);

        System.IO.Directory.CreateDirectory(""Assets/Scenes"");
        string scenePath = ""Assets/Scenes/Main_URP3D.unity"";
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), scenePath);
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(""URP 3D Setup"", ""URP wurde installiert, Pipeline konfiguriert und 'Main_URP3D' erstellt."", ""OK"");
    }
}
";
    }
}
