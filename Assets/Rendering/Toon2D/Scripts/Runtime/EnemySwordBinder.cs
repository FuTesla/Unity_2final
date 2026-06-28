using UnityEngine;

public sealed class EnemySwordBinder : MonoBehaviour
{
    public Transform sourceSword;
    public string targetHandName = "Hand.R";
    public string boundSwordName = "Enemy Sword";
    public Vector3 fallbackLocalPosition = new Vector3(0.02f, 0.08f, 0.02f);
    public Vector3 fallbackLocalEulerAngles = new Vector3(8f, 84f, 174f);
    public Vector3 fallbackLocalScale = Vector3.one;

    private void Awake()
    {
        EnsureSword();
    }

    public void EnsureSword()
    {
        if (sourceSword == null)
        {
            return;
        }

        var targetHand = FindLikelyRightHand(transform, targetHandName);
        if (targetHand == null)
        {
            return;
        }

        var existing = targetHand.Find(boundSwordName);
        if (existing != null)
        {
            existing.localPosition = sourceSword.localPosition;
            existing.localRotation = sourceSword.localRotation;
            existing.localScale = sourceSword.localScale;
            return;
        }

        var sword = Instantiate(sourceSword.gameObject, targetHand);
        sword.name = boundSwordName;
        sword.transform.localPosition = sourceSword.localPosition;
        sword.transform.localRotation = sourceSword.localRotation;
        sword.transform.localScale = sourceSword.localScale;
    }

    public static Transform FindLikelyRightHand(Transform root, string preferredName)
    {
        var preferred = FindDeepChild(root, preferredName);
        if (preferred != null)
        {
            return preferred;
        }

        var candidates = new[]
        {
            "Hand.R",
            "Hand_R",
            "RightHand",
            "mixamorig:RightHand",
            "mixamorigRightHand",
            "R_Hand",
            "hand.R",
            "hand_r"
        };

        foreach (var candidate in candidates)
        {
            var match = FindDeepChild(root, candidate);
            if (match != null)
            {
                return match;
            }
        }

        return FindDeepChildContains(root, "right", "hand") ?? FindDeepChildContains(root, "hand", ".r");
    }

    private static Transform FindDeepChild(Transform root, string childName)
    {
        if (root.name == childName)
        {
            return root;
        }

        foreach (Transform child in root)
        {
            var match = FindDeepChild(child, childName);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    private static Transform FindDeepChildContains(Transform root, string firstToken, string secondToken)
    {
        var normalizedName = root.name.ToLowerInvariant();
        if (normalizedName.Contains(firstToken) && normalizedName.Contains(secondToken))
        {
            return root;
        }

        foreach (Transform child in root)
        {
            var match = FindDeepChildContains(child, firstToken, secondToken);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }
}
