using UnityEngine;

/// <summary>
/// Lets the human find and use Interactables.
///  - Aims with the camera centre (crosshair); falls back to "whatever's right in front of me".
///  - E: use (open, look inside, pick up). F: grab & move heavy things (Pushable).
///    On something that can only be moved (boulder, armchair), E grabs too.
///  - Carrying: E drops, left click throws. Moving furniture: W push, S pull, E/F let go.
///  - While looking into a shelf/crate (LookPoint), the cursor is free: point at an item and click/E.
///    Q / Esc / right click steps back.
/// Shows a simple OnGUI prompt so it works with zero UI setup.
/// </summary>
[RequireComponent(typeof(HumanMovement))]
public class HumanInteractor : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Child transform in front of the chest where carried items float.")]
    public Transform holdPoint;
    [Tooltip("Camera used for aiming. Auto-filled with Camera.main if empty.")]
    public Camera viewCamera;

    [Header("Reach")]
    public float interactRange = 2.2f;
    [Tooltip("Height of the 'hands' above the feet; reach is measured from here.")]
    public float reachHeight = 1.3f;
    public LayerMask interactMask = ~0;

    [Header("Keys")]
    public KeyCode interactKey = KeyCode.E;
    public KeyCode grabKey = KeyCode.F;
    public KeyCode throwKey = KeyCode.Mouse0;
    public KeyCode exitLookKey = KeyCode.Q;

    [Header("UI")]
    public bool lockCursor = true;
    public bool showPromptGUI = true;

    public HumanMovement Movement { get; private set; }
    public Rigidbody Body { get; private set; }
    public Collider[] OwnColliders { get; private set; }

    /// <summary>What E does right now (may be a Pushable).</summary>
    public Interactable Current { get; private set; }
    /// <summary>What F moves right now (the Pushable this aim belongs to, e.g. the cabinet behind a door).</summary>
    public Pushable CurrentMovable { get; private set; }
    public Liftable Held { get; private set; }
    public Pushable Grabbed { get; private set; }
    public LookPoint ActiveLook { get; private set; }

    public Vector3 ReachOrigin => Feet + Vector3.up * reachHeight;
    Vector3 Feet => Movement != null ? Movement.FeetPosition : transform.position;

    string message;
    float messageUntil;
    GUIStyle promptStyle, smallStyle;

    void Awake()
    {
        Movement = GetComponent<HumanMovement>();
        Body = GetComponent<Rigidbody>();
        OwnColliders = GetComponentsInChildren<Collider>(true);

        if (holdPoint == null)
        {
            holdPoint = new GameObject("HoldPoint").transform;
            holdPoint.SetParent(transform, false);
            holdPoint.position = Feet + Vector3.up * 1.15f + transform.forward * 0.8f;
        }
    }

    void OnEnable()
    {
        SetCursorLocked(lockCursor);
    }

    void OnDisable()
    {
        // Transforming back into the mouse (or despawning) — let go of everything.
        if (Held != null) Held.Drop();
        if (Grabbed != null) Grabbed.Release();
        if (ActiveLook != null) ActiveLook.Exit(this);
        Current = null;
        CurrentMovable = null;
        SetCursorLocked(false);
    }

    void Update()
    {
        if (viewCamera == null) viewCamera = Camera.main;
        if (GameState.InputBlocked) return; // a menu is open

        if (Held != null)
        {
            Current = null; CurrentMovable = null;
            if (Input.GetKeyDown(interactKey)) Held.Drop();
            else if (Input.GetKeyDown(throwKey)) Held.Throw();
            return;
        }

        if (Grabbed != null)
        {
            Current = null; CurrentMovable = null;
            if (Input.GetKeyDown(interactKey) || Input.GetKeyDown(grabKey)) Grabbed.Release();
            return;
        }

        if (ActiveLook != null &&
            (Input.GetKeyDown(exitLookKey) || Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Mouse1)))
        {
            ActiveLook.Exit(this);
            return;
        }

        FindTarget();

        bool usePressed = Input.GetKeyDown(interactKey) || (ActiveLook != null && Input.GetKeyDown(KeyCode.Mouse0));
        if (usePressed && Current != null)
        {
            Current.Interact(this);
            // Took something out of the shelf/crate? Step back into normal control.
            if (ActiveLook != null && (Held != null || Grabbed != null)) ActiveLook.Exit(this);
        }
        else if (Input.GetKeyDown(grabKey) && CurrentMovable != null && ActiveLook == null)
        {
            CurrentMovable.Grab(this);
        }
    }

    // ---------------- Targeting ----------------

    void FindTarget()
    {
        Current = null;
        CurrentMovable = null;

        if (viewCamera != null)
        {
            Ray ray = ActiveLook != null
                ? viewCamera.ScreenPointToRay(Input.mousePosition)
                : viewCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

            RaycastHit[] hits = Physics.RaycastAll(ray, 50f, interactMask, QueryTriggerInteraction.Collide);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (RaycastHit hit in hits)
            {
                Collider col = hit.collider;
                if (IsOwnCollider(col)) continue;

                Interactable it = col.GetComponentInParent<Interactable>();
                bool usable = it != null && it.CanInteract(this) && !(ActiveLook != null && it is LookPoint);
                bool inReach = Vector3.Distance(ReachOrigin, hit.point) <= interactRange;

                // Triggers (smell zones, look zones we can't use) never block aim.
                if (col.isTrigger && (!usable || !inReach)) continue;
                if (!inReach) break;

                if (usable) Current = it;
                if (ActiveLook == null) CurrentMovable = MovableFor(col);
                if (Current != null || CurrentMovable != null) return;
                break; // something solid and inert is in the way
            }
        }

        if (ActiveLook == null) FindNearbyInFront();
    }

    // Third-person aiming at low objects (cheese on the floor) is fiddly, so if the
    // crosshair finds nothing, pick the best thing right in front of the body.
    void FindNearbyInFront()
    {
        Vector3 center = Feet + transform.forward * 0.7f + Vector3.up * 0.6f;
        Collider[] cols = Physics.OverlapSphere(center, 0.75f, interactMask, QueryTriggerInteraction.Collide);

        Collider bestCol = null;
        Interactable best = null;
        float bestScore = float.MinValue;
        foreach (Collider col in cols)
        {
            if (IsOwnCollider(col)) continue;
            Interactable it = col.GetComponentInParent<Interactable>();
            if (it == null || it is LookPoint || !it.CanInteract(this)) continue;

            Vector3 closest = col.ClosestPoint(ReachOrigin);
            Vector3 to = closest - ReachOrigin;
            if (to.magnitude > interactRange) continue;
            if (!HasLineOfSight(it, closest)) continue; // e.g. cheese under a boulder

            Vector3 flat = Vector3.ProjectOnPlane(to, Vector3.up);
            float score = flat.sqrMagnitude > 0.0001f ? Vector3.Dot(transform.forward, flat.normalized) : 1f;
            score -= to.magnitude * 0.25f;
            if (score > bestScore) { bestScore = score; best = it; bestCol = col; }
        }

        if (best != null)
        {
            Current = best;
            CurrentMovable = MovableFor(bestCol);
        }
    }

    Pushable MovableFor(Collider col)
    {
        Pushable p = col.GetComponentInParent<Pushable>();
        return p != null && p.CanInteract(this) ? p : null;
    }

    bool HasLineOfSight(Interactable target, Vector3 point)
    {
        Vector3 dir = point - ReachOrigin;
        float dist = dir.magnitude;
        if (dist < 0.01f) return true;

        RaycastHit[] hits = Physics.RaycastAll(ReachOrigin, dir / dist, dist + 0.02f, interactMask, QueryTriggerInteraction.Ignore);
        foreach (RaycastHit h in hits)
        {
            if (IsOwnCollider(h.collider)) continue;
            if (h.collider.GetComponentInParent<Interactable>() != target) return false;
        }
        return true;
    }

    public bool IsOwnCollider(Collider c)
    {
        return c.attachedRigidbody == Body || c.transform.IsChildOf(transform);
    }

    // ---------------- State called by interactables ----------------

    public void SetHeld(Liftable item) { Held = item; }
    public void ClearHeld(Liftable item) { if (Held == item) Held = null; }
    public void SetGrabbed(Pushable item) { Grabbed = item; }
    public void ClearGrabbed(Pushable item) { if (Grabbed == item) Grabbed = null; }

    public void BeginLook(LookPoint look)
    {
        ActiveLook = look;
        Movement.InputEnabled = false;
        SetCursorLocked(false);
    }

    public void EndLook(LookPoint look)
    {
        if (ActiveLook != look) return;
        ActiveLook = null;
        Movement.InputEnabled = true;
        if (isActiveAndEnabled) SetCursorLocked(lockCursor);
    }

    public void ShowMessage(string text, float seconds = 2f)
    {
        message = text;
        messageUntil = Time.time + seconds;
    }

    void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    // ---------------- Prompt data (used by GameHUD) ----------------

    public struct PromptAction
    {
        public string key;
        public string label;
        public PromptAction(string key, string label) { this.key = key; this.label = label; }
    }

    /// <summary>The message from ShowMessage() if it's still showing, else null.</summary>
    public string ActiveMessage => message != null && Time.time < messageUntil ? message : null;

    public bool ShowCrosshair => ActiveLook == null && Held == null && Grabbed == null;

    /// <summary>
    /// What the on-screen prompt should say right now: an optional title ("Carrying Rock") and
    /// the key/action pairs. Returns false if there's nothing to show.
    /// </summary>
    public bool GetPrompt(System.Collections.Generic.List<PromptAction> actions, out string title)
    {
        actions.Clear();
        title = null;
        string use = KeyName(interactKey), grab = KeyName(grabKey);

        if (Held != null)
        {
            title = $"Carrying {Held.DisplayName}";
            actions.Add(new PromptAction(use, "Drop"));
            actions.Add(new PromptAction(KeyName(throwKey), "Throw"));
        }
        else if (Grabbed != null)
        {
            title = $"Holding {Grabbed.DisplayName}";
            actions.Add(new PromptAction("W", "Push"));
            if (Grabbed.allowPull) actions.Add(new PromptAction("S", "Pull"));
            if (Grabbed.canRotate) actions.Add(new PromptAction("A/D", "Turn"));
            actions.Add(new PromptAction(grab, "Let go"));
        }
        else if (ActiveLook != null)
        {
            title = $"Looking in the {ActiveLook.DisplayName}";
            if (Current != null) actions.Add(new PromptAction("Click", Current.GetPrompt(this)));
            actions.Add(new PromptAction(KeyName(exitLookKey), "Step back"));
        }
        else if (Current != null && Current == CurrentMovable)
        {
            actions.Add(new PromptAction(grab, Current.GetPrompt(this))); // E works too; F matches the controls card
        }
        else
        {
            if (Current != null) actions.Add(new PromptAction(use, Current.GetPrompt(this)));
            if (CurrentMovable != null) actions.Add(new PromptAction(grab, CurrentMovable.GetPrompt(this)));
        }
        return title != null || actions.Count > 0;
    }

    // ---------------- Placeholder UI (off when the GameHUD is in the scene) ----------------

    void OnGUI()
    {
        if (!showPromptGUI) return;

        if (promptStyle == null)
        {
            promptStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Max(18, Screen.height / 36),
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
            smallStyle = new GUIStyle(promptStyle) { fontSize = Mathf.Max(14, Screen.height / 48), fontStyle = FontStyle.Normal };
        }

        if (ActiveLook == null && Held == null && Grabbed == null)
            ShadowLabel(new Rect(Screen.width / 2f - 10, Screen.height / 2f - 12, 20, 24), "+", promptStyle);

        string use = KeyName(interactKey);
        string grab = KeyName(grabKey);
        string line = null;
        string hint = null;

        if (Held != null)
        {
            line = $"Carrying {Held.DisplayName}";
            hint = $"[{use}] Drop    [{KeyName(throwKey)}] Throw";
        }
        else if (Grabbed != null)
        {
            line = $"Holding {Grabbed.DisplayName}";
            hint = Grabbed.ControlsHint + $"    [{grab}] Let go";
        }
        else
        {
            if (Current != null && Current == CurrentMovable)
                line = $"[{use}/{grab}] {Current.GetPrompt(this)}";
            else
            {
                if (Current != null) line = $"[{use}] {Current.GetPrompt(this)}";
                if (CurrentMovable != null)
                {
                    string move = $"[{grab}] {CurrentMovable.GetPrompt(this)}";
                    line = line == null ? move : line + "      " + move;
                }
            }
            if (ActiveLook != null) hint = $"Point at something and click / [{use}]    [{KeyName(exitLookKey)}] Step back";
        }

        float y = Screen.height * 0.78f;
        if (line != null) ShadowLabel(new Rect(0, y, Screen.width, 40), line, promptStyle);
        if (hint != null) ShadowLabel(new Rect(0, y + 38, Screen.width, 32), hint, smallStyle);
        if (message != null && Time.time < messageUntil)
            ShadowLabel(new Rect(0, Screen.height * 0.3f, Screen.width, 40), message, promptStyle);
    }

    static void ShadowLabel(Rect r, string text, GUIStyle style)
    {
        Color old = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.8f);
        GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), text, style);
        GUI.color = Color.white;
        GUI.Label(r, text, style);
        GUI.color = old;
    }

    public static string KeyName(KeyCode k)
    {
        switch (k)
        {
            case KeyCode.Mouse0: return "Left Click";
            case KeyCode.Mouse1: return "Right Click";
            default: return k.ToString();
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        HumanMovement m = GetComponent<HumanMovement>();
        Vector3 feet = m != null && GetComponent<CapsuleCollider>() != null ? m.FeetPosition : transform.position;
        Gizmos.DrawWireSphere(feet + Vector3.up * reachHeight, interactRange);
    }
}
