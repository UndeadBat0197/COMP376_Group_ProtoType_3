using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(BoxCollider))]
public class ThirdPersonMovement : MonoBehaviour
{
    [Header("References")]
    private Rigidbody rb;
    private BoxCollider boxCollider;
    public Transform cam;

    [Header("Movement")]
    public float walkSpeed = 4f;
    public float sprintSpeed = 8f;
    public float turnSmoothTime = 0.1f;
    float turnSmoothVelocity;
    private float currentSpeed;

    [Header("Jumping")]
    public float jumpForce = 5f;
    [Tooltip("How aggressively momentum cuts when you release Spacebar. Higher = shorter tap jumps. (0.5 = cut in half)")]
    [Range(0f, 1f)] public float jumpCutMultiplier = 0.5f;
    public int additionalJumps = 0;
    private int jumpsUsed = 0;

    // GROUND LEAVE COOLDOWN (Fixes the accidental double jump bug)
    private float groundCheckCooldown = 0f;

    // Cache input values safely
    private Vector3 inputDirection;
    private bool jumpRequested;
    private bool jumpReleased;
    private bool isGrounded;
    private bool isSprinting;
    private float calculatedTargetAngle;
    private bool hasMovementInput;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        boxCollider = GetComponent<BoxCollider>();

        rb.useGravity = true;
        rb.freezeRotation = true;

        if (rb.interpolation == RigidbodyInterpolation.None)
        {
            rb.interpolation = RigidbodyInterpolation.Interpolate;
        }
    }

    void Update()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        inputDirection = new Vector3(horizontal, 0f, vertical).normalized;

        isSprinting = Input.GetKey(KeyCode.LeftShift);

        if (Input.GetButtonDown("Jump"))
        {
            jumpRequested = true;
        }

        if (Input.GetButtonUp("Jump"))
        {
            jumpReleased = true;
        }

        if (inputDirection.magnitude >= 0.1f)
        {
            hasMovementInput = true;
            calculatedTargetAngle = Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg + cam.eulerAngles.y;
        }
        else
        {
            hasMovementInput = false;
        }
    }

    void FixedUpdate()
    {
        // Tick down the ground check cooldown timer safely inside physics updates
        if (groundCheckCooldown > 0f)
        {
            groundCheckCooldown -= Time.fixedDeltaTime;
        }

        if (hasMovementInput)
        {
            float angle = Mathf.SmoothDampAngle(rb.rotation.eulerAngles.y, calculatedTargetAngle, ref turnSmoothVelocity, turnSmoothTime);
            rb.MoveRotation(Quaternion.Euler(0f, angle, 0f));
        }

        currentSpeed = isSprinting ? sprintSpeed : walkSpeed;

        Vector3 moveDir = Quaternion.Euler(0f, cam.eulerAngles.y, 0f) * inputDirection;
        Vector3 targetVelocity = moveDir * currentSpeed;

        targetVelocity.y = rb.linearVelocity.y;
        rb.linearVelocity = targetVelocity;

        // 1. Process Initial Jump Force
        if (jumpRequested)
        {
            if (isGrounded || jumpsUsed <= additionalJumps)
            {
                rb.linearVelocity = new Vector3(rb.linearVelocity.x, jumpForce, rb.linearVelocity.z);
                jumpsUsed++;
                isGrounded = false;

                // FIX: Ignore the ground for the next 0.1 seconds so the player can lift off cleanly
                groundCheckCooldown = 0.1f;
            }
            jumpRequested = false;
        }

        // 2. PROCESS VARIABLE JUMP CUT
        if (jumpReleased)
        {
            if (rb.linearVelocity.y > 0f)
            {
                rb.linearVelocity = new Vector3(rb.linearVelocity.x, rb.linearVelocity.y * jumpCutMultiplier, rb.linearVelocity.z);
            }
            jumpReleased = false;
        }

        isGrounded = false;
    }

    void OnCollisionStay(Collision collision)
    {
        // FIX: If the liftoff cooldown timer is running, refuse to count grounding physics rules
        if (groundCheckCooldown > 0f) return;

        foreach (ContactPoint contact in collision.contacts)
        {
            float localColliderBottom = transform.position.y + boxCollider.center.y - (boxCollider.size.y * 0.5f);

            if (contact.normal.y > 0.7f && contact.point.y <= (localColliderBottom + 0.05f))
            {
                isGrounded = true;
                jumpsUsed = 0;
                break;
            }
        }
    }
}
