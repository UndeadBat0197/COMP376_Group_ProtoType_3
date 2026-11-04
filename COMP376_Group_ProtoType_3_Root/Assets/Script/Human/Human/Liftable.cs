using System.Collections;
using UnityEngine;

/// <summary>
/// Something the human can pick up, carry, drop and throw (rock, cheese, book...).
/// Needs a collider. A Rigidbody is optional: items sitting on a shelf or inside a crate can have
/// none, so they ride along as part of that furniture; one is added when picked up.
/// Trigger colliders (e.g. cheese hidden under a boulder) become solid once picked up.
/// The item is moved with physics while carried, so it still bumps into walls;
/// if it gets snagged too far from the hands it is dropped automatically.
/// </summary>
public class Liftable : Interactable
{
    [Header("Carrying")]
    [Tooltip("Player walk speed multiplier while carrying this. Heavier things = lower.")]
    [Range(0.1f, 1f)] public float carrySpeedMultiplier = 0.8f;
    [Tooltip("How fast the item catches up to the hands.")]
    public float followSpeed = 20f;
    [Tooltip("Drop the item if it gets stuck this far from the hands.")]
    public float breakDistance = 1.2f;
    [Tooltip("Rotate to keep facing the same way relative to the player.")]
    public bool keepRelativeRotation = true;
    [Tooltip("Mass used if this item has no Rigidbody until it's picked up.")]
    public float massIfNoBody = 1f;

    [Header("Throwing")]
    [Tooltip("Impulse applied on throw. Divided by mass, so heavy things fly less.")]
    public float throwImpulse = 25f;
    [Range(0f, 1f)] public float throwUpwardBias = 0.2f;

    public bool IsHeld => holder != null;

    Rigidbody rb;
    Collider[] myColliders;
    HumanInteractor holder;
    Quaternion relativeRotation;
    bool settling;
    float settleUntil;

    bool savedGravity;
    float savedDamping, savedAngularDamping;
    RigidbodyInterpolation savedInterp;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        myColliders = GetComponentsInChildren<Collider>();
    }

    public override bool CanInteract(HumanInteractor who) => base.CanInteract(who) && !IsHeld;

    public override string GetPrompt(HumanInteractor who) => $"Pick up {DisplayName}";

    public override void Interact(HumanInteractor who) => PickUp(who);

    public void PickUp(HumanInteractor who)
    {
        if (IsHeld || who.Held != null || who.Grabbed != null) return;

        holder = who;
        who.SetHeld(this);

        // Riding inside furniture? Detach so it doesn't drag the furniture along.
        if (transform.parent != null && transform.parent.GetComponentInParent<Rigidbody>() != null)
            transform.SetParent(null, true);
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
            rb.mass = massIfNoBody;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        }
        foreach (Collider c in myColliders)
            if (c != null && c.isTrigger) c.isTrigger = false;

        savedGravity = rb.useGravity;
        savedDamping = rb.linearDamping;
        savedAngularDamping = rb.angularDamping;
        savedInterp = rb.interpolation;

        rb.isKinematic = false;
        rb.useGravity = false;
        rb.linearDamping = 5f;
        rb.angularDamping = 5f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        relativeRotation = Quaternion.Inverse(who.transform.rotation) * rb.rotation;

        SetIgnoreCollisions(who, true);

        // Let it pass through shelf edges etc. on its way to the hands, then collide normally.
        settling = true;
        settleUntil = Time.time + 0.5f;
        rb.detectCollisions = false;

        who.Movement.SpeedMultiplier = carrySpeedMultiplier;
        who.Movement.IgnoreGroundBody = rb;
    }

    public void Drop()
    {
        if (!IsHeld) return;
        Release();
    }

    public void Throw()
    {
        if (!IsHeld) return;
        HumanInteractor who = holder;

        Vector3 dir = who.viewCamera != null ? who.viewCamera.transform.forward : who.transform.forward;
        // Don't throw into the floor if the camera is looking down at us.
        dir = Vector3.ProjectOnPlane(dir, Vector3.up).normalized;
        if (dir.sqrMagnitude < 0.01f) dir = who.transform.forward;
        dir = (dir + Vector3.up * throwUpwardBias).normalized;

        Release();
        rb.AddForce(dir * throwImpulse, ForceMode.Impulse);
    }

    void Release()
    {
        HumanInteractor who = holder;
        holder = null;
        settling = false;
        rb.detectCollisions = true;

        rb.useGravity = savedGravity;
        rb.linearDamping = savedDamping;
        rb.angularDamping = savedAngularDamping;
        rb.interpolation = savedInterp;

        who.ClearHeld(this);
        who.Movement.SpeedMultiplier = 1f;
        if (who.Movement.IgnoreGroundBody == rb) who.Movement.IgnoreGroundBody = null;

        if (isActiveAndEnabled) StartCoroutine(RestoreCollisionsWhenClear(who));
        else SetIgnoreCollisions(who, false);
    }

    void FixedUpdate()
    {
        if (holder == null) return;

        Vector3 target = holder.holdPoint.position;
        Vector3 delta = target - rb.position;

        if (settling && (delta.magnitude < 0.15f || Time.time > settleUntil))
        {
            settling = false;
            rb.detectCollisions = true;
        }

        if (!settling && delta.magnitude > breakDistance)
        {
            holder.ShowMessage($"Dropped the {DisplayName}", 1.2f);
            Release();
            return;
        }

        rb.linearVelocity = Vector3.ClampMagnitude(delta * followSpeed, 15f);

        if (keepRelativeRotation)
        {
            Quaternion targetRot = holder.transform.rotation * relativeRotation;
            Quaternion diff = targetRot * Quaternion.Inverse(rb.rotation);
            diff.ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180f) angle -= 360f;
            if (Mathf.Abs(angle) > 0.01f && !float.IsInfinity(axis.x))
                rb.angularVelocity = axis * (angle * Mathf.Deg2Rad * 10f);
            else
                rb.angularVelocity = Vector3.zero;
        }
    }

    void SetIgnoreCollisions(HumanInteractor who, bool ignore)
    {
        foreach (Collider mine in myColliders)
            foreach (Collider theirs in who.OwnColliders)
                if (mine != null && theirs != null)
                    Physics.IgnoreCollision(mine, theirs, ignore);
    }

    // Re-enable player<->item collisions only once they no longer overlap, so the item doesn't pop.
    IEnumerator RestoreCollisionsWhenClear(HumanInteractor who)
    {
        float timeout = Time.time + 3f;
        while (Time.time < timeout && Overlapping(who))
            yield return new WaitForFixedUpdate();
        if (holder == null) SetIgnoreCollisions(who, false);
    }

    bool Overlapping(HumanInteractor who)
    {
        foreach (Collider mine in myColliders)
            foreach (Collider theirs in who.OwnColliders)
                if (mine != null && theirs != null && !theirs.isTrigger && mine.bounds.Intersects(theirs.bounds))
                    return true;
        return false;
    }
}
