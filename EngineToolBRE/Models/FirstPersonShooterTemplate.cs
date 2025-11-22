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
            var editor = Path.Combine(_targetPath, "Editor");
            var scripts = Path.Combine(assets, "Scripts");
            Directory.CreateDirectory(assets);
            Directory.CreateDirectory(editor);
            Directory.CreateDirectory(scripts);

            File.WriteAllText(Path.Combine(scripts, "Gun.cs"), RUNTIME_GUN);
            File.WriteAllText(Path.Combine(scripts, "Health.cs"), RUNTIME_HEALTH);

            File.WriteAllText(Path.Combine(editor, "BRE_AutoFpsSetup.cs"), EDITOR_AUTO_FPS_SETUP);

            _log("FPS-Template Datein geschrieben. Beim ersten Editor-Start wird die Szene automatisch erzeugt.");
            return Task.CompletedTask;
        }

        private const string RUNTIME_GUN = @"
            using UnityEngine;

public class Gun : MonoBehaviour
{
    [Header(""Gun Settings"")]
    [SerializeField] private float damage = 25f;
    [SerializeField] private float range = 100f;
    [SerializeField] private float fireCooldown = 0.12f;
    [SerializeField] private Transform shootOrigin; // z.B. Kamera
    [SerializeField] private LayerMask hitMask = ~0;

    private float _nextFireTime;

    private void Reset()
    {
        shootOrigin = Camera.main ? Camera.main.transform : transform;
    }

    public void TryFire()
    {
        if (Time.time < _nextFireTime) return;
        _nextFireTime = Time.time + fireCooldown;
        Fire();
    }

    private void Fire()
    {
        if (!shootOrigin) shootOrigin = transform;

        if (Physics.Raycast(shootOrigin.position, shootOrigin.forward, out var hit, range, hitMask, QueryTriggerInteraction.Ignore))
        {
            // Health suchen und Schaden zufügen
            var h = hit.collider.GetComponentInParent<Health>();
            if (h) h.ApplyDamage(damage);

            // kleiner Einschlag-Impuls
            if (hit.rigidbody) hit.rigidbody.AddForceAtPosition(shootOrigin.forward * 2f, hit.point, ForceMode.Impulse);

            // einfacher Debug-Impact
            Debug.DrawLine(shootOrigin.position, hit.point, Color.yellow, 0.2f);
        }

        // Mündungsblitz-Gizmo
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
        // Farb-Feedback: grün -> rot je nach HP
        float t = 1f - Mathf.Clamp01(_hp / Mathf.Max(1f, maxHealth));
        var col = Color.Lerp(new Color(0.2f, 0.9f, 0.4f), new Color(0.9f, 0.2f, 0.25f), t);
        if (colorFeedbackRenderer.material && colorFeedbackRenderer.material.HasProperty(""_Color""))
            colorFeedbackRenderer.material.color = col;
    }
}
        ";

        private const string EDITOR_AUTO_FPS_SETUP = @"
            using System;
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
    private static AddRequest _addInputSystem;

    [InitializeOnLoadMethod]
    private static void Init()
    {
        if (File.Exists(FileGuardPath)) return;

        // Input System sicherstellen
        if (!ManifestHas(""com.unity.inputsystem""))
        {
            _addInputSystem = Client.Add(""com.unity.inputsystem"");
            EditorApplication.update += WaitForInputInstall;
        }
        else
        {
            EditorApplication.delayCall += CreateSceneAndPlayer;
        }
    }

    [MenuItem(""BRE/FPS/Run Setup Now"")]
    private static void MenuRunNow()
    {
        if (File.Exists(FileGuardPath)) File.Delete(FileGuardPath);
        EditorApplication.delayCall += CreateSceneAndPlayer;
    }

    private static void WaitForInputInstall()
    {
        if (_addInputSystem == null) { EditorApplication.update -= WaitForInputInstall; return; }
        if (!_addInputSystem.IsCompleted) return;

        EditorApplication.update -= WaitForInputInstall;

        if (_addInputSystem.Status == StatusCode.Failure)
        {
            Debug.LogError(""[BRE] Input System Installation failed: "" + _addInputSystem.Error.message);
            return;
        }

        // Nach Installation Domain-Reload → danach Assets anlegen
        EditorApplication.delayCall += CreateSceneAndPlayer;
    }

    private static bool ManifestHas(string packageName)
    {
        var manifest = ""Packages/manifest.json"";
        if (!File.Exists(manifest)) return false;
        return File.ReadAllText(manifest).IndexOf(packageName, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static void CreateSceneAndPlayer()
    {
        try
        {
            Directory.CreateDirectory(""Assets/Scenes"");
            Directory.CreateDirectory(""Assets/Input"");
            Directory.CreateDirectory(""Assets/Settings"");

            // 1) Input Actions erzeugen
            var actions = CreateInputActionsAsset(""Assets/Input/PlayerInputActions.inputactions"");

            // 2) Scene erstellen (falls nicht vorhanden)
            var scenePath = ""Assets/Scenes/Main_FPS.unity"";
            if (!File.Exists(scenePath))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

                // Boden
                var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
                floor.name = ""Floor"";
                floor.transform.position = Vector3.zero;
                floor.transform.localScale = new Vector3(4, 1, 4); // größerer Floor

                // ein paar Boxen als Ziele
                for (int i = 0; i < 6; i++)
                {
                    var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    box.name = ""Target_"" + i;
                    box.transform.position = new Vector3(UnityEngine.Random.Range(-12f, 12f), 0.5f, UnityEngine.Random.Range(6f, 18f));
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

                // dein PlayerController
                var pc = player.AddComponent<PlayerController>();

                // Kamera als Child
                var camGO = new GameObject(""Main Camera"");
                camGO.tag = ""MainCamera"";
                camGO.transform.SetParent(player.transform, false);
                camGO.transform.localPosition = new Vector3(0, 0.9f, 0f);
                var cam = camGO.AddComponent<Camera>();
                cam.fieldOfView = 75f;
                camGO.AddComponent<AudioListener>();

                // Gun an Kamera
                var gun = camGO.AddComponent<Gun>();

                // PlayerInput (Input System)
                var playerInputType = Type.GetType(""UnityEngine.InputSystem.PlayerInput, Unity.InputSystem"");
                if (playerInputType != null)
                {
                    var pi = camGO.AddComponent(playerInputType); // an Kamera, geht auch am Player
                    // actions zuweisen
                    var field = playerInputType.GetProperty(""actions"");
                    var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(""Assets/Input/PlayerInputActions.inputactions"");
                    field?.SetValue(pi, asset, null);

                    // Default Map
                    var defMap = playerInputType.GetProperty(""defaultActionMap"");
                    defMap?.SetValue(pi, ""Player"", null);

                    // Events-Verbindung: Schuss mit LeftClick
                    var onActionTriggeredEvt = playerInputType.GetEvent(""onActionTriggered"");
                    if (onActionTriggeredEvt != null)
                    {
                        System.Action<object> handler = (ctx) =>
                        {
                            // nichts – Event-Bridge geht zur Runtime nicht direkt via Reflection
                        };
                    }
                }

                // Player-Startposition
                player.transform.position = new Vector3(0, 1.1f, -6f);

                // Save scene
                EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), scenePath);
            }

            File.WriteAllText(FileGuardPath, ""done"");
            AssetDatabase.Refresh();
            Debug.Log(""[BRE] FPS setup finished. Scene 'Main_FPS' created."");
        }
        catch (Exception ex)
        {
            Debug.LogError(""[BRE] FPS setup failed: "" + ex);
        }
    }

    // Erzeugt ein InputActionAsset mit: Move (WASD), Look (Mouse), Jump (Space), Fire (LeftMouse)
    private static UnityEngine.Object CreateInputActionsAsset(string path)
    {
#if ENABLE_INPUT_SYSTEM
        var asset = new UnityEngine.InputSystem.InputActionAsset();

        var map = new UnityEngine.InputSystem.InputActionMap(""Player"");
        var aMove = map.AddAction(""Move"", UnityEngine.InputSystem.InputActionType.Value, ""<Gamepad>/leftStick"");
        aMove.AddCompositeBinding(""2DVector"")
            .With(""Up"", ""<Keyboard>/w"")
            .With(""Down"", ""<Keyboard>/s"")
            .With(""Left"", ""<Keyboard>/a"")
            .With(""Right"", ""<Keyboard>/d"");

        var aLook = map.AddAction(""Look"", UnityEngine.InputSystem.InputActionType.Value);
        aLook.AddBinding(""<Mouse>/delta"");

        var aJump = map.AddAction(""Jump"", UnityEngine.InputSystem.InputActionType.Button, ""<Keyboard>/space"");

        var aFire = map.AddAction(""Fire"", UnityEngine.InputSystem.InputActionType.Button, ""<Mouse>/leftButton"");

        asset.AddActionMap(map);

        if (!Directory.Exists(Path.GetDirectoryName(path))) Directory.CreateDirectory(Path.GetDirectoryName(path));
        UnityEditor.AssetDatabase.CreateAsset(asset, path);
        UnityEditor.AssetDatabase.SaveAssets();
        UnityEditor.AssetDatabase.Refresh();
        return asset;
#else
        // Falls das ScriptingDefine noch nicht aktiv ist, erzeugen wir leeres Asset
        var obj = ScriptableObject.CreateInstance<ScriptableObject>();
        if (!Directory.Exists(Path.GetDirectoryName(path))) Directory.CreateDirectory(Path.GetDirectoryName(path));
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
