using UnityEngine;

/// <summary>
/// Swaps between the mouse and the human at the same spot. Put this on its own GameObject
/// (NOT on either form, since they get deactivated). Each form keeps its own FreeLook camera
/// as a child, so turning the form on/off also switches the camera.
///
/// Call BecomeHuman() / BecomeMouse() from your game logic, or press the debug key.
/// </summary>
public class FormSwitcher : MonoBehaviour
{
    public GameObject mouseForm;
    public GameObject humanForm;
    public bool startAsHuman = false;

    [Header("Debug")]
    public bool allowKeySwitch = true;
    public KeyCode switchKey = KeyCode.T;

    public bool IsHuman { get; private set; }

    string message;
    float messageUntil;

    void Start()
    {
        IsHuman = startAsHuman;
        if (mouseForm != null) mouseForm.SetActive(!IsHuman);
        if (humanForm != null) humanForm.SetActive(IsHuman);
    }

    void Update()
    {
        if (allowKeySwitch && Input.GetKeyDown(switchKey)) Toggle();
    }

    public void Toggle()
    {
        if (IsHuman) BecomeMouse();
        else BecomeHuman();
    }

    public bool BecomeHuman()
    {
        if (IsHuman || mouseForm == null || humanForm == null) return false;

        Vector3 floor = FindFloor(mouseForm.transform.position, mouseForm);
        // The human's root may not be at its feet (e.g. pivot mid-body), so keep that offset.
        HumanMovement hm = humanForm.GetComponent<HumanMovement>();
        Vector3 rootAboveFeet = hm != null ? humanForm.transform.position - hm.FeetPosition : Vector3.zero;
        Vector3 rootPos = floor + Vector3.up * rootAboveFeet.y;

        if (!HasRoomForHuman(rootPos))
        {
            Flash("Not enough room to transform here");
            return false;
        }

        Swap(mouseForm, humanForm, rootPos);
        IsHuman = true;
        return true;
    }

    public bool BecomeMouse()
    {
        if (!IsHuman || mouseForm == null || humanForm == null) return false;

        Vector3 pos = humanForm.transform.position + Vector3.up * 0.05f;
        Swap(humanForm, mouseForm, pos);
        IsHuman = false;
        return true;
    }

    void Swap(GameObject from, GameObject to, Vector3 position)
    {
        float yaw = from.transform.eulerAngles.y;
        from.SetActive(false);

        to.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        Rigidbody rb = to.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.position = position;
            rb.rotation = Quaternion.Euler(0f, yaw, 0f);
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        to.SetActive(true);
    }

    Vector3 FindFloor(Vector3 from, GameObject ignore)
    {
        RaycastHit[] hits = Physics.RaycastAll(from + Vector3.up * 0.5f, Vector3.down, 5f, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit h in hits)
            if (!h.collider.transform.IsChildOf(ignore.transform)) return h.point;
        return from;
    }

    bool HasRoomForHuman(Vector3 rootPos)
    {
        CapsuleCollider cap = humanForm.GetComponent<CapsuleCollider>();
        if (cap == null) return true;

        float r = cap.radius * 0.95f;
        Vector3 c = rootPos + cap.center;
        Vector3 p1 = c + Vector3.up * (cap.height * 0.5f - cap.radius);
        Vector3 p2 = c - Vector3.up * (cap.height * 0.5f - cap.radius) + Vector3.up * 0.05f;

        foreach (Collider hit in Physics.OverlapCapsule(p1, p2, r, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform.IsChildOf(mouseForm.transform) || hit.transform.IsChildOf(humanForm.transform)) continue;
            return false;
        }
        return true;
    }

    void Flash(string text)
    {
        message = text;
        messageUntil = Time.time + 2f;
        Debug.Log(text);
    }

    void OnGUI()
    {
        if (message == null || Time.time > messageUntil) return;
        GUIStyle s = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        GUI.Label(new Rect(0, Screen.height * 0.2f, Screen.width, 40), message, s);
    }
}
