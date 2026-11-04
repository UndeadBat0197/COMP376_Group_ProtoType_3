using UnityEngine;

/// <summary>
/// Cabinet door (hinge) or drawer (slide) that toggles open/closed with E.
/// Put this on the PIVOT object: for a hinged door that's an empty at the hinge edge, with the
/// door mesh + collider as a child. Add a kinematic Rigidbody to the pivot so it shoves things
/// properly when it swings.
/// </summary>
public class Openable : Interactable
{
    public enum Mode { Hinge, Slide }

    [Header("Motion")]
    public Mode mode = Mode.Hinge;
    [Tooltip("Local axis to rotate around (Hinge).")]
    public Vector3 hingeAxis = Vector3.up;
    [Tooltip("Degrees when fully open. Flip the sign to swing the other way.")]
    public float openAngle = 100f;
    [Tooltip("Local offset when fully open (Slide / drawers).")]
    public Vector3 slideOffset = new Vector3(0f, 0f, 0.4f);
    [Tooltip("Open/close cycles per second.")]
    public float speed = 2.5f;
    public bool startOpen;

    [Header("Lock")]
    public bool locked;
    public string lockedMessage = "It's locked.";

    [Header("Linked")]
    [Tooltip("Other doors that open/close together with this one (e.g. double doors).")]
    public Openable[] linked;

    [Header("Prompt")]
    public string openVerb = "Open";
    public string closeVerb = "Close";

    public bool IsOpen { get; private set; }
    /// <summary>0 = closed, 1 = open.</summary>
    public float Progress => t;

    Quaternion closedRot;
    Vector3 closedPos;
    float t;
    Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;
        closedRot = transform.localRotation;
        closedPos = transform.localPosition;
        IsOpen = startOpen;
        t = startOpen ? 1f : 0f;
        Apply(true);
    }

    public override string GetPrompt(HumanInteractor who)
    {
        if (locked) return $"{DisplayName} (locked)";
        return $"{(IsOpen ? closeVerb : openVerb)} {DisplayName}";
    }

    public override void Interact(HumanInteractor who)
    {
        if (locked)
        {
            who.ShowMessage(lockedMessage);
            return;
        }
        SetOpen(!IsOpen);
    }

    public void SetOpen(bool open)
    {
        if (IsOpen == open) return;
        IsOpen = open;
        if (linked == null) return;
        foreach (Openable o in linked)
            if (o != null && o != this) o.SetOpenNoPropagate(open);
    }

    void SetOpenNoPropagate(bool open) { IsOpen = open; }

    public void Unlock() { locked = false; }
    public void Lock() { locked = true; }

    void FixedUpdate()
    {
        float target = IsOpen ? 1f : 0f;
        if (Mathf.Approximately(t, target)) return;
        t = Mathf.MoveTowards(t, target, speed * Time.fixedDeltaTime);
        Apply(false);
    }

    void Apply(bool instant)
    {
        float e = Mathf.SmoothStep(0f, 1f, t);
        Quaternion localRot = closedRot;
        Vector3 localPos = closedPos;

        if (mode == Mode.Hinge) localRot = closedRot * Quaternion.AngleAxis(openAngle * e, hingeAxis);
        else localPos = closedPos + closedRot * slideOffset * e;

        if (rb != null && !instant)
        {
            Transform parent = transform.parent;
            rb.MoveRotation(parent ? parent.rotation * localRot : localRot);
            rb.MovePosition(parent ? parent.TransformPoint(localPos) : localPos);
        }
        else
        {
            transform.localRotation = localRot;
            transform.localPosition = localPos;
        }
    }
}
