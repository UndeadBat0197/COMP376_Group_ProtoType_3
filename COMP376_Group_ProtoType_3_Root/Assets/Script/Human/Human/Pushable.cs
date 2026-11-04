using UnityEngine;

/// <summary>
/// Heavy things the human must deliberately grab to move (cabinet, crate, boulder, armchair).
/// Locked in place until grabbed, so walking into it does nothing.
/// Grab (F, or E when it's the only thing to do): the player squares up to the nearest face,
/// then W pushes and S pulls (A/D turns if canRotate). F/E again lets go.
/// If it snags on something, the grip breaks.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class Pushable : Interactable
{
    [Header("Handling")]
    [Tooltip("Walk backwards (S) to drag it toward you.")]
    public bool allowPull = true;
    [Tooltip("A/D turns it while holding. Off = straight push/pull only.")]
    public bool canRotate = false;
    public float turnSpeed = 70f;
    [Tooltip("Player speed multiplier while moving this.")]
    [Range(0.1f, 1f)] public float speedMultiplier = 0.5f;
    [Tooltip("Let go if it gets stuck this far from where it should be.")]
    public float breakDistance = 0.6f;

    [Header("Behaviour")]
    [Tooltip("Can't be shoved by walking into it — only moves while grabbed.")]
    public bool lockWhenIdle = true;
    [Tooltip("When grabbing, face the nearest side squarely (good for boxes).")]
    public bool snapToSides = true;

    public bool IsGrabbed => holder != null;

    public string ControlsHint
    {
        get
        {
            string move = allowPull ? "[W] Push   [S] Pull" : "[W] Push";
            return canRotate ? move + "   [A/D] Turn" : move;
        }
    }

    Rigidbody rb;
    HumanInteractor holder;
    Vector3 localOffset;
    Quaternion localRotation;
    RigidbodyConstraints savedConstraints;
    RigidbodyInterpolation savedInterp;
    bool savedKinematic;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (lockWhenIdle) rb.isKinematic = true;
    }

    public override bool CanInteract(HumanInteractor who) => base.CanInteract(who) && !IsGrabbed;

    public override string GetPrompt(HumanInteractor who) => $"Move {DisplayName}";

    public override void Interact(HumanInteractor who) => Grab(who);

    public void Grab(HumanInteractor who)
    {
        if (IsGrabbed || who.Held != null || who.Grabbed != null) return;
        if (!who.Movement.IsGrounded)
        {
            who.ShowMessage("Need your feet on the ground", 1f);
            return;
        }

        holder = who;

        Vector3 toObject = rb.worldCenterOfMass - who.transform.position;
        toObject.y = 0f;
        if (snapToSides) toObject = NearestSide(toObject);
        who.Movement.FaceDirection(toObject);

        Transform p = who.transform;
        localOffset = Quaternion.Inverse(p.rotation) * (rb.position - p.position);
        localRotation = Quaternion.Inverse(p.rotation) * rb.rotation;

        savedKinematic = rb.isKinematic;
        savedConstraints = rb.constraints;
        savedInterp = rb.interpolation;
        rb.isKinematic = false;
        rb.constraints = savedConstraints | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.WakeUp();

        who.SetGrabbed(this);
        who.Movement.BeginGrab(speedMultiplier, allowPull, canRotate ? turnSpeed : 0f);
    }

    public void Release()
    {
        if (!IsGrabbed) return;
        HumanInteractor who = holder;
        holder = null;

        if (!rb.isKinematic)
        {
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
            rb.angularVelocity = Vector3.zero;
        }
        rb.constraints = savedConstraints;
        rb.interpolation = savedInterp;
        rb.isKinematic = lockWhenIdle || savedKinematic;

        who.ClearGrabbed(this);
        who.Movement.EndGrab();
    }

    void FixedUpdate()
    {
        if (holder == null) return;

        float dt = Time.fixedDeltaTime;
        HumanMovement m = holder.Movement;

        // Where the player will be after this step -> where the object should be.
        Vector3 target = m.NextPosition + m.NextRotation * localOffset;
        Vector3 delta = target - rb.position;
        delta.y = 0f;

        if (delta.magnitude > breakDistance)
        {
            holder.ShowMessage($"The {DisplayName} is stuck", 1.2f);
            Release();
            return;
        }

        Vector3 v = Vector3.ClampMagnitude(delta / dt, 8f);
        v.y = rb.linearVelocity.y;
        rb.linearVelocity = v;

        Quaternion targetRot = m.NextRotation * localRotation;
        float yawDelta = Mathf.DeltaAngle(rb.rotation.eulerAngles.y, targetRot.eulerAngles.y);
        rb.angularVelocity = new Vector3(0f, Mathf.Clamp(yawDelta * Mathf.Deg2Rad / dt, -10f, 10f), 0f);
    }

    // Pick whichever of the object's four horizontal sides the player is facing.
    Vector3 NearestSide(Vector3 dir)
    {
        if (dir.sqrMagnitude < 0.0001f) return dir;
        Vector3 f = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        Vector3 r = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
        Vector3[] sides = { f, -f, r, -r };
        Vector3 best = dir;
        float bestDot = float.MinValue;
        foreach (Vector3 s in sides)
        {
            float d = Vector3.Dot(dir.normalized, s);
            if (d > bestDot) { bestDot = d; best = s; }
        }
        return best;
    }

    void OnDisable()
    {
        Release();
    }
}
