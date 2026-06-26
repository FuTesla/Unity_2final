using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class ToonMaterialConverter
{
    private const string OutputFolder = "Assets/Rendering/Toon2D/Materials/Converted";

    [MenuItem("Tools/Toon 2D/Convert Selected To Toon Materials")]
    public static void ConvertSelectedToToonMaterials()
    {
        EnsureFolder(OutputFolder);

        var sourceMaterials = CollectSelectedMaterials();
        if (sourceMaterials.Count == 0)
        {
            Debug.LogWarning("Select a model, renderer, prefab, or material before converting to Toon materials.");
            return;
        }

        var shader = Shader.Find("Custom/ToonRampOutline");
        if (shader == null)
        {
            Debug.LogError("Custom/ToonRampOutline shader was not found.");
            return;
        }

        var converted = new Dictionary<Material, Material>();
        foreach (var source in sourceMaterials)
        {
            converted[source] = CreateOrUpdateToonMaterial(source, shader);
        }

        ReplaceSelectedRendererMaterials(converted);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static HashSet<Material> CollectSelectedMaterials()
    {
        var materials = new HashSet<Material>();

        foreach (var selected in Selection.objects)
        {
            if (selected is Material material)
            {
                materials.Add(material);
                continue;
            }

            if (selected is GameObject gameObject)
            {
                foreach (var renderer in gameObject.GetComponentsInChildren<Renderer>(true))
                {
                    foreach (var sharedMaterial in renderer.sharedMaterials)
                    {
                        if (sharedMaterial != null)
                        {
                            materials.Add(sharedMaterial);
                        }
                    }
                }
            }
        }

        return materials;
    }

    private static Material CreateOrUpdateToonMaterial(Material source, Shader shader)
    {
        var safeName = source.name.Replace("/", "_").Replace("\\", "_");
        var path = $"{OutputFolder}/{safeName}_Toon.mat";
        var toon = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (toon == null)
        {
            toon = new Material(shader);
            AssetDatabase.CreateAsset(toon, path);
        }

        toon.shader = shader;
        toon.SetTexture("_MainTex", FindMainTexture(source));
        toon.SetColor("_Color", FindMainColor(source));
        toon.SetColor("_ShadowColor", new Color(0.42f, 0.42f, 0.48f, 1f));
        toon.SetColor("_HighlightColor", Color.white);
        toon.SetColor("_OutlineColor", new Color(0.018f, 0.017f, 0.02f, 1f));
        toon.SetFloat("_OutlineWidth", 0.0065f);
        toon.SetFloat("_RampSteps", 3f);
        toon.SetFloat("_SpecularSize", 0.75f);
        toon.SetFloat("_SpecularStrength", 0.16f);

        EditorUtility.SetDirty(toon);
        Debug.Log($"Created Toon material: {path}");
        return toon;
    }

    private static void ReplaceSelectedRendererMaterials(Dictionary<Material, Material> converted)
    {
        foreach (var selected in Selection.gameObjects)
        {
            foreach (var renderer in selected.GetComponentsInChildren<Renderer>(true))
            {
                var sharedMaterials = renderer.sharedMaterials;
                var changed = false;

                for (var i = 0; i < sharedMaterials.Length; i++)
                {
                    var source = sharedMaterials[i];
                    if (source != null && converted.TryGetValue(source, out var toon))
                    {
                        sharedMaterials[i] = toon;
                        changed = true;
                    }
                }

                if (!changed)
                {
                    continue;
                }

                Undo.RecordObject(renderer, "Apply Toon Materials");
                renderer.sharedMaterials = sharedMaterials;
                EditorUtility.SetDirty(renderer);
            }
        }
    }

    private static Texture FindMainTexture(Material material)
    {
        if (material.HasProperty("_MainTex"))
        {
            return material.GetTexture("_MainTex");
        }

        if (material.HasProperty("_BaseMap"))
        {
            return material.GetTexture("_BaseMap");
        }

        if (material.HasProperty("_BaseColorMap"))
        {
            return material.GetTexture("_BaseColorMap");
        }

        return null;
    }

    private static Color FindMainColor(Material material)
    {
        if (material.HasProperty("_BaseColor"))
        {
            return material.GetColor("_BaseColor");
        }

        if (material.HasProperty("_Color"))
        {
            return material.GetColor("_Color");
        }

        return Color.white;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        var parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
        var folderName = Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
        {
            EnsureFolder(parent);
        }

        AssetDatabase.CreateFolder(parent, folderName);
    }
}
