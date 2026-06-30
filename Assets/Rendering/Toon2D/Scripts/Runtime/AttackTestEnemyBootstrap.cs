using UnityEngine;
using UnityEngine.UI;

public sealed class AttackTestEnemyBootstrap : MonoBehaviour
{
    public string enemyName = "Enemy_Stand";
    public float enemyRadius = 0.35f;
    public float enemyHeight = 1.8f;
    public Vector3 enemyCenter = new Vector3(0f, 0.9f, 0f);
    public Vector3 healthBarOffset = new Vector3(0f, 2.15f, 0f);
    public Vector2 healthBarSize = new Vector2(1.35f, 0.16f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        var bootstrapObject = new GameObject("Attack Test Enemy Bootstrap");
        bootstrapObject.AddComponent<AttackTestEnemyBootstrap>();
    }

    private void Start()
    {
        BindEnemyStand();
        Destroy(gameObject);
    }

    private void BindEnemyStand()
    {
        var enemy = FindExactTransform(enemyName);
        if (enemy == null)
        {
            return;
        }

        var controller = enemy.GetComponent<CharacterController>();
        if (controller == null)
        {
            controller = enemy.gameObject.AddComponent<CharacterController>();
        }

        controller.radius = enemyRadius;
        controller.height = enemyHeight;
        controller.center = enemyCenter;
        controller.stepOffset = 0.35f;
        controller.slopeLimit = 45f;
        controller.skinWidth = 0.04f;

        var health = enemy.GetComponent<EnemyHealth>();
        if (health == null)
        {
            health = enemy.gameObject.AddComponent<EnemyHealth>();
        }

        if (enemy.GetComponentInChildren<EnemyHealthBar>(true) == null)
        {
            CreateHealthBar(enemy, health);
        }
    }

    private static Transform FindExactTransform(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        foreach (var transform in FindObjectsOfType<Transform>())
        {
            if (transform.name == objectName)
            {
                return transform;
            }
        }

        return null;
    }

    private void CreateHealthBar(Transform enemy, EnemyHealth health)
    {
        var canvasObject = new GameObject("Enemy Stand Health Bar", typeof(RectTransform), typeof(Canvas), typeof(EnemyHealthBar));
        canvasObject.transform.SetParent(enemy, false);
        canvasObject.transform.localPosition = healthBarOffset;
        canvasObject.transform.localRotation = Quaternion.identity;
        canvasObject.transform.localScale = Vector3.one * 0.01f;

        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        var canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = healthBarSize * 100f;

        var background = CreateHealthBarImage("Background", canvasRect, new Color(0.1f, 0.08f, 0.06f, 0.78f));
        background.rectTransform.anchorMin = Vector2.zero;
        background.rectTransform.anchorMax = Vector2.one;
        background.rectTransform.offsetMin = Vector2.zero;
        background.rectTransform.offsetMax = Vector2.zero;

        var fill = CreateHealthBarImage("Fill", canvasRect, new Color(1f, 0.52f, 0.08f, 1f));
        fill.rectTransform.anchorMin = Vector2.zero;
        fill.rectTransform.anchorMax = Vector2.one;
        fill.rectTransform.offsetMin = new Vector2(2f, 2f);
        fill.rectTransform.offsetMax = new Vector2(-2f, -2f);

        var healthBar = canvasObject.GetComponent<EnemyHealthBar>();
        healthBar.target = health;
        healthBar.fillImage = fill;
        healthBar.targetCamera = Camera.main;
    }

    private static Image CreateHealthBarImage(string name, RectTransform parent, Color color)
    {
        var imageObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.transform.SetParent(parent, false);

        var image = imageObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }
}
