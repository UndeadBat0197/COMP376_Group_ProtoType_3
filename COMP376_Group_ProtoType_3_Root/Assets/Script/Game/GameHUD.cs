using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static UIKit;

/// <summary>
/// The whole game UI, built in code (no prefabs to wire up):
///  - objective card with cheese counter, score + timer, current form badge
///  - context prompts with key caps (mouse: eat; human: use / grab / carry)
///  - toasts, "+100" popups, crosshair for the human
///  - intro (goal + controls), pause and win screens
/// Added automatically by CheeseGameManager. The smell meter stays the team's own UI (top centre).
/// </summary>
[DefaultExecutionOrder(100)]
public class GameHUD : MonoBehaviour
{

    CheeseGameManager gm;
    RectTransform root;

    // HUD
    CanvasGroup hud;
    TMP_Text countText, scoreText, timeText, formText;
    Image formBadge;
    readonly List<Image> pips = new List<Image>();
    RectTransform countRect;
    Image crosshair;

    // prompt
    GameObject promptTitleGO, promptPanelGO, promptHintGO;
    TMP_Text promptTitle, promptHint;
    readonly List<(GameObject go, TMP_Text key, TMP_Text label)> actionItems = new List<(GameObject, TMP_Text, TMP_Text)>();
    readonly List<HumanInteractor.PromptAction> actionBuffer = new List<HumanInteractor.PromptAction>();

    // toast + popups
    CanvasGroup toastGroup;
    TMP_Text toastText;
    float toastUntil;
    string lastInteractorMessage;
    readonly List<(TMP_Text text, float born, Vector2 start)> popups = new List<(TMP_Text, float, Vector2)>();

    // overlays
    CanvasGroup introGroup, pauseGroup, winGroup;
    TMP_Text pauseStats, winTitle, winStats, winScore;

    float shownScore;
    float countPunch;
    bool built;

    void Start()
    {
        gm = CheeseGameManager.Instance != null ? CheeseGameManager.Instance : GetComponent<CheeseGameManager>();
        if (gm == null) { enabled = false; return; }

        EnsureEventSystem();
        BuildCanvas();
        BuildHud();
        BuildPrompt();
        BuildToast();
        BuildIntro();
        BuildPause();
        BuildWin();

        gm.CheeseEaten += OnCheeseEaten;
        gm.Toast += ShowToast;
        built = true;
    }

    void OnDestroy()
    {
        if (gm == null) return;
        gm.CheeseEaten -= OnCheeseEaten;
        gm.Toast -= ShowToast;
    }

    // ================================================================ per frame

    void Update()
    {
        if (!built) return;
        float dt = Time.unscaledDeltaTime;
        var phase = gm.CurrentPhase;

        Fade(introGroup, phase == CheeseGameManager.Phase.Intro, dt);
        Fade(pauseGroup, phase == CheeseGameManager.Phase.Paused, dt);
        Fade(winGroup, phase == CheeseGameManager.Phase.Won, dt);
        Fade(hud, phase == CheeseGameManager.Phase.Playing || phase == CheeseGameManager.Phase.Paused, dt);

        // Objective + stats
        SetText(countText, $"{gm.EatenCheese} / {gm.TotalCheese}");
        for (int i = 0; i < pips.Count; i++) pips[i].color = i < gm.EatenCheese ? Accent : PipEmpty;
        shownScore = Mathf.MoveTowards(shownScore, gm.CheesePoints, Mathf.Max(200f, gm.CheesePoints - shownScore) * 4f * dt);
        SetText(scoreText, Mathf.RoundToInt(shownScore).ToString());
        SetText(timeText, CheeseGameManager.FormatTime(gm.ElapsedTime));
        countPunch = Mathf.MoveTowards(countPunch, 0f, dt * 3f);
        countRect.localScale = Vector3.one * (1f + 0.35f * countPunch);

        bool human = gm.IsHuman;
        SetText(formText, human ? $"<b>HUMAN</b>   <size=75%><color={MutedHex}>Hold</color> T <color={MutedHex}>to transform</color></size>"
                                : $"<b>MOUSE</b>   <size=75%><color={MutedHex}>Hold</color> T <color={MutedHex}>to transform</color></size>");
        Color fc = human ? HumanColor : MouseColor;
        formBadge.color = new Color(fc.r, fc.g, fc.b, 0.22f);
        formText.color = Color.Lerp(fc, Color.white, 0.35f);

        UpdatePrompt(phase == CheeseGameManager.Phase.Playing);
        crosshair.enabled = phase == CheeseGameManager.Phase.Playing && human && gm.Interactor != null && gm.Interactor.ShowCrosshair;

        // Toasts from the human's own messages ("Dropped the Rock", "The crate is stuck", ...)
        string m = gm.Interactor != null && gm.Interactor.isActiveAndEnabled ? gm.Interactor.ActiveMessage : null;
        if (m != null && m != lastInteractorMessage) ShowToast(m);
        lastInteractorMessage = m;
        toastGroup.alpha = Mathf.MoveTowards(toastGroup.alpha, Time.unscaledTime < toastUntil ? 1f : 0f, dt * 5f);

        UpdatePopups();

        if (phase == CheeseGameManager.Phase.Paused)
            SetText(pauseStats, $"Cheese  <b>{gm.EatenCheese} / {gm.TotalCheese}</b>      Time  <b>{CheeseGameManager.FormatTime(gm.ElapsedTime)}</b>      Score  <b>{gm.CheesePoints}</b>");
        if (phase == CheeseGameManager.Phase.Won) FillWin();
    }

    void UpdatePrompt(bool playing)
    {
        actionBuffer.Clear();
        string title = null, hint = null;

        if (playing)
        {
            if (gm.IsHuman)
            {
                HumanInteractor hi = gm.Interactor;
                if (hi != null && hi.isActiveAndEnabled)
                {
                    hi.GetPrompt(actionBuffer, out title);
                    bool cheeseInHand = hi.Held != null && hi.Held.GetComponent<Cheese>() != null;
                    bool cheeseTarget = hi.Current != null && hi.Current.GetComponent<Cheese>() != null;
                    if (cheeseInHand) hint = "Only the mouse can eat. Put it where the mouse can reach, then hold T";
                    else if (cheeseTarget) hint = "Only the mouse can eat cheese";
                }
            }
            else if (gm.Eater != null && gm.Eater.isActiveAndEnabled)
            {
                if (gm.Eater.Target != null)
                    actionBuffer.Add(new HumanInteractor.PromptAction(gm.Eater.eatKey.ToString(), "Eat cheese"));
                else if (gm.Eater.Blocked != null)
                    hint = "Cheese is close, but something's in the way. A human could move it. Hold T";
            }
        }

        promptTitleGO.SetActive(title != null);
        if (title != null) SetText(promptTitle, title);
        promptHintGO.SetActive(hint != null);
        if (hint != null) SetText(promptHint, hint);

        promptPanelGO.SetActive(actionBuffer.Count > 0);
        for (int i = 0; i < actionItems.Count; i++)
        {
            bool on = i < actionBuffer.Count;
            actionItems[i].go.SetActive(on);
            if (!on) continue;
            SetText(actionItems[i].key, actionBuffer[i].key);
            SetText(actionItems[i].label, actionBuffer[i].label);
        }
    }

    void FillWin()
    {
        string best = gm.NewBest ? $"<color={AccentHex}>New best!</color>" : $"<color={MutedHex}>Best {CheeseGameManager.FormatTime(gm.BestTime)}</color>";
        SetText(winStats,
            $"Cheese eaten<pos=62%><b>{gm.EatenCheese} / {gm.TotalCheese}</b><pos=80%><b>{gm.CheesePoints}</b>\n" +
            $"Time<pos=62%><b>{CheeseGameManager.FormatTime(gm.ElapsedTime)}</b>  {best}\n" +
            $"Time bonus<pos=62%><color={MutedHex}>under {CheeseGameManager.FormatTime(gm.parTimeSeconds)}</color><pos=80%><b>+{gm.TimeBonus}</b>");
        SetText(winScore, $"<size=50%><color={MutedHex}>FINAL SCORE</color></size>\n{gm.Score}");
    }

    // ================================================================ events

    void OnCheeseEaten(Vector3 worldPos, int points)
    {
        countPunch = 1f;
        Camera cam = Camera.main;
        Vector2 screen = new Vector2(Screen.width * 0.5f, Screen.height * 0.55f);
        if (cam != null)
        {
            Vector3 sp = cam.WorldToScreenPoint(worldPos + Vector3.up * 0.15f);
            if (sp.z > 0f) screen = sp;
        }
        RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, null, out Vector2 local);

        TMP_Text t = null;
        foreach (var p in popups) if (!p.text.gameObject.activeSelf) { t = p.text; break; }
        if (t == null)
        {
            t = Label(root, "", 40, Accent, FontStyles.Bold, TextAlignmentOptions.Center);
            t.rectTransform.sizeDelta = new Vector2(240, 60);
            t.outlineWidth = 0.18f;
            t.outlineColor = new Color32(40, 28, 0, 255);
        }
        popups.RemoveAll(p => p.text == t);
        t.text = "+" + points;
        t.gameObject.SetActive(true);
        popups.Add((t, Time.unscaledTime, local));
    }

    void UpdatePopups()
    {
        foreach (var p in popups)
        {
            if (!p.text.gameObject.activeSelf) continue;
            float k = (Time.unscaledTime - p.born) / 1.1f;
            if (k >= 1f) { p.text.gameObject.SetActive(false); continue; }
            float ease = 1f - (1f - k) * (1f - k);
            p.text.rectTransform.anchoredPosition = p.start + Vector2.up * (110f * ease);
            p.text.alpha = k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f;
            p.text.rectTransform.localScale = Vector3.one * (k < 0.15f ? 0.6f + 0.6f * (k / 0.15f) : 1.2f - 0.2f * Mathf.Min(1f, (k - 0.15f) / 0.2f));
        }
    }

    void ShowToast(string text)
    {
        if (toastText == null) return;
        toastText.text = text;
        toastUntil = Time.unscaledTime + 2.2f;
    }

    // ================================================================ build: HUD

    void BuildCanvas()
    {
        root = MakeCanvas(transform, "Game UI", 50);
    }

    void BuildHud()
    {
        RectTransform hudRect = Stretch(root, "HUD");
        hud = hudRect.gameObject.AddComponent<CanvasGroup>();
        hud.alpha = 0f;
        hud.blocksRaycasts = false;

        // --- Objective card (top left)
        RectTransform card = Panel(hudRect, "Objective", new Vector2(0, 1), new Vector2(32, -32), new Vector2(460, 190), PanelColor);
        Place(Label(card, "OBJECTIVE", 17, Accent, FontStyles.Bold, TextAlignmentOptions.TopLeft, 4f), new Vector2(26, -20), new Vector2(400, 24));
        Place(Label(card, "Eat all the cheese", 32, TextColor, FontStyles.Bold, TextAlignmentOptions.TopLeft), new Vector2(26, -44), new Vector2(410, 42));
        Place(Label(card, "Only the mouse can eat. Hidden cheese needs the human's help.", 18, Muted, FontStyles.Normal, TextAlignmentOptions.TopLeft), new Vector2(26, -86), new Vector2(410, 48));

        int n = Mathf.Max(1, gm.TotalCheese);
        float pipSize = Mathf.Clamp(260f / n - 8f, 12f, 26f);
        for (int i = 0; i < n; i++)
        {
            Image pip = NewImage(card, "Pip", Pill, PipEmpty);
            Place(pip.rectTransform, new Vector2(26 + i * (pipSize + 8f), -146), new Vector2(pipSize, pipSize));
            pips.Add(pip);
        }
        countText = Label(card, "0 / 0", 26, TextColor, FontStyles.Bold, TextAlignmentOptions.Right);
        countRect = countText.rectTransform;
        countRect.anchorMin = countRect.anchorMax = new Vector2(1, 1);
        countRect.pivot = new Vector2(1, 0.5f);
        countRect.anchoredPosition = new Vector2(-26, -146 - pipSize / 2f);
        countRect.sizeDelta = new Vector2(130, 34);

        // --- Score + time (top right)
        RectTransform stats = Panel(hudRect, "Stats", new Vector2(1, 1), new Vector2(-32, -32), new Vector2(330, 100), PanelColor);
        Place(Label(stats, "TIME", 17, Muted, FontStyles.Bold, TextAlignmentOptions.TopLeft, 4f), new Vector2(24, -18), new Vector2(120, 24));
        timeText = Label(stats, "00:00", 34, TextColor, FontStyles.Bold, TextAlignmentOptions.TopLeft);
        Place(timeText, new Vector2(24, -42), new Vector2(140, 44));
        Place(Label(stats, "SCORE", 17, Muted, FontStyles.Bold, TextAlignmentOptions.TopRight, 4f), new Vector2(-24, -18), new Vector2(160, 24), right: true);
        scoreText = Label(stats, "0", 40, Accent, FontStyles.Bold, TextAlignmentOptions.TopRight);
        Place(scoreText, new Vector2(-24, -38), new Vector2(170, 50), right: true);

        // --- Form badge (under stats)
        formBadge = NewImage(hudRect, "Form", Pill, PanelSoft);
        RectTransform fb = formBadge.rectTransform;
        fb.anchorMin = fb.anchorMax = new Vector2(1, 1);
        fb.pivot = new Vector2(1, 1);
        fb.anchoredPosition = new Vector2(-32, -146);
        fb.sizeDelta = new Vector2(330, 52);
        formText = Label(fb, "", 22, TextColor, FontStyles.Normal, TextAlignmentOptions.Center);
        Fill(formText.rectTransform);

        // --- Crosshair
        crosshair = NewImage(hudRect, "Crosshair", Dot, new Color(1f, 1f, 1f, 0.85f));
        crosshair.rectTransform.anchorMin = crosshair.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        crosshair.rectTransform.sizeDelta = new Vector2(8, 8);
        crosshair.enabled = false;
    }

    void BuildPrompt()
    {
        // Bottom-centre column: title / key-cap row / hint. Sizes itself to its contents.
        GameObject col = new GameObject("Prompt", typeof(RectTransform));
        col.transform.SetParent(hud.transform, false);
        RectTransform cr = (RectTransform)col.transform;
        cr.anchorMin = cr.anchorMax = new Vector2(0.5f, 0f);
        cr.pivot = new Vector2(0.5f, 0f);
        cr.anchoredPosition = new Vector2(0, 64);
        VerticalLayoutGroup v = col.AddComponent<VerticalLayoutGroup>();
        v.childAlignment = TextAnchor.LowerCenter;
        v.spacing = 10;
        v.childControlWidth = v.childControlHeight = true;
        v.childForceExpandWidth = v.childForceExpandHeight = false;
        ContentSizeFitter f = col.AddComponent<ContentSizeFitter>();
        f.horizontalFit = f.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        promptTitle = Label(cr, "", 22, Muted, FontStyles.Bold, TextAlignmentOptions.Center);
        promptTitle.textWrappingMode = TextWrappingModes.NoWrap;
        promptTitleGO = promptTitle.gameObject;

        Image panel = NewImage(cr, "Actions", Pill, PanelColor);
        promptPanelGO = panel.gameObject;
        HorizontalLayoutGroup h = promptPanelGO.AddComponent<HorizontalLayoutGroup>();
        h.padding = new RectOffset(16, 26, 10, 10);
        h.spacing = 30;
        h.childAlignment = TextAnchor.MiddleCenter;
        h.childControlWidth = h.childControlHeight = true;
        h.childForceExpandWidth = h.childForceExpandHeight = false;

        for (int i = 0; i < 4; i++)
        {
            GameObject item = new GameObject("Action", typeof(RectTransform));
            item.transform.SetParent(panel.transform, false);
            HorizontalLayoutGroup ih = item.AddComponent<HorizontalLayoutGroup>();
            ih.spacing = 12;
            ih.childAlignment = TextAnchor.MiddleLeft;
            ih.childControlWidth = ih.childControlHeight = true;
            ih.childForceExpandWidth = ih.childForceExpandHeight = false;

            Image cap = NewImage(item.transform, "Key", Rounded, new Color(1f, 1f, 1f, 0.94f));
            cap.pixelsPerUnitMultiplier = 2f;
            HorizontalLayoutGroup ch = cap.gameObject.AddComponent<HorizontalLayoutGroup>();
            ch.padding = new RectOffset(12, 12, 5, 5);
            ch.childAlignment = TextAnchor.MiddleCenter;
            ch.childControlWidth = ch.childControlHeight = true;
            ch.childForceExpandWidth = ch.childForceExpandHeight = false;
            LayoutElement le = cap.gameObject.AddComponent<LayoutElement>();
            le.minWidth = 42;
            le.minHeight = 42;
            TMP_Text key = Label(cap.transform, "E", 20, new Color(0.08f, 0.09f, 0.11f), FontStyles.Bold, TextAlignmentOptions.Center);
            key.textWrappingMode = TextWrappingModes.NoWrap;

            TMP_Text label = Label(item.transform, "", 24, TextColor, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            actionItems.Add((item, key, label));
        }

        promptHint = Label(cr, "", 20, Accent, FontStyles.Normal, TextAlignmentOptions.Center);
        promptHint.textWrappingMode = TextWrappingModes.NoWrap;
        promptHintGO = promptHint.gameObject;

        promptTitleGO.SetActive(false);
        promptPanelGO.SetActive(false);
        promptHintGO.SetActive(false);
    }

    void BuildToast()
    {
        // Sits under the team's smell meter (top centre).
        Image bg = NewImage(hud.transform, "Toast", Pill, PanelColor);
        RectTransform r = bg.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 1f);
        r.pivot = new Vector2(0.5f, 1f);
        r.anchoredPosition = new Vector2(0, -136);
        HorizontalLayoutGroup h = bg.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.padding = new RectOffset(30, 30, 12, 12);
        h.childControlWidth = h.childControlHeight = true;
        h.childForceExpandWidth = h.childForceExpandHeight = false;
        ContentSizeFitter f = bg.gameObject.AddComponent<ContentSizeFitter>();
        f.horizontalFit = f.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        toastText = Label(r, "", 24, TextColor, FontStyles.Bold, TextAlignmentOptions.Center);
        toastText.textWrappingMode = TextWrappingModes.NoWrap;
        toastGroup = bg.gameObject.AddComponent<CanvasGroup>();
        toastGroup.alpha = 0f;
        toastGroup.blocksRaycasts = false;
    }

    // ================================================================ build: screens

    void BuildIntro()
    {
        RectTransform card = Overlay(root, "Intro", new Vector2(1040, 750), out introGroup);

        Place(Label(card, gm.gameTitle, 60, Accent, FontStyles.Bold, TextAlignmentOptions.Top), new Vector2(0, -38), new Vector2(960, 72), center: true);
        Place(Label(card, "You're a mouse who can turn into a human.", 24, Muted, FontStyles.Normal, TextAlignmentOptions.Top), new Vector2(0, -112), new Vector2(960, 34), center: true);

        Section(card, "HOW TO WIN", new Vector2(56, -170));
        TMP_Text goal = Label(card, GoalText(gm.TotalCheese), 21, TextColor, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        goal.lineSpacing = 18f;
        Place(goal, new Vector2(56, -200), new Vector2(928, 130));

        Section(card, "CONTROLS", new Vector2(56, -352));
        ControlsColumns(card, new Vector2(56, -384), 928);

        Button start = MakeButton(card, "START", Accent, AccentDark, new Vector2(260, 62), gm.StartGame);
        RectTransform sr = (RectTransform)start.transform;
        sr.anchorMin = sr.anchorMax = new Vector2(0.5f, 0f);
        sr.pivot = new Vector2(0.5f, 0f);
        sr.anchoredPosition = new Vector2(0, 62);
        TMP_Text or = Label(card, "or press Enter", 17, Muted, FontStyles.Normal, TextAlignmentOptions.Center);
        RectTransform orr = or.rectTransform;
        orr.anchorMin = orr.anchorMax = new Vector2(0.5f, 0f);
        orr.pivot = new Vector2(0.5f, 0f);
        orr.anchoredPosition = new Vector2(0, 28);
        orr.sizeDelta = new Vector2(300, 26);
    }

    void BuildPause()
    {
        RectTransform card = Overlay(root, "Pause", new Vector2(1040, 660), out pauseGroup);
        Place(Label(card, "PAUSED", 52, TextColor, FontStyles.Bold, TextAlignmentOptions.Top), new Vector2(0, -38), new Vector2(960, 64), center: true);
        pauseStats = Label(card, "", 22, Muted, FontStyles.Normal, TextAlignmentOptions.Top);
        Place(pauseStats, new Vector2(0, -110), new Vector2(960, 32), center: true);

        Section(card, "CONTROLS", new Vector2(56, -168));
        ControlsColumns(card, new Vector2(56, -200), 928);

        if (gm.HasMainMenu)
            ButtonRow(card, 54,
                ("RESUME  (Esc)", Accent, AccentDark, (Action)gm.Resume),
                ("RESTART  (R)", ButtonSoft, TextColor, (Action)gm.Restart),
                ("MAIN MENU  (M)", ButtonSoft, TextColor, (Action)gm.GoToMainMenu));
        else
            ButtonRow(card, 54,
                ("RESUME  (Esc)", Accent, AccentDark, (Action)gm.Resume),
                ("RESTART  (R)", ButtonSoft, TextColor, (Action)gm.Restart),
                ("QUIT  (Q)", ButtonSoft, TextColor, (Action)gm.Quit));
    }

    void BuildWin()
    {
        RectTransform card = Overlay(root, "Win", new Vector2(860, 600), out winGroup);
        winTitle = Label(card, "ALL CHEESE EATEN!", 54, Accent, FontStyles.Bold, TextAlignmentOptions.Top);
        Place(winTitle, new Vector2(0, -40), new Vector2(800, 66), center: true);
        Place(Label(card, "The mouse is very full. Nice work.", 22, Muted, FontStyles.Normal, TextAlignmentOptions.Top), new Vector2(0, -112), new Vector2(800, 32), center: true);

        Image statsBg = NewImage(card, "StatsBg", Rounded, PanelSoft);
        Place(statsBg.rectTransform, new Vector2(0, -164), new Vector2(760, 148), center: true);
        winStats = Label(statsBg.rectTransform, "", 23, TextColor, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        winStats.lineSpacing = 22f;
        RectTransform ws = winStats.rectTransform;
        Fill(ws);
        ws.offsetMin = new Vector2(30, 14);
        ws.offsetMax = new Vector2(-30, -18);

        winScore = Label(card, "", 64, Accent, FontStyles.Bold, TextAlignmentOptions.Top);
        Place(winScore, new Vector2(0, -330), new Vector2(760, 120), center: true);

        if (gm.HasMainMenu)
            ButtonRow(card, 50,
                ("PLAY AGAIN  (R)", Accent, AccentDark, (Action)gm.Restart),
                ("MAIN MENU  (M)", ButtonSoft, TextColor, (Action)gm.GoToMainMenu));
        else
            ButtonRow(card, 50,
                ("PLAY AGAIN  (R)", Accent, AccentDark, (Action)gm.Restart),
                ("QUIT  (Q)", ButtonSoft, TextColor, (Action)gm.Quit));
    }

    // ================================================================ helpers

    void Fade(CanvasGroup g, bool show, float dt)
    {
        g.alpha = Mathf.MoveTowards(g.alpha, show ? 1f : 0f, dt * 6f);
        bool interactive = show && g.alpha > 0.5f;
        g.blocksRaycasts = interactive && g != hud;
        g.interactable = interactive;
    }

}
