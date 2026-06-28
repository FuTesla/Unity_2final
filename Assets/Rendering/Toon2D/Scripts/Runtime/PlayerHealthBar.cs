using UnityEngine;
using UnityEngine.UI;

public sealed class PlayerHealthBar : MonoBehaviour
{
    public PlayerHealth target;
    public Image fillImage;
    public Color fullColor = Color.white;
    public Color warningColor = new Color(1f, 0.52f, 0.08f, 1f);
    public Color dangerColor = new Color(0.95f, 0.06f, 0.04f, 1f);

    private void Update()
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
        fillRect.offsetMin = new Vector2(4f, 4f);
        fillRect.offsetMax = new Vector2(-4f, -4f);

        if (health01 < 0.3f)
        {
            fillImage.color = dangerColor;
        }
        else if (health01 < 0.6f)
        {
            fillImage.color = warningColor;
        }
        else
        {
            fillImage.color = fullColor;
        }
    }
}
