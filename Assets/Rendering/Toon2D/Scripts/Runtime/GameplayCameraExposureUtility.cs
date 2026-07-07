using UnityEngine;
using UnityEngine.SceneManagement;

public static class GameplayCameraExposureUtility
{
    public static void ApplyGameplayDefaults(Camera camera, bool disableHdr)
    {
        if (camera == null)
        {
            return;
        }

        camera.allowHDR = !disableHdr;
        SetUniversalCameraDataProperty(camera, "allowHDROutput", !disableHdr);
        ApplySceneColorDefaults();
    }

    private static void ApplySceneColorDefaults()
    {
        var sceneName = SceneManager.GetActiveScene().name;
        if (string.Equals(sceneName, "Lvl_1", System.StringComparison.OrdinalIgnoreCase))
        {
            ApplyLevelOneColorDefaults();
            return;
        }

        if (string.Equals(sceneName, "Lvl_2", System.StringComparison.OrdinalIgnoreCase))
        {
            ApplyLevelTwoColorDefaults();
        }
    }

    private static void ApplyLevelOneColorDefaults()
    {
        RenderSettings.ambientSkyColor = new Color(0.24f, 0.27f, 0.31f, 1f);
        RenderSettings.ambientEquatorColor = new Color(0.085f, 0.092f, 0.098f, 1f);
        RenderSettings.ambientGroundColor = new Color(0.034f, 0.032f, 0.03f, 1f);
        RenderSettings.ambientIntensity = 0.72f;
        RenderSettings.reflectionIntensity = 0.45f;

        ApplyKeyLightDefaults(new Color(0.92f, 0.94f, 1f, 1f), 0.9f, 0.7f);
    }

    private static void ApplyLevelTwoColorDefaults()
    {
        RenderSettings.ambientSkyColor = new Color(0.212f, 0.227f, 0.259f, 1f);
        RenderSettings.ambientEquatorColor = new Color(0.114f, 0.125f, 0.133f, 1f);
        RenderSettings.ambientGroundColor = new Color(0.047f, 0.043f, 0.035f, 1f);
        RenderSettings.ambientIntensity = 1f;
        RenderSettings.reflectionIntensity = 1f;
    }

    private static void ApplyKeyLightDefaults(Color color, float intensity, float bounceIntensity)
    {
        var keyLightObject = GameObject.Find("Key Toon Light");
        if (keyLightObject == null)
        {
            return;
        }

        var keyLight = keyLightObject.GetComponent<Light>();
        if (keyLight == null)
        {
            return;
        }

        keyLight.color = color;
        keyLight.intensity = intensity;
        keyLight.bounceIntensity = bounceIntensity;
    }

    private static void SetUniversalCameraDataProperty(Camera camera, string propertyName, object value)
    {
        var additionalDataType = System.Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
        if (additionalDataType == null)
        {
            return;
        }

        var additionalData = camera.GetComponent(additionalDataType);
        if (additionalData == null)
        {
            return;
        }

        var property = additionalDataType.GetProperty(propertyName);
        if (property == null || !property.CanWrite)
        {
            return;
        }

        property.SetValue(additionalData, value, null);
    }
}
