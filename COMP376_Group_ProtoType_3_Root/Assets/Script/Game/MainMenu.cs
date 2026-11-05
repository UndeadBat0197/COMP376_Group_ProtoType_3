using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static UIKit;

/// <summary>
/// Title screen: Play, How to play, Quit, plus your best time.
/// Put it on one GameObject in an otherwise empty scene (Tools > Cheese Game > Create Main Menu Scene
/// does this). The whole screen is built in code, in the same style as the in-game HUD.
/// Keys: Enter = play, H = how to play, Esc = back / quit.
/// </summary>
public class MainMenu : MonoBehaviour
{
    public string gameTitle = "Cheese Hunt";
    public string tagline = "You're a mouse who can turn into a human. Find every piece of cheese.";
    [Tooltip("Scene loaded by Play. It must be in the build list (the setup menu adds it).")]
    public string gameScene = "Game";

    RectTransform root;
    CanvasGroup mainGroup, howGroup;
    bool showingHow;

    void Start()
    {
        Time.timeScale = 1f;
        GameState.InputBlocked = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        EnsureEventSystem();
        root = MakeCanvas(transform, "Main Menu UI", 0);
        BuildBackground();
        BuildMain();
        BuildHowTo();
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        FadeGroup(mainGroup, !showingHow, dt);
        FadeGroup(howGroup, showingHow, dt);

        if (showingHow)
        {
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.H)) showingHow = false;
            else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) Play();
            return;
        }
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) Play();
        else if (Input.GetKeyDown(KeyCode.H)) showingHow = true;
        else if (Input.GetKeyDown(KeyCode.Escape)) Quit();
    }

    // ------------------------------------------------------------ actions

    public void Play()
    {
        if (!Application.CanStreamedLevelBeLoaded(gameScene))
        {
            Debug.LogError($"[MainMenu] Scene '{gameScene}' isn't in the build list. Run Tools > Cheese Game > Set Up Everything, " +
                           "or add it in File > Build Profiles > Scene List.");
            return;
        }
        SceneManager.LoadScene(gameScene);
    }

    public void ShowHowTo() => showingHow = true;
    public void HideHowTo() => showingHow = false;

    public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ------------------------------------------------------------ build

    void BuildBackground()
    {
        Image bg = NewImage(root, "Background", null, new Color(0.045f, 0.055f, 0.08f, 1f));
        bg.type = Image.Type.Simple;
        Fill(bg.rectTransform);

        // Two soft glows in the form colours.
        AddGlow(new Vector2(1f, 1f), new Vector2(-160, -120), 1300f, new Color(Accent.r, Accent.g, Accent.b, 0.16f));
        AddGlow(new Vector2(0f, 0f), new Vector2(100, 60), 1100f, new Color(MouseColor.r, MouseColor.g, MouseColor.b, 0.12f));
    }

    void AddGlow(Vector2 corner, Vector2 offset, float size, Color color)
    {
        Image g = NewImage(root, "Glow", UIKit.Glow, color);
        g.type = Image.Type.Simple;
        RectTransform r = g.rectTransform;
        r.anchorMin = r.anchorMax = corner;
        r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = offset;
        r.sizeDelta = new Vector2(size, size);
    }

    void BuildMain()
    {
        RectTransform page = Stretch(root, "Main");
        mainGroup = page.gameObject.AddComponent<CanvasGroup>();

        // Left column: title, tagline, buttons.
        RectTransform col = NewRect(page, "Column", new Vector2(0f, 0.5f), new Vector2(170, 40), new Vector2(760, 640));

        Place(Label(col, gameTitle, 112, Accent, FontStyles.Bold, TextAlignmentOptions.TopLeft), new Vector2(0, 0), new Vector2(760, 130));
        TMP_Text tag = Label(col, tagline, 28, Muted, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        Place(tag, new Vector2(4, -140), new Vector2(640, 80));

        MenuButton(col, "PLAY", Accent, AccentDark, -260, Play);
        MenuButton(col, "HOW TO PLAY", ButtonSoft, TextColor, -348, ShowHowTo);
        MenuButton(col, "QUIT", ButtonSoft, TextColor, -436, Quit);

        float best = PlayerPrefs.GetFloat("CheeseHunt_BestTime_" + gameScene, 0f);
        if (best > 0f)
            Place(Label(col, $"Best time  <color={AccentHex}><b>{CheeseGameManager.FormatTime(best)}</b></color>", 22, Muted,
                        FontStyles.Normal, TextAlignmentOptions.TopLeft), new Vector2(4, -530), new Vector2(400, 30));

        // Right: the two forms at a glance.
        RectTransform forms = NewRect(page, "Forms", new Vector2(1f, 0.5f), new Vector2(-170, 20), new Vector2(560, 470));
        forms.pivot = new Vector2(1f, 0.5f);
        FormCard(forms, "MOUSE", MouseColor, "Sniffs out cheese\nSqueezes through holes\nClimbs walls\n<b>The only one who can eat</b>", 0);
        FormCard(forms, "HUMAN", HumanColor, "Opens doors and lids\nPushes and pulls furniture\nPicks up and throws things\nCan't fit through holes", -250);
        TMP_Text swap = Label(forms, $"Hold {K("T")} to switch", 22, Muted, FontStyles.Normal, TextAlignmentOptions.Center);
        RectTransform sr = swap.rectTransform;
        sr.anchorMin = sr.anchorMax = new Vector2(0.5f, 1f);
        sr.pivot = new Vector2(0.5f, 0.5f);
        sr.anchoredPosition = new Vector2(0, -232);
        sr.sizeDelta = new Vector2(400, 30);

        // Footer
        TMP_Text keys = Label(page, $"{K("Enter")}  Play      {K("H")}  How to play      {K("Esc")}  Quit", 20, Muted,
                              FontStyles.Normal, TextAlignmentOptions.BottomLeft);
        RectTransform kr = keys.rectTransform;
        kr.anchorMin = kr.anchorMax = kr.pivot = new Vector2(0f, 0f);
        kr.anchoredPosition = new Vector2(174, 56);
        kr.sizeDelta = new Vector2(900, 30);
    }

    void FormCard(RectTransform parent, string title, Color color, string body, float y)
    {
        Image card = NewImage(parent, title, Rounded, PanelColor);
        Place(card.rectTransform, new Vector2(0, y), new Vector2(560, 210));

        Image badge = NewImage(card.rectTransform, "Badge", Pill, new Color(color.r, color.g, color.b, 0.22f));
        Place(badge.rectTransform, new Vector2(28, -26), new Vector2(150, 44));
        TMP_Text bt = Label(badge.rectTransform, title, 22, Color.Lerp(color, Color.white, 0.35f), FontStyles.Bold,
                            TextAlignmentOptions.Center, 3f);
        Fill(bt.rectTransform);

        TMP_Text t = Label(card.rectTransform, body, 21, TextColor, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        t.lineSpacing = 8f;
        Place(t, new Vector2(210, -24), new Vector2(330, 170));
    }

    void MenuButton(RectTransform parent, string label, Color bg, Color fg, float y, Action onClick)
    {
        Button b = MakeButton(parent, label, bg, fg, new Vector2(380, 70), onClick);
        Place(b, new Vector2(0, y), new Vector2(380, 70));
    }

    void BuildHowTo()
    {
        RectTransform card = Overlay(root, "How To Play", new Vector2(1040, 700), out howGroup);
        Place(Label(card, "HOW TO PLAY", 48, TextColor, FontStyles.Bold, TextAlignmentOptions.Top), new Vector2(0, -36), new Vector2(960, 60), center: true);

        Section(card, "HOW TO WIN", new Vector2(56, -120));
        TMP_Text goal = Label(card, GoalText(-1), 21, TextColor, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        goal.lineSpacing = 18f;
        Place(goal, new Vector2(56, -150), new Vector2(928, 130));

        Section(card, "CONTROLS", new Vector2(56, -300));
        ControlsColumns(card, new Vector2(56, -332), 928);

        ButtonRow(card, 44,
            ("BACK  (Esc)", ButtonSoft, TextColor, (Action)HideHowTo),
            ("PLAY  (Enter)", Accent, AccentDark, (Action)Play));
    }

    // ------------------------------------------------------------ helpers

    static RectTransform NewRect(RectTransform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform r = (RectTransform)go.transform;
        r.anchorMin = r.anchorMax = anchor;
        r.pivot = new Vector2(0f, 0.5f);
        r.anchoredPosition = pos;
        r.sizeDelta = size;
        return r;
    }

    static void FadeGroup(CanvasGroup g, bool show, float dt)
    {
        g.alpha = Mathf.MoveTowards(g.alpha, show ? 1f : 0f, dt * 6f);
        g.interactable = g.blocksRaycasts = show && g.alpha > 0.5f;
    }
}
