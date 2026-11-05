using UnityEngine;

/// <summary>
/// Switches between Mouse and Human. Attach to an always-active GameObject,
/// not to one of the character forms.
/// Compatible with FormWheelUI (BecomeHuman / BecomeMouse and allowKeySwitch).
/// </summary>
public class FormSwitcher : MonoBehaviour
{
    public GameObject mouseForm;
    public GameObject humanForm;
    public bool startAsHuman = false;

    [Header("Input")]
    public bool allowKeySwitch = true;
    public KeyCode switchKey = KeyCode.T;

    [Header("Human Transformation Clearance")]
    [Tooltip("Disable temporarily to test switching without collision clearance checks.")]
    public bool checkHumanSpace = true;
    [Tooltip("Log the object that prevents transformation into Human.")]
    public bool debugSpaceCheck = true;

    public bool IsHuman { get; private set; }

    private string message;
    private float messageUntil;

    private void Start()
    {
        IsHuman = startAsHuman;
        if (mouseForm != null) mouseForm.SetActive(!IsHuman);
        if (humanForm != null) humanForm.SetActive(IsHuman);
    }

    private void Update()
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
        HumanMovement movement = humanForm.GetComponent<HumanMovement>();
        Vector3 rootAboveFeet = movement != null
            ? humanForm.transform.position - movement.FeetPosition
            : Vector3.zero;
        Vector3 rootPos = floor + Vector3.up * rootAboveFeet.y;

        if (checkHumanSpace && !HasRoomForHuman(rootPos))
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

        Vector3 position = humanForm.transform.position + Vector3.up * 0.05f;
        Swap(humanForm, mouseForm, position);
        IsHuman = false;
        return true;
    }

    private void Swap(GameObject from, GameObject to, Vector3 position)
    {
        float yaw = from.transform.eulerAngles.y;
        Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);

        from.SetActive(false);
        to.transform.SetPositionAndRotation(position, rotation);

        Rigidbody body = to.GetComponent<Rigidbody>();
        if (body != null)
        {
            body.position = position;
            body.rotation = rotation;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        to.SetActive(true);
    }

    private Vector3 FindFloor(Vector3 from, GameObject ignore)
    {
        RaycastHit[] hits = Physics.RaycastAll(
            from + Vector3.up * 0.5f,
            Vector3.down,
            5f,
            ~0,
            QueryTriggerInteraction.Ignore);

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null) continue;
            if (hit.collider.transform.IsChildOf(ignore.transform)) continue;
            if (humanForm != null && hit.collider.transform.IsChildOf(humanForm.transform)) continue;
            return hit.point;
        }

        return from;
    }

    private bool HasRoomForHuman(Vector3 rootPos)
    {
        CapsuleCollider capsule = humanForm.GetComponent<CapsuleCollider>();
        if (capsule == null) return true;

        float scaleXZ = Mathf.Max(
            Mathf.Abs(humanForm.transform.lossyScale.x),
            Mathf.Abs(humanForm.transform.lossyScale.z));
        float scaleY = Mathf.Abs(humanForm.transform.lossyScale.y);
        float radius = capsule.radius * scaleXZ * 0.95f;
        float height = Mathf.Max(capsule.height * scaleY, radius * 2f);
        Vector3 center = rootPos + Quaternion.Euler(0f, mouseForm.transform.eulerAngles.y, 0f)
            * Vector3.Scale(capsule.center, humanForm.transform.lossyScale);
        float halfAxis = Mathf.Max(0f, height * 0.5f - radius);
        Vector3 p1 = center + Vector3.up * halfAxis;
        Vector3 p2 = center - Vector3.up * halfAxis + Vector3.up * 0.05f;

        Collider[] hits = Physics.OverlapCapsule(
            p1, p2, radius, ~0, QueryTriggerInteraction.Ignore);

        foreach (Collider hit in hits)
        {
            if (hit == null) continue;
            if (mouseForm != null && hit.transform.IsChildOf(mouseForm.transform)) continue;
            if (humanForm != null && hit.transform.IsChildOf(humanForm.transform)) continue;

            if (debugSpaceCheck)
            {
                Debug.LogWarning(
                    "Human transformation blocked by: " + hit.name +
                    " (layer: " + LayerMask.LayerToName(hit.gameObject.layer) + ")",
                    hit.gameObject);
            }
            return false;
        }
        return true;
    }

    private void Flash(string text)
    {
        message = text;
        messageUntil = Time.time + 2f;
        Debug.Log(text);
    }

    private void OnGUI()
    {
        if (string.IsNullOrEmpty(message) || Time.time > messageUntil) return;
        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 20,
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold
        };
        GUI.Label(new Rect(0, Screen.height * 0.2f, Screen.width, 40), message, style);
    }
}
