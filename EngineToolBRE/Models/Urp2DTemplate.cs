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

    private static AddRequest _addUrp;
    private static AddRequest _add2dSprite;
    private static AddRequest _add2dPixelPerfect;

    // ===== Menü: manuell steuern =====
    [MenuItem(""BRE/URP 2D/Run Setup Now"")]
    private static void Menu_RunSetupNow()
    {
        Debug.Log(""[BRE] 2D: Run Setup Now"");
        TryRunSetup(force: true);
    }

    [MenuItem(""BRE/URP 2D/Reset Guard"")]
    private static void Menu_ResetGuard()
    {
        Debug.Log(""[BRE] 2D: Reset Guard"");
        try { if (File.Exists(FileGuardPath)) File.Delete(FileGuardPath); } catch { }
        AssetDatabase.Refresh();
        Debug.Log(""[BRE] 2D: Guard reset done. You can run 'BRE/URP 2D/Run Setup Now'."");
    }

    [MenuItem(""BRE/URP 2D/Diagnostics"")]
    private static void Menu_Diagnostics()
    {
        var flag = File.Exists(FileGuardPath);
        Debug.Log($""[BRE] 2D: Diagnostics | GuardFlag:{flag} ManifestHasURP:{ManifestHasUrp()}"");
    }
    // =================================

    [InitializeOnLoadMethod]
    private static void Init()
    {
        TryRunSetup(force: false);
    }

    private static void TryRunSetup(bool force)
    {
        if (!force && File.Exists(FileGuardPath))
        {
            Debug.Log(""[BRE] 2D: Setup skipped (guard present)."");
            return;
        }

        EditorSettings.defaultBehaviorMode = EditorBehaviorMode.Mode2D;
        Debug.Log(""[BRE] 2D: Setup init …"");

        if (IsUrpInstalled())
        {
            Debug.Log(""[BRE] 2D: URP already in manifest. Scheduling Create …"");
            EditorApplication.delayCall += CreateUrpAssetsAndScene;
        }
        else
        {
            Debug.Log(""[BRE] 2D: Installing packages (URP + 2D sprite + pixel perfect) …"");
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
        var txt = File.ReadAllText(manifest);
        return txt.IndexOf(""com.unity.render-pipelines.universal"", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsUrpInstalled() => ManifestHasUrp();

    private static void WaitForPackages()
    {
        if ((_addUrp == null) || (_add2dSprite == null) || (_add2dPixelPerfect == null))
        {
            EditorApplication.update -= WaitForPackages;
            return;
        }

        if (!_addUrp.IsCompleted || !_add2dSprite.IsCompleted || !_add2dPixelPerfect.IsCompleted)
            return;

        EditorApplication.update -= WaitForPackages;

        if (_addUrp.Status == StatusCode.Failure ||
            _add2dSprite.Status == StatusCode.Failure ||
            _add2dPixelPerfect.Status == StatusCode.Failure)
        {
            Debug.LogError(""[BRE] 2D: Package install failed. Check Console."");
            EditorUtility.DisplayDialog(""URP 2D Setup"", ""Paketinstallation fehlgeschlagen (URP/2D). Siehe Console."", ""OK"");
            return;
        }

        Debug.Log(""[BRE] 2D: Packages installed. Scheduling Create …"");
        EditorApplication.delayCall += CreateUrpAssetsAndScene;
    }

    private static void CreateUrpAssetsAndScene()
    {
        // URP Typen sicher verfügbar?
        var urpAssetType = Type.GetType(
            ""UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset, Unity.RenderPipelines.Universal.Runtime"");
        var rendererDataType = Type.GetType(
            ""UnityEngine.Rendering.Universal.UniversalRendererData, Unity.RenderPipelines.Universal.Runtime"");

        if (urpAssetType == null || rendererDataType == null)
        {
            Debug.Log(""[BRE] 2D: URP types not ready yet, re-schedule …"");
            EditorApplication.delayCall += CreateUrpAssetsAndScene;
            return;
        }

        Directory.CreateDirectory(""Assets/Settings"");

        // URP Asset
        var urpAssetPath = ""Assets/Settings/UniversalRenderPipelineAsset_2D.asset"";
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
        var rendererPath = ""Assets/Settings/UniversalRenderer_2D.asset"";
        UnityEngine.Object rendererDataObj = null;
        if (File.Exists(rendererPath))
            rendererDataObj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(rendererPath);
        if (rendererDataObj == null)
        {
            rendererDataObj = ScriptableObject.CreateInstance(rendererDataType);
            AssetDatabase.CreateAsset(rendererDataObj, rendererPath);
            AssetDatabase.SaveAssets();
        }

        // --- Post-Processing im Renderer aktivieren (ultra-robust) ---
        var rdSerialized = new SerializedObject(rendererDataObj);

        bool changed = false;
        var it = rdSerialized.GetIterator();
        bool enterChildren = true;
        while (it.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (it.propertyType != SerializedPropertyType.Boolean) continue;

            var n = it.name.ToLowerInvariant();
            bool looksLikePostToggle =
                (n.Contains(""post"") && (n.Contains(""enable"") || n.Contains(""process"")));
            if (looksLikePostToggle && !it.boolValue)
            {
                it.boolValue = true;
                changed = true;
            }
        }

        // Optional: PostProcessData zuweisen, falls Feld existiert & leer
        var ppDataProp =
            rdSerialized.FindProperty(""m_PostProcessData"") ??
            rdSerialized.FindProperty(""postProcessData"");
        if (ppDataProp != null && ppDataProp.propertyType == SerializedPropertyType.ObjectReference && ppDataProp.objectReferenceValue == null)
        {
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
        }

        if (changed)
        {
            rdSerialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(rendererDataObj);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        // Renderer in URP-Asset eintragen
        var so = new SerializedObject(urpAssetObj);
        var listProp = so.FindProperty(""m_RendererDataList"");
        var idxProp  = so.FindProperty(""m_DefaultRendererIndex"");

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

        // Global aktivieren
        GraphicsSettings.renderPipelineAsset = urpAssetObj;

        // Szene nur einmal anlegen
        Directory.CreateDirectory(""Assets/Scenes"");
        var scenePath = ""Assets/Scenes/Main_URP2D.unity"";
        if (!File.Exists(scenePath))
        {
            var newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Kamera (Orthographic) + Post-Processing = an
            var camGO = new GameObject(""Main Camera"");
            camGO.tag = ""MainCamera"";
            var cam = camGO.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            camGO.transform.position = new Vector3(0, 0, -10f);

            // UniversalAdditionalCameraData + PP aktivieren
            var uacdType = Type.GetType(""UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime"");
            if (uacdType != null)
            {
                var uacd = camGO.GetComponent(uacdType) ?? camGO.AddComponent(uacdType);
                var ppProp = uacdType.GetProperty(""renderPostProcessing"");
                if (ppProp != null && ppProp.CanWrite) ppProp.SetValue(uacd, true, null);
            }

            // Optional: PixelPerfectCamera anhängen (falls Paket da)
            var ppcType = Type.GetType(""UnityEngine.U2D.PixelPerfectCamera, Unity.2D.PixelPerfect"");
            if (ppcType != null)
            {
                camGO.AddComponent(ppcType);
            }

            // Optional: 2D Grid als Beispiel
            var grid = new GameObject(""Grid"");
            grid.AddComponent<UnityEngine.Grid>();

            // Globales Volume (leer) anlegen
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
                    var profilePath = ""Assets/Settings/GlobalVolumeProfile_2D.asset"";
                    AssetDatabase.CreateAsset(profile, profilePath);
                    AssetDatabase.SaveAssets();

                    var profileProp = volumeType.GetProperty(""profile"");
                    if (profileProp != null && profileProp.CanWrite)
                        profileProp.SetValue(volume, AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(profilePath), null);
                }
            }

            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), scenePath);
        }

        // Guard setzen (projektspezifisch)
        try
        {
            Directory.CreateDirectory(""ProjectSettings"");
            File.WriteAllText(FileGuardPath, ""done"");
        }
        catch { }

        Debug.Log(""[BRE] 2D: Setup completed (Renderer PP enabled, Camera PP enabled)."");
        EditorUtility.DisplayDialog(""URP 2D Setup"", ""URP 2D ist konfiguriert. Szene 'Main_URP2D' wurde angelegt (falls nicht vorhanden). Post-Processing ist aktiviert."", ""OK"");
    }
}
";
    }
}
