using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public static class GameFontUtility
{
    private const string DongfangDakaiFontPath = "Assets/Art/Fonts/\u4e1c\u65b9\u5927\u6977.ttf";
    private const string RuntimeUIFontResourcePath = "Fonts/UIFont";
    private static Font cachedFont;

    public static Font GetUIFont()
    {
        if (cachedFont != null)
        {
            return cachedFont;
        }

#if UNITY_EDITOR
        cachedFont = AssetDatabase.LoadAssetAtPath<Font>(DongfangDakaiFontPath);
        if (cachedFont != null)
        {
            return cachedFont;
        }
#endif

        cachedFont = Resources.Load<Font>(RuntimeUIFontResourcePath);
        if (cachedFont != null)
        {
            return cachedFont;
        }

        foreach (var font in Resources.FindObjectsOfTypeAll<Font>())
        {
            if (font != null && font.name.IndexOf("\u4e1c\u65b9\u5927\u6977", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                cachedFont = font;
                return cachedFont;
            }
        }

        try
        {
            cachedFont = Font.CreateDynamicFontFromOSFont(
                new[] { "Alimama DongFangDaKai", "Microsoft YaHei UI", "Microsoft YaHei", "SimHei" },
                24);
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning($"UI font lookup failed: {exception.Message}");
        }

        if (cachedFont == null)
        {
            cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        return cachedFont;
    }
}
