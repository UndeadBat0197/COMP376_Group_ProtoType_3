using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Shared look for every screen (main menu, HUD, pause, win): colours, rounded panels,
/// text, buttons and the controls card. Everything is made in code, so no prefabs or sprites
/// need importing. Change a colour here and every screen follows.
/// </summary>
public static class UIKit
{
    // ---- palette
    public static readonly Color PanelColor = new Color(0.07f, 0.09f, 0.12f, 0.88f);
    public static readonly Color PanelSoft = new Color(1f, 1f, 1f, 0.06f);
    public static readonly Color Accent = new Color(1f, 0.79f, 0.24f);
    public static readonly Color AccentDark = new Color(0.23f, 0.17f, 0.03f);
    public static readonly Color TextColor = new Color(0.96f, 0.97f, 0.98f);
    public static readonly Color Muted = new Color(0.66f, 0.71f, 0.78f);
    public static readonly Color MouseColor = new Color(0.56f, 0.72f, 0.98f);
    public static readonly Color HumanColor = new Color(0.99f, 0.63f, 0.43f);
    public static readonly Color PipEmpty = new Color(1f, 1f, 1f, 0.12f);
    public static readonly Color ButtonSoft = new Color(1f, 1f, 1f, 0.14f);
    public const string AccentHex = "#FFC93C", MutedHex = "#A9B3C1";

    static Sprite rounded, pill, dot;
    public static Sprite Rounded => rounded != null ? rounded : (rounded = MakeRounded(64, 18));
    public static Sprite Pill => pill != null ? pill : (pill = MakeRounded(64, 30));
    public static Sprite Dot => dot != null ? dot : (dot = MakeRounded(32, 16));
    static Sprite glow;
    /// <summary>Soft radial gradient (white centre fading to clear), for background glows.</summary>
    public static Sprite Glow => glow != null ? glow : (glow = MakeGlow(128));

    static Sprite MakeGlow(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        Color32[] px = new Color32[size * size];
        float half = size / 2f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(half, half)) / half;
                float a = Mathf.Clamp01(1f - d);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * a * 255f));
            }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    /// <summary>A screen-space canvas scaled for 1920x1080, drawn above the scene's own UI.</summary>
    public static RectTransform MakeCanvas(Transform parent, string name, int sortingOrder)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        return (RectTransform)go.transform;
    }

    public static void ControlsColumns(RectTransform card, Vector2 topLeft, float width)
    {
        float colW = (width - 40f) / 3f;

        ControlsColumn(card, "MOUSE", MouseColor, topLeft, colW,
            $"{K("WASD")}  Move\n{K("Shift")}  Sprint\n{K("Space")}  Jump\n{K("Shift")} on a wall  Climb\n{K("E")}  Eat cheese");
        ControlsColumn(card, "HUMAN", HumanColor, topLeft + new Vector2(colW + 20f, 0), colW,
            $"<color={MutedHex}>Moves like the mouse</color>\n{K("E")}  Use / open / pick up\n{K("F")}  Grab furniture\n{K("W")} / {K("S")}  Push / pull\n{K("Left click")}  Throw");
        ControlsColumn(card, "ANY FORM", TextColor, topLeft + new Vector2(2f * (colW + 20f), 0), colW,
            $"Hold {K("T")}  Form wheel:\npoint at a form,\nrelease to transform\n{K("Esc")}  Pause");
    }

    /// <summary>The "how to win" paragraph. count &lt; 0 when the number of cheese isn't known (main menu).</summary>
    public static string GoalText(int count)
    {
        string what = count > 0 ? $"all <b><color={AccentHex}>{count}</color></b> pieces of cheese" : "every piece of cheese";
        return $"Eat {what}. <b>Only the mouse can eat.</b>\n" +
               "Follow your nose: the smell meter fills up as you get closer.\n" +
               "Cheese hidden behind or under furniture needs the <b>human</b>: open, push, pull or carry things out of the way, then switch back.";
    }

    public static string K(string key) => $"<color={AccentHex}><b>{key}</b></color>";

    public static void ControlsColumn(RectTransform card, string title, Color color, Vector2 pos, float w, string body)
    {
        Image bg = NewImage(card, title, Rounded, PanelSoft);
        Place(bg.rectTransform, pos, new Vector2(w, 214));
        Place(Label(bg.rectTransform, title, 17, color, FontStyles.Bold, TextAlignmentOptions.TopLeft, 4f), new Vector2(20, -16), new Vector2(w - 40, 24));
        TMP_Text t = Label(bg.rectTransform, body, 19, TextColor, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        t.lineSpacing = 10f;
        Place(t, new Vector2(20, -48), new Vector2(w - 34, 160));
    }

    public static void Section(RectTransform card, string text, Vector2 pos)
    {
        Place(Label(card, text, 17, Accent, FontStyles.Bold, TextAlignmentOptions.TopLeft, 4f), pos, new Vector2(400, 24));
    }

    public static void ButtonRow(RectTransform card, float bottom, params (string label, Color bg, Color fg, Action onClick)[] buttons)
    {
        const float w = 250f, gap = 20f;
        float total = buttons.Length * w + (buttons.Length - 1) * gap;
        for (int i = 0; i < buttons.Length; i++)
        {
            Button b = MakeButton(card, buttons[i].label, buttons[i].bg, buttons[i].fg, new Vector2(w, 60), buttons[i].onClick);
            RectTransform r = (RectTransform)b.transform;
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0f);
            r.pivot = new Vector2(0f, 0f);
            r.anchoredPosition = new Vector2(-total / 2f + i * (w + gap), bottom);
        }
    }

    public static RectTransform Overlay(RectTransform root, string name, Vector2 cardSize, out CanvasGroup group)
    {
        RectTransform layer = Stretch(root, name);
        Image dim = layer.gameObject.AddComponent<Image>();
        dim.color = new Color(0.02f, 0.03f, 0.05f, 0.66f);
        group = layer.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;

        Image card = NewImage(layer, "Card", Rounded, new Color(0.08f, 0.1f, 0.14f, 0.97f));
        card.pixelsPerUnitMultiplier = 0.6f; // softer, larger corners
        RectTransform r = card.rectTransform;
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
        r.sizeDelta = cardSize;
        return r;
    }

    public static void SetText(TMP_Text t, string s)
    {
        if (t != null && t.text != s) t.text = s;
    }

    public static TMP_Text Label(Transform parent, string text, float size, Color color, FontStyles style, TextAlignmentOptions align, float spacing = 0f)
    {
        GameObject go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.fontStyle = style;
        t.alignment = align;
        t.characterSpacing = spacing;
        t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.richText = true;
        return t;
    }

    public static Image NewImage(Transform parent, string name, Sprite sprite, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Image img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.type = Image.Type.Sliced;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    public static RectTransform Panel(RectTransform parent, string name, Vector2 corner, Vector2 pos, Vector2 size, Color color)
    {
        Image img = NewImage(parent, name, Rounded, color);
        RectTransform r = img.rectTransform;
        r.anchorMin = r.anchorMax = r.pivot = corner;
        r.anchoredPosition = pos;
        r.sizeDelta = size;
        return r;
    }

    // Top-left placement inside a parent (or top-centre / top-right).
    public static void Place(Component c, Vector2 pos, Vector2 size, bool center = false, bool right = false)
    {
        RectTransform r = (RectTransform)c.transform;
        Vector2 a = center ? new Vector2(0.5f, 1f) : right ? new Vector2(1f, 1f) : new Vector2(0f, 1f);
        r.anchorMin = r.anchorMax = r.pivot = a;
        r.anchoredPosition = pos;
        r.sizeDelta = size;
    }

    public static RectTransform Stretch(RectTransform parent, string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform r = (RectTransform)go.transform;
        Fill(r);
        return r;
    }

    public static void Fill(RectTransform r)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
    }

    public static Button MakeButton(RectTransform parent, string label, Color bg, Color fg, Vector2 size, Action onClick)
    {
        Image img = NewImage(parent, label, Pill, Color.white);
        img.raycastTarget = true;
        img.rectTransform.sizeDelta = size;
        Button b = img.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        ColorBlock cb = b.colors;
        cb.normalColor = bg;
        cb.highlightedColor = Color.Lerp(bg, Color.white, 0.25f);
        cb.pressedColor = Color.Lerp(bg, Color.black, 0.15f);
        cb.selectedColor = bg;
        cb.colorMultiplier = 1f;
        cb.fadeDuration = 0.08f;
        b.colors = cb;
        Navigation nav = b.navigation;
        nav.mode = Navigation.Mode.None;
        b.navigation = nav;
        b.onClick.AddListener(() => onClick());

        TMP_Text t = Label(img.transform, label, 22, fg, FontStyles.Bold, TextAlignmentOptions.Center, 2f);
        t.textWrappingMode = TextWrappingModes.NoWrap;
        Fill(t.rectTransform);
        return b;
    }

    // Anti-aliased Rounded rectangle, 9-sliced so it scales to any size.
    public static Sprite MakeRounded(int size, int radius)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        Color32[] px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
                float cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                byte a = (byte)(Mathf.Clamp01(radius - d + 0.5f) * 255f);
                px[y * size + x] = new Color32(255, 255, 255, a);
            }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        float b = Mathf.Min(radius + 1, size / 2 - 1);
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                             SpriteMeshType.FullRect, new Vector4(b, b, b, b));
    }

    public static void EnsureEventSystem()
    {
        if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) return;
        GameObject go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        Type inputSystemModule = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (inputSystemModule != null) go.AddComponent(inputSystemModule);
        else go.AddComponent<StandaloneInputModule>();
    }
}
