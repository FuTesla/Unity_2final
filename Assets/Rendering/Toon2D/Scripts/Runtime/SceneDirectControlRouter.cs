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
    private static bool hasRequestedSpawnPosition;
    private static Vector3 requestedSpawnPosition;
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
        hasRequestedSpawnPosition = false;
        OpeningMenuController.SkipNextOpeningMenu();
    }

    public static bool ShouldBypassOpeningMenuForScene(string sceneName)
    {
        return directControlRequested
            && (string.IsNullOrWhiteSpace(requestedSceneName)
                || string.Equals(sceneName, requestedSceneName, System.StringComparison.OrdinalIgnoreCase));
    }

    public static void RequestDirectControl(string sceneName, Vector3 spawnPosition)
    {
        RequestDirectControl(sceneName);
        hasRequestedSpawnPosition = true;
        requestedSpawnPosition = spawnPosition;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureInstance();

        if (!hasSeenInitialScene)
        {
            hasSeenInitialScene = true;
            if (!HasOpeningMenuInScene(scene))
            {
                instance.StartCoroutine(BindAfterSceneSettles(null));
            }

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

        var spawnPosition = hasRequestedSpawnPosition ? requestedSpawnPosition : (Vector3?)null;
        directControlRequested = false;
        requestedSceneName = null;
        hasRequestedSpawnPosition = false;
        instance.StartCoroutine(BindAfterSceneSettles(spawnPosition));
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

    private static IEnumerator BindAfterSceneSettles(Vector3? spawnPosition)
    {
        yield return null;
        yield return null;
        BindDirectControl(spawnPosition);
        yield return null;
        yield return null;
        BindDirectControl(null);
    }

    private static void BindDirectControl(Vector3? spawnPosition)
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

        playerRoot.gameObject.SetActive(true);
        DisableNonPlayerControllers(playerRoot);

        var controller = playerRoot.GetComponent<CharacterController>();
        if (spawnPosition.HasValue)
        {
            MovePlayerToSpawn(playerRoot, controller, spawnPosition.Value);
        }

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

        DisableNonGameplayCameras(mainCamera);
        mainCamera.enabled = true;
        mainCamera.orthographic = false;
        GameplayCameraExposureUtility.ApplyGameplayDefaults(mainCamera, true);
        mainCamera.fieldOfView = 52f;
        mainCamera.focalLength = 38f;
        mainCamera.nearClipPlane = 0.1f;
        mainCamera.farClipPlane = Mathf.Max(mainCamera.farClipPlane, 1000f);

        motor.cameraTransform = mainCamera.transform;

        var follow = mainCamera.GetComponent<IsometricCameraFollow>();
        if (follow == null)
        {
            follow = mainCamera.gameObject.AddComponent<IsometricCameraFollow>();
        }

        follow.target = playerRoot;
        follow.offset = new Vector3(0f, 2.8f, -5.2f);
        follow.focusOffset = new Vector3(0f, 1.35f, 0f);
        follow.cameraDistance = 5.9f;
        follow.lockRotation = true;
        follow.ResetVelocity();

        var occlusionFader = mainCamera.GetComponent<CameraOcclusionFader>();
        if (occlusionFader == null)
        {
            occlusionFader = mainCamera.gameObject.AddComponent<CameraOcclusionFader>();
        }

        occlusionFader.target = playerRoot;
        occlusionFader.enableOcclusionHandling = false;
        occlusionFader.renderPlayerOnTopWhenOccluded = false;

        mainCamera.transform.position = playerRoot.position + follow.offset;
        mainCamera.transform.LookAt(playerRoot.position + follow.focusOffset);
    }

    private static void MovePlayerToSpawn(Transform playerRoot, CharacterController controller, Vector3 spawnPosition)
    {
        var restoreController = controller != null && controller.enabled;
        if (controller != null)
        {
            controller.enabled = false;
        }

        playerRoot.position = spawnPosition;
        Physics.SyncTransforms();

        if (controller != null)
        {
            controller.enabled = restoreController;
        }
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

    private static void DisableNonGameplayCameras(Camera gameplayCamera)
    {
        var activeScene = SceneManager.GetActiveScene();
        foreach (var camera in FindObjectsOfType<Camera>(true))
        {
            if (camera == null || camera == gameplayCamera || camera.gameObject.scene != activeScene)
            {
                continue;
            }

            if (camera.GetComponent<OpeningMenuController>() != null || camera.name.IndexOf("Opening", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                camera.targetTexture = null;
                camera.enabled = false;
                GameplayCameraExposureUtility.ApplyGameplayDefaults(camera, true);
            }
        }
    }

    private static bool HasOpeningMenuInScene(Scene scene)
    {
        foreach (var menu in FindObjectsOfType<OpeningMenuController>(true))
        {
            if (menu.gameObject.scene == scene && menu.showOnStart)
            {
                return true;
            }
        }

        return false;
    }

    private static Transform FindPlayerRoot()
    {
        var activeScene = SceneManager.GetActiveScene();
        Transform inactiveCandidate = null;
        foreach (var transform in FindObjectsOfType<Transform>(true))
        {
            if (transform.gameObject.scene != activeScene)
            {
                continue;
            }

            if (string.Equals(transform.name, PlayerName, System.StringComparison.OrdinalIgnoreCase))
            {
                var root = transform.root;
                if (!IsUsablePlayerRoot(root))
                {
                    continue;
                }

                if (root.gameObject.activeInHierarchy)
                {
                    return root;
                }

                if (inactiveCandidate == null)
                {
                    inactiveCandidate = root;
                }
            }
        }

        if (inactiveCandidate != null)
        {
            return inactiveCandidate;
        }

        Transform disabledMotorCandidate = null;
        foreach (var motor in FindObjectsOfType<TopDownCharacterMotor>(true))
        {
            if (motor.gameObject.scene != activeScene || !IsUsablePlayerRoot(motor.transform.root))
            {
                continue;
            }

            if (motor.gameObject.activeInHierarchy)
            {
                return motor.transform.root;
            }

            if (disabledMotorCandidate == null)
            {
                disabledMotorCandidate = motor.transform.root;
            }
        }

        return disabledMotorCandidate;
    }

    private static bool IsUsablePlayerRoot(Transform root)
    {
        if (root == null)
        {
            return false;
        }

        if (root.name.IndexOf("(Clone)", System.StringComparison.OrdinalIgnoreCase) >= 0
            || root.name.IndexOf("Inventory Character Preview", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return false;
        }

        return root.GetComponent<TopDownCharacterMotor>() != null
            || root.GetComponent<CharacterController>() != null
            || root.GetComponentInChildren<Animator>(true) != null;
    }

    private static Camera FindMainCameraInActiveScene()
    {
        var activeScene = SceneManager.GetActiveScene();
        foreach (var camera in FindObjectsOfType<Camera>(true))
        {
            if (camera.gameObject.scene == activeScene && camera.CompareTag("MainCamera") && camera.GetComponent<OpeningMenuController>() == null)
            {
                return camera;
            }
        }

        foreach (var camera in FindObjectsOfType<Camera>(true))
        {
            if (camera.gameObject.scene == activeScene
                && string.Equals(camera.name, "Main Camera", System.StringComparison.OrdinalIgnoreCase)
                && camera.GetComponent<OpeningMenuController>() == null)
            {
                return camera;
            }
        }

        var mainCamera = Camera.main;
        if (mainCamera != null && mainCamera.gameObject.scene == activeScene && mainCamera.GetComponent<OpeningMenuController>() == null)
        {
            return mainCamera;
        }

        foreach (var camera in FindObjectsOfType<Camera>(true))
        {
            if (camera.gameObject.scene == activeScene && camera.GetComponent<OpeningMenuController>() == null)
            {
                return camera;
            }
        }

        return mainCamera != null && mainCamera.gameObject.scene == activeScene ? mainCamera : null;
    }
}
