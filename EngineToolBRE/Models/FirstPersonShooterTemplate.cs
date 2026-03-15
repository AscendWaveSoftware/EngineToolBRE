using System;
using System.IO;
using System.Threading.Tasks;

namespace EngineToolBRE.Models
{
    public sealed class FirstPersonShooterTemplate : TemplateBase
    {
        public FirstPersonShooterTemplate() : base(
            "First Person Shooter (Simple)",
            "Erzeugt eine kleine 3D-Testszene, einen FPS-Player mit Schießen und einfachen Zielen."
        ) { }

        public override Task RunAsync(string _targetPath, UnityInstallation _unity, Action<string> _log)
        {
            var assets = Path.Combine(_targetPath, "Assets");
            var editor = Path.Combine(assets, "Editor");
            var scripts = Path.Combine(assets, "Scripts");
            Directory.CreateDirectory(assets);
            Directory.CreateDirectory(editor);
            Directory.CreateDirectory(scripts);

            File.WriteAllText(Path.Combine(scripts, "Gun.cs"), RUNTIME_GUN);
            File.WriteAllText(Path.Combine(scripts, "Health.cs"), RUNTIME_HEALTH);

            File.WriteAllText(Path.Combine(editor, "BRE_AutoFpsSetup.cs"), EDITOR_AUTO_SETUP_FPS);

            _log("FPS-Template Datein geschrieben. Beim ersten Editor-Start wird die Szene automatisch erzeugt.");
            return Task.CompletedTask;
        }

        private const string RUNTIME_GUN = @"using UnityEngine;

public class Gun : MonoBehaviour
{
    [Header(""Gun Settings"")]
    [SerializeField] private float damage = 25f;
    [SerializeField] private float range = 100f;
    [SerializeField] private float fireCooldown = 0.12f;
    [SerializeField] private Transform shootOrigin;
    [SerializeField] private LayerMask hitMask = ~0;

    private float _nextFireTime;

    private void Reset()
    {
        if (!shootOrigin && Camera.main)
            shootOrigin = Camera.main.transform;
    }

    public void TryFire()
    {
        if (Time.time < _nextFireTime) 
            return;

        _nextFireTime = Time.time + fireCooldown;
        Fire();
    }

    private void Fire()
    {
        if (!shootOrigin)
            shootOrigin = transform;

        if (UnityEngine.Physics.Raycast(
                shootOrigin.position,
                shootOrigin.forward,
                out UnityEngine.RaycastHit hit,
                range,
                hitMask,
                UnityEngine.QueryTriggerInteraction.Ignore))
        {
            var h = hit.collider.GetComponentInParent<Health>();
            if (h != null)
            {
                h.ApplyDamage(damage);
            }

            var rb = hit.rigidbody;
            if (rb != null)
            {
                rb.AddForceAtPosition(
                    shootOrigin.forward * 2f,
                    hit.point,
                    UnityEngine.ForceMode.Impulse);
            }

            Debug.DrawLine(shootOrigin.position, hit.point, Color.yellow, 0.2f);
        }

        Debug.DrawRay(shootOrigin.position, shootOrigin.forward * 0.6f, Color.cyan, 0.05f);
    }
}
";

        private const string RUNTIME_HEALTH = @"
            using UnityEngine;

public class Health : MonoBehaviour
{
    [SerializeField] private float maxHealth = 50f;
    [SerializeField] private bool destroyOnDeath = true;
    [SerializeField] private Renderer colorFeedbackRenderer;

    private float _hp;

    private void Awake()
    {
        _hp = maxHealth;
        if (!colorFeedbackRenderer) colorFeedbackRenderer = GetComponentInChildren<Renderer>();
        UpdateVisual();
    }

    public void ApplyDamage(float amount)
    {
        _hp -= amount;
        if (_hp <= 0f)
        {
            _hp = 0f;
            if (destroyOnDeath)
                Destroy(gameObject);
        }
        UpdateVisual();
    }

    private void UpdateVisual()
    {
        if (!colorFeedbackRenderer) return;
        float t = 1f - Mathf.Clamp01(_hp / Mathf.Max(1f, maxHealth));
        var col = Color.Lerp(new Color(0.2f, 0.9f, 0.4f), new Color(0.9f, 0.2f, 0.25f), t);
        if (colorFeedbackRenderer.material && colorFeedbackRenderer.material.HasProperty(""_Color""))
            colorFeedbackRenderer.material.color = col;
    }
}
        ";

        private const string EDITOR_AUTO_SETUP_FPS = @"using System;
using System.IO;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BRE_AutoFpsSetup
{
    private const string FileGuardPath = ""ProjectSettings/BRE_FPS_DONE.flag"";
    private const string ScenePath     = ""Assets/Scenes/Main_FPS.unity"";

    private static AddRequest _addInputSystem;

    [InitializeOnLoadMethod]
    private static void Init()
    {
        Debug.Log(""[BRE FPS] Init domain reload …"");

        if (File.Exists(FileGuardPath) && File.Exists(ScenePath))
        {
            Debug.Log(""[BRE FPS] Guard + Scene present, auto-setup skipped."");
            return;
        }

        EnsureInputSystemThenSetup();
    }

    [MenuItem(""BRE/FPS/Run Setup Now (Force)"")]
    private static void MenuRunNowForce()
    {
        Debug.Log(""[BRE FPS] Manual menu trigger (force)."");

        if (File.Exists(FileGuardPath))
            File.Delete(FileGuardPath);

        EnsureInputSystemThenSetup();
    }

    private static void EnsureInputSystemThenSetup()
    {
        if (ManifestHas(""com.unity.inputsystem""))
        {
            Debug.Log(""[BRE FPS] Input System already present. Creating scene/player …"");
            EditorApplication.delayCall += CreateSceneAndPlayer;
            return;
        }

        Debug.Log(""[BRE FPS] Input System not in manifest. Installing via UPM …"");
        _addInputSystem = Client.Add(""com.unity.inputsystem"");
        EditorApplication.update += WaitForInputInstall;
    }

    private static void WaitForInputInstall()
    {
        if (_addInputSystem == null)
        {
            EditorApplication.update -= WaitForInputInstall;
            Debug.LogWarning(""[BRE FPS] WaitForInputInstall: no request."");
            return;
        }

        if (!_addInputSystem.IsCompleted)
            return;

        EditorApplication.update -= WaitForInputInstall;

        if (_addInputSystem.Status == StatusCode.Failure)
        {
            Debug.LogError(""[BRE FPS] Input System installation failed: "" + _addInputSystem.Error.message);
            return;
        }

        Debug.Log(""[BRE FPS] Input System installed. Creating scene/player …"");
        EditorApplication.delayCall += CreateSceneAndPlayer;
    }

    private static bool ManifestHas(string packageName)
    {
        var manifest = ""Packages/manifest.json"";
        if (!File.Exists(manifest)) return false;
        return File.ReadAllText(manifest)
                   .IndexOf(packageName, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static void CreateSceneAndPlayer()
    {
        try
        {
            Directory.CreateDirectory(""Assets/Scenes"");
            Directory.CreateDirectory(""Assets/Input"");
            Directory.CreateDirectory(""Assets/Settings"");

            var actions = CreateInputActionsAsset(""Assets/Input/PlayerInputActions.asset"");

            if (!File.Exists(ScenePath))
            {
                Debug.Log(""[BRE FPS] Creating scene + player …"");

                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

                var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
                floor.name = ""Floor"";
                floor.transform.position = Vector3.zero;
                floor.transform.localScale = new Vector3(4, 1, 4);

                for (int i = 0; i < 6; i++)
                {
                    var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    box.name = ""Target_"" + i;
                    box.transform.position = new Vector3(
                        UnityEngine.Random.Range(-12f, 12f),
                        0.5f,
                        UnityEngine.Random.Range(6f, 18f));

                    var rb = box.AddComponent<Rigidbody>();
                    rb.mass = 2f;
                    box.AddComponent<Health>();
                }

                // Player
                var player = new GameObject(""Player"");
                var col = player.AddComponent<CapsuleCollider>();
                col.height = 1.8f;
                col.center = new Vector3(0, 0.9f, 0);

                var rbp = player.AddComponent<Rigidbody>();
                rbp.constraints = RigidbodyConstraints.FreezeRotation;

                var camGO = new GameObject(""Main Camera"");
                camGO.tag = ""MainCamera"";
                camGO.transform.SetParent(player.transform, false);
                camGO.transform.localPosition = new Vector3(0, 0.9f, 0f);

                var cam = camGO.AddComponent<Camera>();
                cam.fieldOfView = 75f;
                camGO.AddComponent<AudioListener>();

                camGO.AddComponent<Gun>();

                var playerInputType = Type.GetType(""UnityEngine.InputSystem.PlayerInput, Unity.InputSystem"");
                if (playerInputType != null)
                {
                    var pi = camGO.AddComponent(playerInputType);

                    var field = playerInputType.GetProperty(""actions"");
                    var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                        ""Assets/Input/PlayerInputActions.asset"");
                    field?.SetValue(pi, asset, null);

                    var defMap = playerInputType.GetProperty(""defaultActionMap"");
                    defMap?.SetValue(pi, ""Player"", null);
                }

                player.transform.position = new Vector3(0, 1.1f, -6f);

                EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath);
            }
            else
            {
                Debug.Log(""[BRE FPS] Scene already exists, skipping creation."");
            }

            Directory.CreateDirectory(""ProjectSettings"");
            File.WriteAllText(FileGuardPath, ""done"");

            AssetDatabase.Refresh();
            Debug.Log(""[BRE FPS] Setup finished. Scene 'Main_FPS' ready."");
        }
        catch (Exception ex)
        {
            Debug.LogError(""[BRE FPS] Setup failed: "" + ex);
        }
    }

    private static UnityEngine.Object CreateInputActionsAsset(string path)
    {
        #if ENABLE_INPUT_SYSTEM
    var asset = ScriptableObject.CreateInstance<UnityEngine.InputSystem.InputActionAsset>();

    var dir = Path.GetDirectoryName(path);
    if (!Directory.Exists(dir))
        Directory.CreateDirectory(dir);

    var existing = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
    if (existing != null)
        return existing;

    UnityEditor.AssetDatabase.CreateAsset(asset, path);
    UnityEditor.AssetDatabase.SaveAssets();
    UnityEditor.AssetDatabase.Refresh();
    return asset;
#else
    var obj = ScriptableObject.CreateInstance<ScriptableObject>();
    var dir = Path.GetDirectoryName(path);
    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
    UnityEditor.AssetDatabase.CreateAsset(obj, path);
    UnityEditor.AssetDatabase.SaveAssets();
    UnityEditor.AssetDatabase.Refresh();
    return obj;
#endif
    }
}
";
    }
}
