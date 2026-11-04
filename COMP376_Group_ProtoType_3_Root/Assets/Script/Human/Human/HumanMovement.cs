using UnityEngine;

/// <summary>
/// Third-person movement for the human form. Same feel as the mouse's ThirdPersonMovement
/// (camera-relative WASD, Shift sprint, Space jump) but sized for a capsule and with hooks
/// the interaction scripts use: speed multiplier while carrying, "grab mode" while pushing,
/// and InputEnabled for look mode / cutscenes.
/// </summary>
[DefaultExecutionOrder(-50)] // run before Pushable/Liftable so they can read this frame's motion
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class HumanMovement : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Main Camera transform. Auto-filled with Camera.main if empty.")]
    public Transform cam;

    [Header("Movement")]
    public float walkSpeed = 3.5f;
    public float sprintSpeed = 6f;
    public float turnSmoothTime = 0.1f;

    [Header("Jumping")]
    public float jumpForce = 4.5f;
    public LayerMask groundMask = ~0;

    // --- Hooks used by the interaction scripts ---
    public bool InputEnabled { get; set; } = true;
    public float SpeedMultiplier { get; set; } = 1f;
    public bool IsGrounded { get; private set; }
    public bool IsGrabbing => grabMode;
    /// <summary>A body to ignore in the ground check (the thing we're carrying).</summary>
    public Rigidbody IgnoreGroundBody { get; set; }

    /// <summary>World position of the bottom of the capsule (the root may sit higher, e.g. mid-body).</summary>
    public Vector3 FeetPosition
    {
        get
        {
            if (capsule == null) capsule = GetComponent<CapsuleCollider>();
            Vector3 c = transform.TransformPoint(capsule.center);
            return c - Vector3.up * (capsule.height * 0.5f * transform.lossyScale.y);
        }
    }

    /// <summary>Where the body will be after this physics step (used by Pushable).</summary>
    public Vector3 NextPosition { get; private set; }
    public Quaternion NextRotation { get; private set; }

    Rigidbody rb;
    CapsuleCollider capsule;
    float inputH, inputV;
    Vector3 inputDir;
    bool sprinting, jumpRequested;
    float turnVelocity;

    bool grabMode;
    bool grabAllowBackward;
    float grabTurnSpeed;
    float grabSpeedMul = 1f;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();
        rb.useGravity = true;
        rb.freezeRotation = true;
        if (rb.interpolation == RigidbodyInterpolation.None)
            rb.interpolation = RigidbodyInterpolation.Interpolate;
        NextPosition = rb.position;
        NextRotation = rb.rotation;
    }

    void Start()
    {
        if (cam == null && Camera.main != null) cam = Camera.main.transform;
    }

    void OnDisable()
    {
        inputH = inputV = 0f;
        inputDir = Vector3.zero;
        jumpRequested = false;
    }

    void Update()
    {
        if (!InputEnabled)
        {
            inputH = inputV = 0f;
            inputDir = Vector3.zero;
            jumpRequested = false;
            return;
        }

        inputH = Input.GetAxisRaw("Horizontal");
        inputV = Input.GetAxisRaw("Vertical");
        inputDir = Vector3.ClampMagnitude(new Vector3(inputH, 0f, inputV), 1f);
        sprinting = Input.GetKey(KeyCode.LeftShift) && !grabMode;

        if (Input.GetButtonDown("Jump") && !grabMode) jumpRequested = true;
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        IsGrounded = CheckGround();

        Vector3 velocity = rb.linearVelocity;
        Vector3 move = Vector3.zero;
        Quaternion rotation = rb.rotation;

        if (grabMode)
        {
            // Tank-style while holding furniture: W/S along facing, A/D turns.
            float v = grabAllowBackward ? inputV : Mathf.Max(0f, inputV);
            move = transform.forward * v * walkSpeed * grabSpeedMul;

            if (grabTurnSpeed > 0f && Mathf.Abs(inputH) > 0.01f)
            {
                rotation = rb.rotation * Quaternion.Euler(0f, inputH * grabTurnSpeed * dt, 0f);
                rb.MoveRotation(rotation);
            }
        }
        else if (inputDir.sqrMagnitude > 0.01f)
        {
            float camYaw = cam != null ? cam.eulerAngles.y : 0f;
            float targetAngle = Mathf.Atan2(inputDir.x, inputDir.z) * Mathf.Rad2Deg + camYaw;
            float angle = Mathf.SmoothDampAngle(rb.rotation.eulerAngles.y, targetAngle, ref turnVelocity, turnSmoothTime);
            rotation = Quaternion.Euler(0f, angle, 0f);
            rb.MoveRotation(rotation);

            Vector3 dir = Quaternion.Euler(0f, camYaw, 0f) * inputDir;
            move = dir * (sprinting ? sprintSpeed : walkSpeed) * SpeedMultiplier;
        }

        velocity.x = move.x;
        velocity.z = move.z;

        if (jumpRequested)
        {
            if (IsGrounded) velocity.y = jumpForce;
            jumpRequested = false;
        }

        rb.linearVelocity = velocity;
        NextPosition = rb.position + velocity * dt;
        NextRotation = rotation;
    }

    bool CheckGround()
    {
        float radius = capsule.radius * 0.9f;
        Vector3 center = transform.TransformPoint(capsule.center);
        Vector3 bottomSphere = center - Vector3.up * (capsule.height * 0.5f - capsule.radius);
        Vector3 probe = bottomSphere - Vector3.up * 0.08f;

        Collider[] hits = Physics.OverlapSphere(probe, radius, groundMask, QueryTriggerInteraction.Ignore);
        foreach (Collider c in hits)
        {
            if (c.attachedRigidbody == rb) continue;
            if (IgnoreGroundBody != null && c.attachedRigidbody == IgnoreGroundBody) continue;
            return true;
        }
        return false;
    }

    /// <summary>Snap to face a direction (used when grabbing furniture).</summary>
    public void FaceDirection(Vector3 worldDir)
    {
        worldDir.y = 0f;
        if (worldDir.sqrMagnitude < 0.0001f) return;
        Quaternion rot = Quaternion.LookRotation(worldDir.normalized, Vector3.up);
        rb.rotation = rot;
        transform.rotation = rot;
        NextRotation = rot;
    }

    public void BeginGrab(float speedMultiplier, bool allowBackward, float turnSpeed)
    {
        grabMode = true;
        grabSpeedMul = speedMultiplier;
        grabAllowBackward = allowBackward;
        grabTurnSpeed = turnSpeed;
        jumpRequested = false;
    }

    public void EndGrab()
    {
        grabMode = false;
        grabSpeedMul = 1f;
    }

    /// <summary>Stop all horizontal motion immediately (e.g. on transform).</summary>
    public void Halt()
    {
        rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
    }
}
