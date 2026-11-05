using UnityEngine;

/// <summary>
/// Hold T to choose a form with the mouse, release T to confirm.
/// Mouse and Human use the existing FormSwitcher. Cat is a locked placeholder.
/// Place this on a separate, always-active GameObject with FormSwitcher assigned.
/// </summary>
public class FormWheelUI : MonoBehaviour
{
    public FormSwitcher formSwitcher;
    public KeyCode wheelKey = KeyCode.T;
    [Range(180, 600)] public int textureSize = 384;
    [Range(0.1f, 0.5f)] public float innerRadius = 0.20f;
    [Range(0.25f, 0.5f)] public float outerRadius = 0.48f;
    public float displaySize = 340f;

    private enum Form { None, Cat, Human, Mouse }
    private bool wheelOpen;
    private Form hover = Form.None;
    private CursorLockMode previousLock;
    private bool previousCursorVisible;
    private Texture2D[] wheels;
    private ThirdPersonMovement mouseMovement;
    private WallClimbing mouseClimbing;
    private HumanMovement humanMovement;
    private bool mouseWasEnabled, climbWasEnabled, humanInputWasEnabled;
    private GUIStyle titleStyle, formStyle, hintStyle;

    void Awake()
    {
        if (formSwitcher == null) formSwitcher = GetComponent<FormSwitcher>();
        if (formSwitcher == null)
        {
            Debug.LogError("FormWheelUI: Assign a FormSwitcher in the Inspector.", this);
            enabled = false;
            return;
        }
        // Prevent the existing FormSwitcher T-toggle from conflicting with the wheel.
        formSwitcher.allowKeySwitch = false;
        for (int i = 0; i < 4; i++) { /* values made in Start */ }
    }

    void Start()
    {
        if (formSwitcher == null) return;
        CacheControllers();
        wheels = new Texture2D[4];
        for (int i = 0; i < wheels.Length; i++) wheels[i] = MakeWheel((Form)i);
    }

    void CacheControllers()
    {
        if (formSwitcher.mouseForm != null)
        {
            mouseMovement = formSwitcher.mouseForm.GetComponent<ThirdPersonMovement>();
            mouseClimbing = formSwitcher.mouseForm.GetComponent<WallClimbing>();
        }
        if (formSwitcher.humanForm != null)
            humanMovement = formSwitcher.humanForm.GetComponent<HumanMovement>();
    }

    void Update()
    {
        if (Input.GetKeyDown(wheelKey) && !wheelOpen) OpenWheel();
        if (!wheelOpen) return;

        Vector2 offset = new Vector2(Input.mousePosition.x - Screen.width / 2f,
                                     Input.mousePosition.y - Screen.height / 2f);
        float radius = offset.magnitude;
        float outer = displaySize / 2f * (outerRadius / 0.5f);
        float inner = displaySize / 2f * (innerRadius / 0.5f);
        if (radius < inner || radius > outer) hover = Form.None;
        else
        {
            // Top=0°, clockwise positive; Cat top, Human lower right, Mouse lower left.
            float degrees = (Mathf.Atan2(offset.x, offset.y) * Mathf.Rad2Deg + 360f) % 360f;
            hover = degrees < 60f || degrees >= 300f ? Form.Cat :
                    degrees < 180f ? Form.Human : Form.Mouse;
        }

        if (Input.GetKeyUp(wheelKey)) CloseWheel(true);
    }

    void OpenWheel()
    {
        CacheControllers();
        wheelOpen = true;
        hover = Form.None;
        previousLock = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (mouseMovement != null) { mouseWasEnabled = mouseMovement.enabled; mouseMovement.enabled = false; }
        if (mouseClimbing != null) { climbWasEnabled = mouseClimbing.enabled; mouseClimbing.enabled = false; }
        if (humanMovement != null) { humanInputWasEnabled = humanMovement.InputEnabled; humanMovement.InputEnabled = false; }
        GameObject activeForm = formSwitcher.IsHuman ? formSwitcher.humanForm : formSwitcher.mouseForm;
        if (activeForm != null)
        {
            Rigidbody rb = activeForm.GetComponent<Rigidbody>();
            if (rb != null) rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
        }
    }

    void CloseWheel(bool apply)
    {
        if (!wheelOpen) return;
        wheelOpen = false;
        Cursor.lockState = previousLock;
        Cursor.visible = previousCursorVisible;
        if (mouseMovement != null) mouseMovement.enabled = mouseWasEnabled;
        if (mouseClimbing != null) mouseClimbing.enabled = climbWasEnabled;
        if (humanMovement != null) humanMovement.InputEnabled = humanInputWasEnabled;
        if (!apply || formSwitcher == null) return;
        if (hover == Form.Mouse && formSwitcher.IsHuman) formSwitcher.BecomeMouse();
        if (hover == Form.Human && !formSwitcher.IsHuman) formSwitcher.BecomeHuman();
        // Cat intentionally locked until a working Cat form is added.
    }

    void OnDisable() { CloseWheel(false); }
    void OnDestroy()
    {
        if (wheels == null) return;
        foreach (Texture2D tex in wheels) if (tex != null) Destroy(tex);
    }

    void OnGUI()
    {
        if (!wheelOpen || wheels == null || wheels[(int)hover] == null) return;
        // IMGUI styles must be created while inside the OnGUI context.
        if (titleStyle == null)
        {
            titleStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 17, fontStyle = FontStyle.Bold };
            titleStyle.normal.textColor = Color.white;
            formStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 16, fontStyle = FontStyle.Bold };
            formStyle.normal.textColor = Color.white;
            hintStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 13 };
            hintStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f);
        }
        float s = Mathf.Min(displaySize, Mathf.Min(Screen.width, Screen.height) * 0.85f);
        float cx = Screen.width / 2f, cy = Screen.height / 2f;
        GUI.DrawTexture(new Rect(cx - s / 2f, cy - s / 2f, s, s), wheels[(int)hover]);
        GUI.Label(new Rect(cx - 76f, cy - 20f, 152f, 26f), "FORM", titleStyle);
        GUI.Label(new Rect(cx - 76f, cy + 3f, 152f, 22f), "SWAP", hintStyle);
        DrawSection(cx, cy - s * 0.345f, "CAT", "COMING SOON", hover == Form.Cat);
        DrawSection(cx + s * 0.30f, cy + s * 0.18f, "HUMAN", "", hover == Form.Human);
        DrawSection(cx - s * 0.30f, cy + s * 0.18f, "MOUSE", "", hover == Form.Mouse);
        GUI.Label(new Rect(cx - 230f, cy + s * 0.5f + 4f, 460f, 25f), "Release T to select  •  Center to cancel", hintStyle);
    }

    void DrawSection(float x, float y, string name, string sub, bool selected)
    {
        Color was = formStyle.normal.textColor;
        formStyle.normal.textColor = name == "CAT" ? new Color(0.68f, 0.72f, 0.78f) :
                                     selected ? new Color(1f, 0.92f, 0.54f) : Color.white;
        GUI.Label(new Rect(x - 65f, y - 18f, 130f, 26f), name, formStyle);
        if (sub.Length != 0) GUI.Label(new Rect(x - 70f, y + 9f, 140f, 20f), sub, hintStyle);
        formStyle.normal.textColor = was;
    }

    Texture2D MakeWheel(Form highlighted)
    {
        int n = textureSize;
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Color32[] pixels = new Color32[n * n];
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            float dx = (x + 0.5f) / n - 0.5f;
            float dy = (y + 0.5f) / n - 0.5f;
            float r = Mathf.Sqrt(dx * dx + dy * dy);
            if (r < innerRadius || r > outerRadius) continue;
            float angle = (Mathf.Atan2(dx, dy) * Mathf.Rad2Deg + 360f) % 360f;
            Form segment = angle < 60f || angle >= 300f ? Form.Cat :
                           angle < 180f ? Form.Human : Form.Mouse;
            bool border = Mathf.Abs(r - innerRadius) < 0.005f || Mathf.Abs(r - outerRadius) < 0.005f ||
                          Mathf.Abs(Mathf.DeltaAngle(angle, 60f)) < 1f ||
                          Mathf.Abs(Mathf.DeltaAngle(angle, 180f)) < 1f ||
                          Mathf.Abs(Mathf.DeltaAngle(angle, 300f)) < 1f;
            Color c = border ? new Color(0.76f, 0.82f, 0.87f, 0.95f) :
                      segment == Form.Cat ? new Color(0.23f, 0.26f, 0.30f, 0.87f) :
                      highlighted == segment ? new Color(0.27f, 0.51f, 0.77f, 0.96f) :
                      new Color(0.12f, 0.17f, 0.24f, 0.90f);
            pixels[y * n + x] = c;
        }
        tex.SetPixels32(pixels);
        tex.Apply(false, true);
        return tex;
    }
}
