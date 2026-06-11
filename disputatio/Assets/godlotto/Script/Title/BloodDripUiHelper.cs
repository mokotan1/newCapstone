using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Creates lightweight UI primitives for blood drip visuals (no external sprite assets required).
/// </summary>
internal static class BloodDripUiHelper
{
    static Sprite s_whiteSprite;

    public static Sprite WhiteSprite => s_whiteSprite ??= CreateWhiteSprite();

    public static RectTransform CreateChildRect(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        return rect;
    }

    public static Image CreateImage(RectTransform rect, Color color, bool raycastTarget = false)
    {
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = WhiteSprite;
        image.type = Image.Type.Simple;
        image.color = color;
        image.raycastTarget = raycastTarget;
        return image;
    }

    public static CanvasGroup GetOrAddCanvasGroup(GameObject target)
    {
        if (!target.TryGetComponent(out CanvasGroup group))
            group = target.AddComponent<CanvasGroup>();

        return group;
    }

    static Sprite CreateWhiteSprite()
    {
        var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };

        var fill = new Color32(255, 255, 255, 255);
        var pixels = new Color32[16];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = fill;

        texture.SetPixels32(pixels);
        texture.Apply(false, true);

        return Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f);
    }
}
