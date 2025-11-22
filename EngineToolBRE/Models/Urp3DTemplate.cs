using System;
using System.IO;
using System.Threading.Tasks;

namespace EngineToolBRE.Models
{
    public sealed class Urp3DTemplate : TemplateBase
    {
        public Urp3DTemplate() : base(
            "URP 3D (Empty)",
            "Installiert die URP, setzt 3D-Default, erzeugt URP-Asset und Startszene automatisch beim ersten Editor-Start."
        ) { }

        public override Task RunAsync(string _targetPath, UnityInstallation _unity, Action<string> _log)
        {
            var assets = Path.Combine(_targetPath, "Assets");
            var scripts = Path.Combine(assets, "Scripts");
            var prefabs = Path.Combine(assets, "Prefabs");
            var materials = Path.Combine(assets, "Materials");
            var textures = Path.Combine(assets, "Textures");
            var resources = Path.Combine(assets, "Resources");
            var sounds = Path.Combine(assets, "Sounds");
            var animators = Path.Combine(assets, "Animators");
            var scenes = Path.Combine(assets, "Scenes");
            var editor = Path.Combine(assets, "Editor");
            var settings = Path.Combine(assets, "Settings");

            EnsureDir(assets);
            EnsureDir(scripts);
            EnsureDir(prefabs);
            EnsureDir(scenes);
            EnsureDir(editor);
            EnsureDir(materials);
            EnsureDir(textures);
            EnsureDir(sounds);
            EnsureDir(resources);
            EnsureDir(animators);
            EnsureDir(settings);

            var autoSetup = Path.Combine(editor, "BRE_AutoUrpSetup3D.cs");
            File.WriteAllText(autoSetup, EDITOR_AUTO_SETUP_URP_3D);

            // Setup Wizard
            var wizardPath = Path.Combine(editor, "BRE_SetupWizard.cs");
            if(!File.Exists(wizardPath))
            File.WriteAllText(wizardPath, EDITOR_PROGRESS_WIZARD);

            _log("URP 3D: Editor-Auto-Setup geschrieben. URP wird beim ersten Öffnen installiert und konfiguriert.");
            return Task.CompletedTask;
        }

        private const string EDITOR_AUTO_SETUP_URP_3D = @"using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class BRE_AutoUrpSetup3D
{
    private const string FileGuardPath = ""ProjectSettings/BRE_URP_3D_DONE.flag"";

    private static AddRequest _addUrp;

    // ===== NEW: Manuelle Steuerung über Menü =====
    [MenuItem(""BRE/URP 3D/Run Setup Now"")]
    private static void Menu_RunSetupNow()
    {
        Debug.Log(""[BRE] Menu: Run Setup Now"");
        TryRunSetup(force: true);
    }

    [MenuItem(""BRE/URP 3D/Reset Guard"")]
    private static void Menu_ResetGuard()
    {
        Debug.Log(""[BRE] Menu: Reset Guard"");
        try { if (File.Exists(FileGuardPath)) File.Delete(FileGuardPath); } catch { }
        AssetDatabase.Refresh();
        Debug.Log(""[BRE] Guard reset done. You can run 'BRE/URP 3D/Run Setup Now'."");
    }

    [MenuItem(""BRE/URP 3D/Diagnostics"")]
    private static void Menu_Diagnostics()
    {
        var flag = File.Exists(FileGuardPath);
        Debug.Log($""[BRE] Diagnostics | GuardFlag:{flag} ManifestHasURP:{ManifestHasUrp()}"");
    }
    // ============================================

    [InitializeOnLoadMethod]
    private static void Init()
    {
        // Beim Laden automatisch versuchen (nur wenn nicht schon erledigt)
        TryRunSetup(force: false);
    }

    private static void TryRunSetup(bool force)
    {
        if (!force && File.Exists(FileGuardPath))
        {
            Debug.Log(""[BRE] Setup skipped (guard present)."");
            return;
        }

        BRE_SetupWizard.Begin(""BRE Setup – URP 3D"");
        BRE_SetupWizard.Step(""Initialize"");
        BRE_SetupWizard.Log(""Init 3D setup …"");


        EditorSettings.defaultBehaviorMode = EditorBehaviorMode.Mode3D;
        Debug.Log(""[BRE] URP 3D Setup init …"");

        if (IsUrpInstalled())
        {
            Debug.Log(""[BRE] URP already in manifest. Scheduling CreateUrpAssetsAndScene …"");
            BRE_SetupWizard.Step(""URP present"");
            BRE_SetupWizard.Log(""URP found in manifest.json"");

            EditorApplication.delayCall += CreateUrpAssetsAndScene;
        }
        else
        {
            Debug.Log(""[BRE] Installing URP via UPM …"");
            BRE_SetupWizard.Step(""Install URP"");
            BRE_SetupWizard.Log(""Installing com.unity.render-pipelines.universal via UPM …"");

            _addUrp = Client.Add(""com.unity.render-pipelines.universal"");
            EditorApplication.update += WaitForUrpInstall;
        }
    }

    private static bool ManifestHasUrp()
    {
        var manifest = ""Packages/manifest.json"";
        if (!File.Exists(manifest)) return false;
        var txt = File.ReadAllText(manifest);
        return txt.IndexOf(""com.unity.render-pipelines.universal"", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsUrpInstalled() => ManifestHasUrp();

    private static void WaitForUrpInstall()
    {
        if (_addUrp == null) { EditorApplication.update -= WaitForUrpInstall; return; }
        if (!_addUrp.IsCompleted) return;

        EditorApplication.update -= WaitForUrpInstall;

        if (_addUrp.Status == StatusCode.Failure)
        {
            Debug.LogError(""[BRE] URP install failed: "" + _addUrp.Error.message);
            BRE_SetupWizard.Fail(""URP installation failed: "" + _addUrp.Error.message);
            EditorUtility.DisplayDialog(""URP 3D Setup"", ""URP-Installation fehlgeschlagen.\nSiehe Console."", ""OK"");
            return;
        }

        Debug.Log(""[BRE] URP installed. Scheduling CreateUrpAssetsAndScene …"");
        BRE_SetupWizard.Step(""URP installed"");
        BRE_SetupWizard.Log(""URP installed. Continue with asset creation."");
        BRE_SetupWizard.Progress(0.35f);

        EditorApplication.delayCall += CreateUrpAssetsAndScene;
    }

    private static void CreateUrpAssetsAndScene()
    {
        // Sicherstellen, dass URP-Typen verfügbar sind
        var urpAssetType = Type.GetType(
            ""UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset, Unity.RenderPipelines.Universal.Runtime"");
        var rendererDataType = Type.GetType(
            ""UnityEngine.Rendering.Universal.UniversalRendererData, Unity.RenderPipelines.Universal.Runtime"");

        if (urpAssetType == null || rendererDataType == null)
        {
            Debug.Log(""[BRE] URP types not ready yet, re-schedule …"");
            EditorApplication.delayCall += CreateUrpAssetsAndScene;
            return;
        }

        Directory.CreateDirectory(""Assets/Settings"");

        // URP Asset
        var urpAssetPath = ""Assets/Settings/UniversalRenderPipelineAsset.asset"";
        RenderPipelineAsset urpAssetObj = null;
        if (File.Exists(urpAssetPath))
            urpAssetObj = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(urpAssetPath);
        if (urpAssetObj == null)
        {
            urpAssetObj = ScriptableObject.CreateInstance(urpAssetType) as RenderPipelineAsset;
            AssetDatabase.CreateAsset(urpAssetObj, urpAssetPath);
            AssetDatabase.SaveAssets();
        }

        // RendererData
        var rendererPath = ""Assets/Settings/UniversalRenderer.asset"";
        UnityEngine.Object rendererDataObj = null;
        if (File.Exists(rendererPath))
            rendererDataObj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(rendererPath);
        if (rendererDataObj == null)
        {
            rendererDataObj = ScriptableObject.CreateInstance(rendererDataType);
            AssetDatabase.CreateAsset(rendererDataObj, rendererPath);
            AssetDatabase.SaveAssets();
        }

        // --- Post-Processing im Renderer aktivieren (falls Feld existiert) ---
        var rdSerialized = new SerializedObject(rendererDataObj);

// 1) Alle Bool-Properties iterieren und jene aktivieren, deren Name nach ""post processing enabled"" aussieht
bool changed = false;
var it = rdSerialized.GetIterator();
bool enterChildren = true;
while (it.NextVisible(enterChildren))
{
    enterChildren = false;

    if (it.propertyType != SerializedPropertyType.Boolean) continue;

    // Property-Namen matchen (lowercase)
    var n = it.name.ToLowerInvariant();

    // typische Varianten: m_PostProcessingEnabled, m_PostProcessEnabled, postProcessEnabled, enablePostProcess, etc.
    // generische Heuristik: enthält ""post"" UND (""enable"" ODER ""processing"")
    bool looksLikePostToggle =
        (n.Contains(""post"") && (n.Contains(""enable"") || n.Contains(""process"")));

    if (looksLikePostToggle && !it.boolValue)
    {
        it.boolValue = true;
        changed = true;
    }
}

// 2) (Optional) PostProcessData zuweisen, falls das Feld existiert und leer ist
//    Manche URP-Versionen speichern referenzierte Shader/Materialien in einem PostProcessData-Objekt.
var ppDataProp =
    rdSerialized.FindProperty(""m_PostProcessData"") ??
    rdSerialized.FindProperty(""postProcessData"");

if (ppDataProp != null && ppDataProp.propertyType == SerializedPropertyType.ObjectReference && ppDataProp.objectReferenceValue == null)
{
    // Versuche, ein vorhandenes PostProcessData-Asset im Projekt zu finden
    var guids = AssetDatabase.FindAssets(""t:UnityEngine.Rendering.Universal.PostProcessData"");
    if (guids != null && guids.Length > 0)
    {
        var path = AssetDatabase.GUIDToAssetPath(guids[0]);
        var ppDataObj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
        if (ppDataObj != null)
        {
            ppDataProp.objectReferenceValue = ppDataObj;
            changed = true;
        }
    }
    // Wenn keins gefunden wird, lassen wir es leer – der Renderer nutzt ggf. interne Defaults.
}

if (changed)
{
    rdSerialized.ApplyModifiedPropertiesWithoutUndo();
    EditorUtility.SetDirty(rendererDataObj);
    AssetDatabase.SaveAssets();
    AssetDatabase.Refresh();
}

        // Renderer ins URP-Asset eintragen
        var so = new SerializedObject(urpAssetObj);
        var listProp = so.FindProperty(""m_RendererDataList"");     // array<Object>
        var idxProp  = so.FindProperty(""m_DefaultRendererIndex""); // int

        if (listProp != null)
        {
            if (listProp.arraySize == 0)
                listProp.InsertArrayElementAtIndex(0);

            var elem = listProp.GetArrayElementAtIndex(0);
            if (elem.objectReferenceValue == null)
                elem.objectReferenceValue = rendererDataObj;
        }

        if (idxProp != null)
            idxProp.intValue = 0;

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(urpAssetObj);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        BRE_SetupWizard.Step(""Configure Renderer"");
        BRE_SetupWizard.Log(""Assigned RendererData, enabled Post-Processing."");
        BRE_SetupWizard.Progress(0.7f);


        // Global aktivieren
        GraphicsSettings.renderPipelineAsset = urpAssetObj;

        BRE_SetupWizard.Log(""RenderPipeline assigned globally."");
        BRE_SetupWizard.Progress(0.8f);


        // Szene nur einmal anlegen
        Directory.CreateDirectory(""Assets/Scenes"");
        var scenePath = ""Assets/Scenes/Main_URP3D.unity"";
        if (!File.Exists(scenePath))
        {
            var newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = ""Ground"";
            ground.transform.position = Vector3.zero;

            var lightGO = new GameObject(""Directional Light"");
            var light = lightGO.AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(50, -30, 0);

            var camGO = new GameObject(""Main Camera"");
            camGO.tag = ""MainCamera"";
            var cam = camGO.AddComponent<Camera>();
            camGO.transform.position = new Vector3(0, 1.6f, -4f);
            camGO.transform.LookAt(Vector3.zero);

            // --- UniversalAdditionalCameraData hinzufügen & Post-Processing aktivieren ---
            var uacdType = Type.GetType(""UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime"");
            if (uacdType != null)
            {
                var uacd = camGO.GetComponent(uacdType) ?? camGO.AddComponent(uacdType);
                var ppProp = uacdType.GetProperty(""renderPostProcessing"");
                if (ppProp != null && ppProp.CanWrite) ppProp.SetValue(uacd, true, null);
            }

            // --- Optional: Globales Volume anlegen (leer, isGlobal = true) ---
            var volumeGO = new GameObject(""Global Volume"");
            var volumeType = Type.GetType(""UnityEngine.Rendering.Volume, Unity.RenderPipelines.Core.Runtime"");
            if (volumeType != null)
            {
                var volume = volumeGO.AddComponent(volumeType);
                var isGlobalProp = volumeType.GetProperty(""isGlobal"");
                if (isGlobalProp != null && isGlobalProp.CanWrite) isGlobalProp.SetValue(volume, true, null);

                var profileType = Type.GetType(""UnityEngine.Rendering.VolumeProfile, Unity.RenderPipelines.Core.Runtime"");
                if (profileType != null)
                {
                    var profile = ScriptableObject.CreateInstance(profileType);
                    var profilePath = ""Assets/Settings/GlobalVolumeProfile.asset"";
                    AssetDatabase.CreateAsset(profile, profilePath);
                    AssetDatabase.SaveAssets();

                    var profileProp = volumeType.GetProperty(""profile"");
                    if (profileProp != null && profileProp.CanWrite)
                        profileProp.SetValue(volume, AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(profilePath), null);
                }
            }

            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), scenePath);
            BRE_SetupWizard.Step(""Create Scene"");
            BRE_SetupWizard.Log(""Scene ready."");
            BRE_SetupWizard.Progress(0.95f);

        }

        // Guards setzen (dauerhaft)
        try
        {
            Directory.CreateDirectory(""ProjectSettings"");
            File.WriteAllText(FileGuardPath, ""done"");
        }
        catch { }

        Debug.Log(""[BRE] URP 3D Setup completed (Renderer PP enabled, Camera PP enabled)."");
        BRE_SetupWizard.Success(""URP 3D setup complete."");

        EditorUtility.DisplayDialog(""URP 3D Setup"", ""URP ist konfiguriert. Szene 'Main_URP3D' wurde angelegt (falls nicht vorhanden). Post-Processing ist aktiviert."", ""OK"");
    }
}
";


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
