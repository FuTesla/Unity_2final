using UnityEngine;
using UnityEngine.UI;

public sealed class EnemyHealthBar : MonoBehaviour
{
    public EnemyHealth target;
    public Image fillImage;
    public Camera targetCamera;
    public Color fullColor = new Color(1f, 0.52f, 0.08f, 1f);
    public Color dangerColor = new Color(0.95f, 0.06f, 0.04f, 1f);
    [Range(0f, 1f)] public float dangerThreshold = 0.2f;

    private void LateUpdate()
    {
        if (target == null || fillImage == null)
        {
            return;
        }

        var health01 = target.Health01;
        fillImage.fillAmount = health01;
        var fillRect = fillImage.rectTransform;
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = new Vector2(health01, 1f);
        fillRect.offsetMin = new Vector2(2f, 2f);
        fillRect.offsetMax = new Vector2(-2f, -2f);

        if (health01 < dangerThreshold)
        {
            fillImage.color = dangerColor;
        }
        else
        {
            fillImage.color = fullColor;
        }

        var cameraToFace = targetCamera != null ? targetCamera : Camera.main;
        if (cameraToFace != null)
        {
            transform.rotation = cameraToFace.transform.rotation;
        }
    }
}
