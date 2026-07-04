using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class SceneDirectControlRouter : MonoBehaviour
{
    private const string RouterName = "Scene Direct Control Router";
    private const string PlayerName = "Medieval";
    private static bool hasSeenInitialScene;
    private static bool directControlRequested;
    private static string requestedSceneName;
    private static SceneDirectControlRouter instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        EnsureInstance();
    }

    public static void RequestDirectControl(string sceneName)
    {
        directControlRequested = true;
        requestedSceneName = sceneName;
        OpeningMenuController.SkipNextOpeningMenu();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureInstance();

        if (!hasSeenInitialScene)
        {
            hasSeenInitialScene = true;
            return;
        }

        if (!directControlRequested)
        {
            RequestDirectControl(scene.name);
        }

        if (!string.IsNullOrWhiteSpace(requestedSceneName)
            && !string.Equals(scene.name, requestedSceneName, System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        directControlRequested = false;
        requestedSceneName = null;
        instance.StartCoroutine(BindAfterSceneSettles());
    }

    private static void EnsureInstance()
    {
        if (instance != null)
        {
            return;
        }

        var existing = GameObject.Find(RouterName);
        if (existing != null)
        {
            instance = existing.GetComponent<SceneDirectControlRouter>();
            if (instance != null)
            {
                DontDestroyOnLoad(existing);
                return;
            }
        }

        var obj = new GameObject(RouterName);
        instance = obj.AddComponent<SceneDirectControlRouter>();
        DontDestroyOnLoad(obj);
    }

    private static IEnumerator BindAfterSceneSettles()
    {
        yield return null;
        yield return null;
        BindDirectControl();
    }

    private static void BindDirectControl()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        DisableOpeningMenuObjects();

        var playerRoot = FindPlayerRoot();
        if (playerRoot == null)
        {
            Debug.LogWarning("SceneDirectControlRouter could not find the Medieval player after scene load.");
            return;
        }

        DisableNonPlayerControllers(playerRoot);

        var controller = playerRoot.GetComponent<CharacterController>();
        if (controller != null)
        {
            controller.enabled = true;
        }

        var motor = playerRoot.GetComponent<TopDownCharacterMotor>();
        if (motor == null)
        {
            motor = playerRoot.gameObject.AddComponent<TopDownCharacterMotor>();
        }

        motor.enabled = true;

        var inventory = playerRoot.GetComponent<PlayerInventoryUI>();
        if (inventory != null)
        {
            inventory.enabled = true;
            if (inventory.IsOpen)
            {
                inventory.SetOpen(false);
            }
        }

        var pauseMenu = playerRoot.GetComponent<PauseMenuController>();
        if (pauseMenu != null)
        {
            pauseMenu.enabled = true;
        }

        var mainCamera = FindMainCameraInActiveScene();
        if (mainCamera == null)
        {
            Debug.LogWarning("SceneDirectControlRouter could not find a scene camera after scene load.");
            return;
        }

        mainCamera.enabled = true;
        mainCamera.orthographic = false;
        mainCamera.allowHDR = false;
        mainCamera.fieldOfView = 14f;
        mainCamera.focalLength = 145f;
        mainCamera.nearClipPlane = 0.1f;
        mainCamera.farClipPlane = Mathf.Max(mainCamera.farClipPlane, 1000f);

        motor.cameraTransform = mainCamera.transform;

        var follow = mainCamera.GetComponent<IsometricCameraFollow>();
        if (follow == null)
        {
            follow = mainCamera.gameObject.AddComponent<IsometricCameraFollow>();
        }

        follow.target = playerRoot;
        follow.offset = new Vector3(-24.5f, 34.8f, -24.5f);
        follow.lockRotation = true;
        follow.ResetVelocity();

        var occlusionFader = mainCamera.GetComponent<CameraOcclusionFader>();
        if (occlusionFader == null)
        {
            occlusionFader = mainCamera.gameObject.AddComponent<CameraOcclusionFader>();
        }

        occlusionFader.target = playerRoot;

        mainCamera.transform.position = playerRoot.position + follow.offset;
        mainCamera.transform.LookAt(playerRoot.position);
    }

    private static void DisableNonPlayerControllers(Transform playerRoot)
    {
        var activeScene = SceneManager.GetActiveScene();
        foreach (var motor in FindObjectsOfType<TopDownCharacterMotor>(true))
        {
            if (motor.gameObject.scene != activeScene || motor.transform.root == playerRoot)
            {
                continue;
            }

            var root = motor.transform.root;
            motor.enabled = false;

            var inventory = root.GetComponent<PlayerInventoryUI>();
            if (inventory != null)
            {
                inventory.enabled = false;
            }

            var pauseMenu = root.GetComponent<PauseMenuController>();
            if (pauseMenu != null)
            {
                pauseMenu.enabled = false;
            }

            var controller = root.GetComponent<CharacterController>();
            if (controller != null)
            {
                controller.enabled = false;
            }

            if (root.name.Contains("(Clone)") || root.name.Contains("Inventory Character Preview"))
            {
                root.gameObject.SetActive(false);
            }
        }
    }

    private static void DisableOpeningMenuObjects()
    {
        foreach (var menu in FindObjectsOfType<OpeningMenuController>(true))
        {
            menu.ForceCloseForDirectControl();

            var camera = menu.GetComponent<Camera>();
            if (camera != null)
            {
                camera.targetTexture = null;
                camera.enabled = false;
            }
        }

        var openingHud = GameObject.Find("Opening Menu HUD");
        if (openingHud != null)
        {
            openingHud.SetActive(false);
        }
    }

    private static Transform FindPlayerRoot()
    {
        var activeScene = SceneManager.GetActiveScene();
        foreach (var transform in FindObjectsOfType<Transform>(true))
        {
            if (transform.gameObject.scene != activeScene)
            {
                continue;
            }

            if (string.Equals(transform.name, PlayerName, System.StringComparison.OrdinalIgnoreCase))
            {
                return transform.root;
            }
        }

        foreach (var motor in FindObjectsOfType<TopDownCharacterMotor>(true))
        {
            if (motor.gameObject.scene == activeScene)
            {
                return motor.transform;
            }
        }

        return null;
    }

    private static Camera FindMainCameraInActiveScene()
    {
        var activeScene = SceneManager.GetActiveScene();
        var mainCamera = Camera.main;
        if (mainCamera != null && mainCamera.gameObject.scene == activeScene)
        {
            return mainCamera;
        }

        foreach (var camera in FindObjectsOfType<Camera>(true))
        {
            if (camera.gameObject.scene == activeScene && camera.CompareTag("MainCamera"))
            {
                return camera;
            }
        }

        foreach (var camera in FindObjectsOfType<Camera>(true))
        {
            if (camera.gameObject.scene == activeScene)
            {
                return camera;
            }
        }

        return null;
    }
}
