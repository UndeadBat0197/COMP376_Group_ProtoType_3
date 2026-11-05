using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Runs the cheese hunt: intro/controls screen, playing, pause, and the win screen.
/// Counts the cheese at start, scores each one the mouse eats, times the run, and ends the game
/// when every cheese is gone.
///
/// Put this on one always-active GameObject (Tools > Cheese Game sets it up). It finds the
/// FormSwitcher and the cheese itself, adds MouseCheeseEater to the mouse, and builds the HUD.
/// </summary>
[DefaultExecutionOrder(-100)]
public class CheeseGameManager : MonoBehaviour
{
    public enum Phase { Intro, Playing, Paused, Won }

    public static CheeseGameManager Instance { get; private set; }

    [Header("Game")]
    public string gameTitle = "Cheese Hunt";
    [Tooltip("Finish under this time for a time bonus.")]
    public float parTimeSeconds = 240f;
    [Tooltip("Bonus points for every second under par time.")]
    public int bonusPerSecond = 2;
    public bool showIntro = true;
    [Tooltip("Scene the pause/win screens go back to. Leave as is if you have no main menu.")]
    public string mainMenuScene = "MainMenu";

    [Header("References (auto-found if empty)")]
    public FormSwitcher formSwitcher;

    [Header("Feel")]
    public bool lockCursorWhilePlaying = true;
    [Range(0f, 1f)] public float sfxVolume = 0.5f;

    public Phase CurrentPhase { get; private set; } = Phase.Intro;
    public int TotalCheese { get; private set; }
    public int EatenCheese { get; private set; }
    public int CheesePoints { get; private set; }
    public int TimeBonus { get; private set; }
    public int Score => CheesePoints + TimeBonus;
    public float ElapsedTime { get; private set; }
    public float BestTime { get; private set; }
    public bool NewBest { get; private set; }

    public bool HasMainMenu => !string.IsNullOrEmpty(mainMenuScene) &&
                               Application.CanStreamedLevelBeLoaded(mainMenuScene) &&
                               SceneManager.GetActiveScene().name != mainMenuScene;
    public bool IsHuman => formSwitcher != null && formSwitcher.IsHuman;
    public GameObject MouseForm => formSwitcher != null ? formSwitcher.mouseForm : null;
    public GameObject HumanForm => formSwitcher != null ? formSwitcher.humanForm : null;
    public MouseCheeseEater Eater { get; private set; }
    public HumanInteractor Interactor { get; private set; }

    /// <summary>(cheese world position, points) whenever a cheese is eaten. The HUD listens to this.</summary>
    public event System.Action<Vector3, int> CheeseEaten;
    /// <summary>A short message for the toast at the top of the screen.</summary>
    public event System.Action<string> Toast;

    readonly List<Cheese> cheeses = new List<Cheese>();
    readonly List<Behaviour> pausedBehaviours = new List<Behaviour>();
    bool lastWasHuman;
    bool lookWasActive;
    int unblockOnFrame = -1;
    AudioSource audioSource;
    AudioClip eatClip, winClip;
    string BestKey => "CheeseHunt_BestTime_" + SceneManager.GetActiveScene().name;

    void Awake()
    {
        Instance = this;
        Time.timeScale = 1f;
        GameState.InputBlocked = false;

        if (formSwitcher == null) formSwitcher = FindAnyObjectByType<FormSwitcher>();
        BestTime = PlayerPrefs.GetFloat(BestKey, 0f);

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        eatClip = MakeEatClip();
        winClip = MakeWinClip();

        if (GetComponent<GameHUD>() == null) gameObject.AddComponent<GameHUD>();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        Time.timeScale = 1f;
        GameState.InputBlocked = false;
    }

    void Start()
    {
        if (formSwitcher == null)
            Debug.LogError("[CheeseGame] No FormSwitcher in the scene. The mouse and human need one to play.", this);

        // The mouse is the only one who can eat.
        if (MouseForm != null)
        {
            Eater = MouseForm.GetComponent<MouseCheeseEater>();
            if (Eater == null) Eater = MouseForm.AddComponent<MouseCheeseEater>();
        }
        if (HumanForm != null)
        {
            Interactor = HumanForm.GetComponent<HumanInteractor>();
            if (Interactor != null) Interactor.showPromptGUI = false; // the HUD draws prompts now
            // Smell is a mouse sense.
            foreach (MonoBehaviour mb in HumanForm.GetComponents<MonoBehaviour>())
                if (mb != null && mb.GetType().Name == "CheeseSmellMeter") mb.enabled = false;
        }

        CollectCheese();
        AssignSmellZones();
        lastWasHuman = IsHuman;

        if (showIntro) EnterPhase(Phase.Intro);
        else EnterPhase(Phase.Playing);
    }

    void CollectCheese()
    {
        cheeses.Clear();
        // Fallback for scenes that weren't set up: anything visible called "cheese".
        if (Cheese.All.Count == 0)
            foreach (Renderer r in FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                Transform root = CheeseRoot(r.transform);
                if (root != null && root.GetComponent<Cheese>() == null) root.gameObject.AddComponent<Cheese>();
            }

        foreach (Cheese c in Cheese.All)
        {
            if (c == null || cheeses.Contains(c)) continue;
            cheeses.Add(c);
            c.Eaten += OnCheeseEaten;
        }
        TotalCheese = cheeses.Count;
        EatenCheese = 0;
        if (TotalCheese == 0) Debug.LogWarning("[CheeseGame] No cheese found. Add the Cheese component to your cheese objects.", this);
    }

    // Topmost ancestor whose name contains "cheese" (but isn't a smell zone).
    static Transform CheeseRoot(Transform t)
    {
        Transform found = null;
        for (Transform p = t; p != null; p = p.parent)
            if (p.name.ToLower().Contains("cheese") && !p.name.ToLower().Contains("smell")) found = p;
        return found;
    }

    // Each existing SmellZone goes to the nearest cheese. Cheese that no zone reaches get their own.
    // (The smell meter only tracks one zone at a time, so zones shouldn't pile up on each other.)
    void AssignSmellZones()
    {
        foreach (SphereCollider zone in FindObjectsByType<SphereCollider>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (!zone.isTrigger || !zone.CompareTag("SmellZone")) continue;
            Vector3 center = zone.transform.TransformPoint(zone.center);
            Cheese nearest = null;
            float best = 1.5f;
            foreach (Cheese c in cheeses)
            {
                float d = Vector3.Distance(center, c.Center);
                if (d < best) { best = d; nearest = c; }
            }
            if (nearest != null) nearest.AdoptSmellZone(zone.transform);
        }
        foreach (Cheese c in cheeses)
            if (!c.HasSmellZone && !InsideAnyZone(c.Center)) c.CreateSmellZone();
    }

    static float ZoneRadius(Transform zone, out Vector3 center)
    {
        SphereCollider sc = zone != null ? zone.GetComponent<SphereCollider>() : null;
        center = sc != null ? zone.TransformPoint(sc.center) : (zone != null ? zone.position : Vector3.zero);
        if (sc == null) return 0f;
        Vector3 s = zone.lossyScale;
        return sc.radius * Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
    }

    bool InsideAnyZone(Vector3 p)
    {
        foreach (Cheese c in cheeses)
            foreach (Transform z in c.SmellZones)
            {
                float r = ZoneRadius(z, out Vector3 center);
                if (Vector3.Distance(p, center) < r) return true;
            }
        return false;
    }

    // When a cheese is eaten, a nearby cheese that has no smell of its own inherits its zone.
    void HandOffSmell(Cheese eaten)
    {
        if (!eaten.HasSmellZone) return;
        float radius = ZoneRadius(eaten.SmellZones[0], out Vector3 center);
        Cheese heir = null;
        float best = float.MaxValue;
        foreach (Cheese c in cheeses)
        {
            if (c == null || c == eaten || c.IsEaten || c.HasSmellZone) continue;
            float d = Vector3.Distance(c.Center, center);
            if (d < radius && d < best) { best = d; heir = c; }
        }
        if (heir != null) eaten.TransferSmellZonesTo(heir);
    }

    void Update()
    {
        // Input comes back one frame after a menu closes, so the click on "Resume"
        // doesn't also count as a throw.
        if (CurrentPhase == Phase.Playing && GameState.InputBlocked && Time.frameCount >= unblockOnFrame)
            GameState.InputBlocked = false;

        switch (CurrentPhase)
        {
            case Phase.Intro:
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) StartGame();
                break;

            case Phase.Playing:
                ElapsedTime += Time.deltaTime;
                if (IsHuman != lastWasHuman)
                {
                    lastWasHuman = IsHuman;
                    if (lockCursorWhilePlaying) SetCursor(true);
                }
                if (Input.GetKeyDown(KeyCode.Escape) && !lookWasActive) Pause();
                break;

            case Phase.Paused:
                if (Input.GetKeyDown(KeyCode.Escape)) Resume();
                else if (Input.GetKeyDown(KeyCode.R)) Restart();
                else if (Input.GetKeyDown(KeyCode.M) && HasMainMenu) GoToMainMenu();
                else if (Input.GetKeyDown(KeyCode.Q)) Quit();
                break;

            case Phase.Won:
                if (Input.GetKeyDown(KeyCode.R) || Input.GetKeyDown(KeyCode.Return)) Restart();
                else if (Input.GetKeyDown(KeyCode.M) && HasMainMenu) GoToMainMenu();
                else if (Input.GetKeyDown(KeyCode.Q)) Quit();
                break;
        }
    }

    void LateUpdate()
    {
        // Esc also backs out of "look into shelf", so don't pause on the same press.
        lookWasActive = Interactor != null && Interactor.isActiveAndEnabled && Interactor.ActiveLook != null;
    }

    // ------------------------------------------------------------ phases (also called by HUD buttons)

    public void StartGame() { if (CurrentPhase == Phase.Intro) EnterPhase(Phase.Playing); }
    public void Pause() { if (CurrentPhase == Phase.Playing) EnterPhase(Phase.Paused); }
    public void Resume() { if (CurrentPhase == Phase.Paused) EnterPhase(Phase.Playing); }

    public void Restart()
    {
        Time.timeScale = 1f;
        GameState.InputBlocked = false;
        Scene scene = SceneManager.GetActiveScene();
        if (scene.buildIndex >= 0)
        {
            SceneManager.LoadScene(scene.buildIndex);
            return;
        }
#if UNITY_EDITOR
        // Scene isn't in the build list: still let Play Again work in the editor.
        UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
            scene.path, new LoadSceneParameters(LoadSceneMode.Single));
#else
        SceneManager.LoadScene(scene.name);
#endif
    }

    public void GoToMainMenu()
    {
        if (!HasMainMenu) return;
        Time.timeScale = 1f;
        GameState.InputBlocked = false;
        SceneManager.LoadScene(mainMenuScene);
    }

    public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void EnterPhase(Phase p)
    {
        CurrentPhase = p;
        bool playing = p == Phase.Playing;

        Time.timeScale = playing ? 1f : 0f;
        GameState.InputBlocked = true;
        unblockOnFrame = playing ? Time.frameCount + 1 : int.MaxValue;
        SetGameplayBehaviours(playing);
        SetCursor(playing && lockCursorWhilePlaying);
    }

    // Turn off things that would react to the mouse/keyboard while a menu is up:
    // the form wheel, Cinemachine's look input, and the mouse's movement scripts.
    void SetGameplayBehaviours(bool on)
    {
        if (on)
        {
            foreach (Behaviour b in pausedBehaviours) if (b != null) b.enabled = true;
            pausedBehaviours.Clear();
            return;
        }
        if (pausedBehaviours.Count > 0) return;

        foreach (MonoBehaviour mb in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (mb == null || !mb.enabled || mb == this) continue;
            string n = mb.GetType().Name;
            if (n == "FormWheelUI" || n == "CinemachineInputAxisController" ||
                n == "ThirdPersonMovement" || n == "WallClimbing")
            {
                mb.enabled = false;
                pausedBehaviours.Add(mb);
            }
        }
    }

    void SetCursor(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    // ------------------------------------------------------------ cheese

    void OnCheeseEaten(Cheese c)
    {
        if (CurrentPhase != Phase.Playing) return;
        HandOffSmell(c);
        EatenCheese++;
        CheesePoints += c.points;
        Play(eatClip);
        CheeseEaten?.Invoke(c.Center, c.points);

        // The cheese's smell zone just vanished; reset the meter so it doesn't stay stuck on.
        if (MouseForm != null)
            foreach (MonoBehaviour mb in MouseForm.GetComponents<MonoBehaviour>())
                if (mb != null && mb.enabled && mb.GetType().Name == "CheeseSmellMeter")
                {
                    mb.enabled = false;
                    mb.enabled = true;
                }

        int left = TotalCheese - EatenCheese;
        if (left <= 0) Win();
        else ShowToast(left == 1 ? "Yum! One cheese left" : $"Yum! {left} cheese left");
    }

    void Win()
    {
        TimeBonus = Mathf.Max(0, Mathf.RoundToInt((parTimeSeconds - ElapsedTime) * bonusPerSecond));
        NewBest = BestTime <= 0f || ElapsedTime < BestTime;
        if (NewBest)
        {
            BestTime = ElapsedTime;
            PlayerPrefs.SetFloat(BestKey, BestTime);
            PlayerPrefs.Save();
        }
        Play(winClip);
        EnterPhase(Phase.Won);
    }

    public void ShowToast(string text) => Toast?.Invoke(text);

    public static string FormatTime(float seconds)
    {
        int s = Mathf.Max(0, Mathf.FloorToInt(seconds));
        return $"{s / 60:00}:{s % 60:00}";
    }

    // ------------------------------------------------------------ tiny generated sounds (no audio files needed)

    void Play(AudioClip clip)
    {
        if (clip != null && sfxVolume > 0f) audioSource.PlayOneShot(clip, sfxVolume);
    }

    static AudioClip MakeEatClip()
    {
        // Two quick "nom" blips (0 Hz = a short gap).
        return Tone("nom", new[] { 520f, 0f, 700f }, new[] { 0.07f, 0.03f, 0.09f });
    }

    static AudioClip MakeWinClip()
    {
        // Rising C-major arpeggio.
        return Tone("win", new[] { 523f, 659f, 784f, 1047f }, new[] { 0.12f, 0.12f, 0.12f, 0.35f });
    }

    static AudioClip Tone(string name, float[] freqs, float[] durs)
    {
        const int rate = 44100;
        float total = 0f;
        foreach (float d in durs) total += d;
        float[] data = new float[Mathf.CeilToInt(total * rate)];
        int i = 0;
        for (int n = 0; n < freqs.Length; n++)
        {
            float freq = freqs[n], dur = durs[n];
            int len = Mathf.CeilToInt(dur * rate);
            for (int k = 0; k < len && i < data.Length; k++, i++)
            {
                if (freq <= 0f) continue;
                float t = k / (float)rate;
                float env = Mathf.Min(1f, k / (rate * 0.005f)) * Mathf.Exp(-4f * t / dur);
                // Sine with a bit of square for a cute, game-y tone.
                float s = Mathf.Sin(2f * Mathf.PI * freq * t);
                data[i] = env * 0.35f * (0.7f * s + 0.3f * Mathf.Sign(s));
            }
        }
        AudioClip clip = AudioClip.Create(name, data.Length, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
