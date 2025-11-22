using System;
using System.Threading.Tasks;
using System.IO;

namespace EngineToolBRE.Models
{
    public sealed class Urp2DTemplate : TemplateBase
    {
        public Urp2DTemplate() : base(
            "URP 2D (Empty)",
            "Installiert die URP, setzt 2D-Default, erzeugt URP-Asset und 2D-Startszene automatisch beim ersten Editor-Start."
        ) { }

        public override Task RunAsync(string _targetPath, UnityInstallation _unity, Action<string> _log)
        {
            var assets = Path.Combine(_targetPath, "Assets");
            var scripts = Path.Combine(assets, "Scripts");
            var scenes = Path.Combine(assets, "Scenes");
            var textures = Path.Combine(assets, "Textures");
            var resources = Path.Combine(assets, "Resources");
            var sounds = Path.Combine(assets, "Sounds");
            var animators = Path.Combine(assets, "Animators");
            var editor = Path.Combine(assets, "Editor");
            var settings = Path.Combine(assets, "Settings");

            EnsureDir(assets);
            EnsureDir(scripts);
            EnsureDir(scenes);
            EnsureDir(textures);
            EnsureDir(resources);
            EnsureDir(sounds);
            EnsureDir(animators);
            EnsureDir(editor);
            EnsureDir(settings);

            var autoSetup = Path.Combine(editor, "BRE_AutoUrpSetup2D.cs");
            File.WriteAllText(autoSetup, EDITOR_AUTO_SETUP_URP_2D);

            var wizardPath = Path.Combine(editor, "BRE_SetupWizard.cs");
            if (!File.Exists(wizardPath))
                File.WriteAllText(wizardPath, EDITOR_PROGRESS_WIZARD);


            _log("URP 2D: Editor-Auto-Setup geschrieben. URP wird beim ersten Öffnen installiert und konfiguriert.");
            return Task.CompletedTask;
        }

        private const string EDITOR_AUTO_SETUP_URP_2D = @"using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class BRE_AutoUrpSetup2D
{
    private const string FileGuardPath = ""ProjectSettings/BRE_URP_2D_DONE.flag"";
    private static AddRequest _addUrp, _add2dSprite, _add2dPixelPerfect;

    [MenuItem(""BRE/URP 2D/Run Setup Now"")]
    private static void Menu_RunSetupNow()
    {
        BRE_SetupWizard.Begin(""BRE Setup – URP 2D"");
        BRE_SetupWizard.Step(""Initialize"");
        BRE_SetupWizard.Log(""Manual trigger …"");
        TryRunSetup(true);
    }

    [MenuItem(""BRE/URP 2D/Reset Guard"")]
    private static void Menu_ResetGuard()
    {
        try { if (File.Exists(FileGuardPath)) File.Delete(FileGuardPath); } catch { }
        AssetDatabase.Refresh();
        BRE_SetupWizard.Begin(""BRE Setup – URP 2D"");
        BRE_SetupWizard.Log(""Guard reset done."");
        BRE_SetupWizard.Success(""Ready. Use 'Run Setup Now'."");
    }

    [InitializeOnLoadMethod]
    private static void Init()
    {
        if (!File.Exists(FileGuardPath))
        {
            BRE_SetupWizard.Begin(""BRE Setup – URP 2D"");
            BRE_SetupWizard.Step(""Initialize"");
            BRE_SetupWizard.Log(""Auto start …"");
        }
        TryRunSetup(false);
    }

    private static void TryRunSetup(bool force)
    {
        if (!force && File.Exists(FileGuardPath))
        {
            BRE_SetupWizard.Log(""Setup skipped (guard present)."");
            return;
        }

        EditorSettings.defaultBehaviorMode = EditorBehaviorMode.Mode2D;

        if (ManifestHasUrp())
        {
            BRE_SetupWizard.Step(""URP present"");
            BRE_SetupWizard.Log(""URP already installed."");
            EditorApplication.delayCall += CreateUrpAssetsAndScene;
        }
        else
        {
            BRE_SetupWizard.Step(""Install packages"");
            BRE_SetupWizard.Log(""Installing URP + 2D sprite + pixel perfect …"");
            _addUrp = Client.Add(""com.unity.render-pipelines.universal"");
            _add2dSprite = Client.Add(""com.unity.2d.sprite"");
            _add2dPixelPerfect = Client.Add(""com.unity.2d.pixel-perfect"");
            EditorApplication.update += WaitForPackages;
        }
    }

    private static bool ManifestHasUrp()
    {
        var manifest = ""Packages/manifest.json"";
        if (!File.Exists(manifest)) return false;
        return File.ReadAllText(manifest)
            .IndexOf(""com.unity.render-pipelines.universal"", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static void WaitForPackages()
    {
        if (_addUrp == null || _add2dSprite == null || _add2dPixelPerfect == null)
        {
            EditorApplication.update -= WaitForPackages;
            BRE_SetupWizard.Fail(""Package requests missing."");
            return;
        }
        if (!_addUrp.IsCompleted || !_add2dSprite.IsCompleted || !_add2dPixelPerfect.IsCompleted) return;

        EditorApplication.update -= WaitForPackages;
        if (_addUrp.Status == StatusCode.Failure)
        {
            BRE_SetupWizard.Fail(""URP installation failed: "" + _addUrp.Error.message);
            return;
        }

        BRE_SetupWizard.Step(""Packages installed"");
        BRE_SetupWizard.Progress(0.35f);
        EditorApplication.delayCall += CreateUrpAssetsAndScene;
    }

    private static void CreateUrpAssetsAndScene()
    {
        var urpAssetType = Type.GetType(""UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset, Unity.RenderPipelines.Universal.Runtime"");
        var rendererDataType = Type.GetType(""UnityEngine.Rendering.Universal.UniversalRendererData, Unity.RenderPipelines.Universal.Runtime"");
        if (urpAssetType == null || rendererDataType == null)
        {
            BRE_SetupWizard.Log(""URP types not ready yet – retry."");
            EditorApplication.delayCall += CreateUrpAssetsAndScene;
            return;
        }

        Directory.CreateDirectory(""Assets/Settings"");

        // URP Asset
        BRE_SetupWizard.Step(""Create URP asset"");
        var urpAssetPath = ""Assets/Settings/UniversalRenderPipelineAsset_2D.asset"";
        RenderPipelineAsset urpAssetObj = File.Exists(urpAssetPath)
            ? AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(urpAssetPath)
            : null;
        if (urpAssetObj == null)
        {
            urpAssetObj = ScriptableObject.CreateInstance(urpAssetType) as RenderPipelineAsset;
            AssetDatabase.CreateAsset(urpAssetObj, urpAssetPath);
        }
        BRE_SetupWizard.Progress(0.55f);

        // RendererData
        var rendererPath = ""Assets/Settings/UniversalRenderer_2D.asset"";
        UnityEngine.Object rendererDataObj = File.Exists(rendererPath)
            ? AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(rendererPath)
            : ScriptableObject.CreateInstance(rendererDataType);
        if (!File.Exists(rendererPath))
            AssetDatabase.CreateAsset(rendererDataObj, rendererPath);

        // Post-Processing aktivieren (ultra-robust)
        var rdSerialized = new SerializedObject(rendererDataObj);
        bool changed = false;
        var it = rdSerialized.GetIterator();
        bool enterChildren = true;
        while (it.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (it.propertyType != SerializedPropertyType.Boolean) continue;
            var n = it.name.ToLowerInvariant();
            bool looksLikePost = (n.Contains(""post"") && (n.Contains(""enable"") || n.Contains(""process"")));
            if (looksLikePost && !it.boolValue) { it.boolValue = true; changed = true; }
        }
        var ppDataProp = rdSerialized.FindProperty(""m_PostProcessData"") ?? rdSerialized.FindProperty(""postProcessData"");
        if (ppDataProp != null && ppDataProp.propertyType == SerializedPropertyType.ObjectReference && ppDataProp.objectReferenceValue == null)
        {
            var guids = AssetDatabase.FindAssets(""t:UnityEngine.Rendering.Universal.PostProcessData"");
            if (guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                ppDataProp.objectReferenceValue = obj; changed = true;
            }
        }
        if (changed)
        {
            rdSerialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(rendererDataObj);
            AssetDatabase.SaveAssets();
        }

        // Renderer ins URP-Asset eintragen
        var so = new SerializedObject(urpAssetObj);
        var listProp = so.FindProperty(""m_RendererDataList"");
        var idxProp = so.FindProperty(""m_DefaultRendererIndex"");
        if (listProp != null)
        {
            if (listProp.arraySize == 0) listProp.InsertArrayElementAtIndex(0);
            var elem = listProp.GetArrayElementAtIndex(0);
            if (elem.objectReferenceValue == null)
                elem.objectReferenceValue = rendererDataObj;
        }
        if (idxProp != null) idxProp.intValue = 0;
        so.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
        BRE_SetupWizard.Step(""Configure Renderer"");
        BRE_SetupWizard.Progress(0.7f);

        // Global aktivieren
        GraphicsSettings.renderPipelineAsset = urpAssetObj;
        BRE_SetupWizard.Log(""RenderPipeline assigned globally."");
        BRE_SetupWizard.Progress(0.8f);

        // Szene anlegen
        Directory.CreateDirectory(""Assets/Scenes"");
        var scenePath = ""Assets/Scenes/Main_URP2D.unity"";
        if (!File.Exists(scenePath))
        {
            BRE_SetupWizard.Step(""Create Scene"");
            var newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGO = new GameObject(""Main Camera"");
            camGO.tag = ""MainCamera"";
            var cam = camGO.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            camGO.transform.position = new Vector3(0, 0, -10f);

            var uacdType = Type.GetType(""UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime"");
            if (uacdType != null)
            {
                var uacd = camGO.GetComponent(uacdType) ?? camGO.AddComponent(uacdType);
                var prop = uacdType.GetProperty(""renderPostProcessing"");
                prop?.SetValue(uacd, true, null);
            }

            var ppcType = Type.GetType(""UnityEngine.U2D.PixelPerfectCamera, Unity.2D.PixelPerfect"");
            if (ppcType != null) camGO.AddComponent(ppcType);

            var grid = new GameObject(""Grid"");
            grid.AddComponent<UnityEngine.Grid>();

            var volumeGO = new GameObject(""Global Volume"");
            var volumeType = Type.GetType(""UnityEngine.Rendering.Volume, Unity.RenderPipelines.Core.Runtime"");
            if (volumeType != null)
            {
                var vol = volumeGO.AddComponent(volumeType);
                volumeType.GetProperty(""isGlobal"")?.SetValue(vol, true, null);
                var profileType = Type.GetType(""UnityEngine.Rendering.VolumeProfile, Unity.RenderPipelines.Core.Runtime"");
                if (profileType != null)
                {
                    var profile = ScriptableObject.CreateInstance(profileType);
                    var profilePath = ""Assets/Settings/GlobalVolumeProfile_2D.asset"";
                    AssetDatabase.CreateAsset(profile, profilePath);
                    AssetDatabase.SaveAssets();
                    volumeType.GetProperty(""profile"")?.SetValue(vol,
                        AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(profilePath), null);
                }
            }

            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), scenePath);
            BRE_SetupWizard.Progress(0.95f);
        }

        Directory.CreateDirectory(""ProjectSettings"");
        File.WriteAllText(FileGuardPath, ""done"");

        BRE_SetupWizard.Success(""URP 2D setup complete."");
        EditorUtility.DisplayDialog(""URP 2D Setup"", ""URP 2D ist konfiguriert. Szene 'Main_URP2D' wurde angelegt (falls nicht vorhanden). Post-Processing ist aktiviert."", ""OK"");
    }
}";

        private const string EDITOR_PROGRESS_WIZARD = @"using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class BRE_SetupWizard : EditorWindow
{
    private static BRE_SetupWizard _win;
    private static readonly List<WizardStep> _steps = new List<WizardStep>();
    private static readonly List<string> _log = new List<string>();
    private static string _title = ""BRE Setup"";
    private static float _progress = -1f;
    private static bool _failed;
    private static bool _done;
    private static double _spinnerStart;

    // ---------- Step-Klasse ----------
    private class WizardStep
    {
        public string Label;
        public StepState CurrentState;

        public WizardStep(string label)
        {
            Label = label;
            CurrentState = StepState.Pending;
        }
    }

    private enum StepState { Pending, Running, Done, Fail }
    // ---------------------------------

    // === Öffentliche API =====================================================
    public static void Begin(string title)
    {
        _title = string.IsNullOrEmpty(title) ? ""BRE Setup"" : title;
        _steps.Clear();
        _log.Clear();
        _progress = -1f;
        _failed = false;
        _done = false;
        EnsureWindow();
        _spinnerStart = EditorApplication.timeSinceStartup;
        _win.Repaint();
    }

    public static void Step(string label)
    {
        EnsureWindow();

        // Vorherigen Running-Step abschließen
        var running = _steps.FirstOrDefault(s => s.CurrentState == StepState.Running);
        if (running != null && !_done)
            running.CurrentState = StepState.Done;

        var s = _steps.FirstOrDefault(x => x.Label == label);
        if (s == null)
        {
            s = new WizardStep(label);
            _steps.Add(s);
        }

        s.CurrentState = StepState.Running;
        _win.Repaint();
    }

    public static void Progress(float normalized0to1)
    {
        EnsureWindow();
        _progress = Mathf.Clamp01(normalized0to1);
        _win.Repaint();
    }

    public static void Log(string message)
    {
        EnsureWindow();
        var line = $""[{DateTime.Now:HH:mm:ss}] {message}"";
        _log.Add(line);
        Debug.Log(""[BRE] "" + message);
        _win.Repaint();
    }

    public static void Success(string finalMessage = ""Setup complete."")
    {
        EnsureWindow();
        var running = _steps.FirstOrDefault(s => s.CurrentState == StepState.Running);
        if (running != null) running.CurrentState = StepState.Done;

        _done = true;
        _failed = false;
        _progress = 1f;
        Log(finalMessage);
        _win.Repaint();
    }

    public static void Fail(string errorMessage = ""Setup failed."")
    {
        EnsureWindow();
        var running = _steps.FirstOrDefault(s => s.CurrentState == StepState.Running);
        if (running != null) running.CurrentState = StepState.Fail;

        _failed = true;
        _done = false;
        Log(errorMessage);
        _win.Repaint();
    }

    public static void CloseIfOpen()
    {
        if (_win != null)
        {
            _win.Close();
            _win = null;
        }
    }
    // ========================================================================

    [MenuItem(""BRE/Setup Wizard/Open"")]
    public static void OpenMenu() => EnsureWindow();

    private static void EnsureWindow()
    {
        if (_win == null)
        {
            _win = GetWindow<BRE_SetupWizard>();
            _win.titleContent = new GUIContent(""BRE Setup"");
            _win.minSize = new Vector2(440, 360);
        }
        _win.titleContent = new GUIContent(_title);
    }

    private void OnEnable()
    {
        _spinnerStart = EditorApplication.timeSinceStartup;
        EditorApplication.update -= AutoRepaint;
        EditorApplication.update += AutoRepaint;
    }

    private void OnDisable()
    {
        EditorApplication.update -= AutoRepaint;
    }

    private void AutoRepaint() => Repaint();

    private void OnGUI()
    {
        var headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 };
        GUILayout.Space(6);
        GUILayout.Label(_title, headerStyle);

        DrawProgress();
        GUILayout.Space(8);
        DrawSteps();
        GUILayout.Space(8);
        DrawLog();

        GUILayout.FlexibleSpace();

        using (new GUILayout.HorizontalScope())
        {
            if (GUILayout.Button(""Copy Log""))
            {
                EditorGUIUtility.systemCopyBuffer = string.Join(""\n"", _log);
            }

            GUILayout.FlexibleSpace();

            if (!_done && !_failed)
            {
                if (GUILayout.Button(""Run Again""))
                {
                    Log(""Use 'Run Setup Now' in BRE menu to re-trigger."");
                }
            }

            if ((_done || _failed) && GUILayout.Button(""Close""))
            {
                CloseIfOpen();
            }
        }

        GUILayout.Space(6);
    }

    private void DrawProgress()
    {
        var rect = GUILayoutUtility.GetRect(1, 16);
        EditorGUI.ProgressBar(rect, _progress < 0 ? 0 : _progress, ProgressLabel());
    }

    private string ProgressLabel()
    {
        if (_failed) return ""Failed"";
        if (_done) return ""Done"";
        if (_progress < 0) return ""Working… "" + Spinner();
        return $""{Mathf.RoundToInt(_progress * 100f)}%"";
    }

    private string Spinner()
    {
        string[] frames = { ""⠋"",""⠙"",""⠹"",""⠸"",""⠼"",""⠴"",""⠦"",""⠧"",""⠇"",""⠏"" };
        var idx = (int)((EditorApplication.timeSinceStartup - _spinnerStart) * 12) % frames.Length;
        return frames[idx];
    }

    private void DrawSteps()
    {
        GUILayout.Label(""Steps"", EditorStyles.boldLabel);
        if (_steps.Count == 0)
        {
            GUILayout.Label(""No steps yet."", EditorStyles.miniLabel);
            return;
        }

        var box = new GUIStyle(""HelpBox"");
        foreach (var s in _steps)
        {
            using (new GUILayout.HorizontalScope(box))
            {
                GUILayout.Label(StateIcon(s.CurrentState), GUILayout.Width(22));
                GUILayout.Label(s.Label, EditorStyles.label);
            }
        }
    }

    private GUIContent StateIcon(StepState st)
    {
        switch (st)
        {
            case StepState.Pending: return new GUIContent(""•"", ""Pending"");
            case StepState.Running: return new GUIContent(""▶"", ""Running"");
            case StepState.Done:    return new GUIContent(""✓"", ""Done"");
            case StepState.Fail:    return new GUIContent(""✖"", ""Failed"");
        }
        return new GUIContent(""•"");
    }

    private void DrawLog()
    {
        GUILayout.Label(""Log"", EditorStyles.boldLabel);
        var scrollRect = GUILayoutUtility.GetRect(1, 160);
        GUI.Box(scrollRect, GUIContent.none);
        var inner = new RectOffset(8, 8, 4, 4).Remove(scrollRect);

        GUILayout.BeginArea(inner);
        foreach (var line in _log.TakeLast(500))
            GUILayout.Label(line, EditorStyles.miniLabel);
        GUILayout.EndArea();
    }
}
"; 
    }
}
