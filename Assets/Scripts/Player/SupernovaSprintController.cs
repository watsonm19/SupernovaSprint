// ═════════════════════════════════════════════════════════════════════════════
//  SupernovaSprintController.cs
//  High-speed momentum platformer controller — SA2: Battle inspired.
//
//  SETUP:
//    1. Attach this script to your player root GameObject.
//    2. The [RequireComponent] below will auto-add Rigidbody.
//    3. Assign 'visualModel' to the child mesh transform.
//    4. Assign 'cameraTransform' or leave null (auto-finds Camera.main).
//    5. Set 'groundLayers' to your terrain/platform layer(s).
//    6. Tag any homing-attackable objects with "Target".
//
//  INPUT (direct device polling — no PlayerInput component needed):
//    Gamepad left stick / WASD + arrow keys  → Move
//    Gamepad south button / Space            → Jump (grounded) / Homing Attack (airborne)
// ═════════════════════════════════════════════════════════════════════════════

using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class SupernovaSprintController : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    #region Inspector Variables

    [Header("── Movement ──────────────────────────────────────────────────")]
    [Tooltip("Maximum flat-surface speed (m/s).")]
    public float topSpeed = 25f;

    [Tooltip("Base acceleration force. Lower values give a longer 0→topSpeed ramp.\n" +
             "At 80 the player reaches topSpeed in ~0.375 s on flat ground.")]
    public float acceleration = 35f;

    [Tooltip("Bonus force applied when the player runs downhill. Makes slopes feel rewarding.")]
    public float slopeForce = 40f;

    [Tooltip("How quickly the player turns to face the input direction (degrees/sec proxy).")]
    public float turnSpeed = 12f;

    [Header("── Jump ─────────────────────────────────────────────────────")]
    [Tooltip("Immediate upward impulse on jump. This is a velocity change, not a force.")]
    public float jumpForce = 15f;

    [Tooltip("Sustained upward force while jump is held (variable height).")]
    public float jumpHoldForce = 8f;

    [Tooltip("Max seconds the hold force is applied after pressing jump.")]
    public float jumpHoldTime = 0.2f;

    [Tooltip("Window after walking off a ledge where the player can still jump (seconds).")]
    public float coyoteTime = 0.12f;

    [Tooltip("A jump press is remembered this long before landing (seconds).")]
    public float jumpBufferTime = 0.15f;

    [Header("── Homing Attack ─────────────────────────────────────────────")]
    [Tooltip("Search radius for valid homing targets.")]
    public float homingRange = 15f;

    [Tooltip("Travel speed during the homing attack.")]
    public float homingSpeed = 40f;

    [Tooltip("Upward impulse applied after a successful homing hit (allows chaining).")]
    public float homingBounceForce = 12f;

    [Tooltip("Time-scale freeze duration on homing impact — the SA2 'crunch' (seconds).")]
    public float homingFreezeFrameDuration = 0.05f;

    [Tooltip("Duration of the forward dash when no target is in range (seconds).")]
    public float homingDashDuration = 0.35f;

    [Header("── Gravity ──────────────────────────────────────────────────")]
    [Tooltip("Custom gravity magnitude (replaces Unity's built-in gravity).")]
    public float gravityForce = 25f;

    [Tooltip("Extra downforce applied when grounded, keeping the player glued to loops.")]
    public float groundStickyForce = 15f;

    [Header("── Surface Detection ──────────────────────────────────────────")]
    [Tooltip("Radius of the SphereCast. Set to ~40% of the capsule collider radius.")]
    public float groundCheckRadius = 0.4f;

    [Tooltip("Cast distance. Set to capsule half-height + a small margin (~1.1 for a 2m capsule).")]
    public float groundCheckDistance = 1.1f;

    [Tooltip("Speed at which the player's transform.up aligns to the surface normal.")]
    public float surfaceAlignSpeed = 12f;

    [Tooltip("Layers treated as ground. IMPORTANT: exclude the Player layer to avoid self-hits.")]
    public LayerMask groundLayers = ~0;

    [Header("── Skate Friction (SA2 drift feel) ─────────────────────────")]
    [Tooltip("Max friction force applied when braking or turning sharply.")]
    public float brakeFriction = 10f;

    [Tooltip("Input-vs-velocity angle at which full brake friction kicks in (degrees).")]
    [Range(10f, 90f)]
    public float brakeFrictionAngle = 45f;

    [Tooltip("Minimal coasting friction applied when no input is held while grounded.")]
    public float rollingFriction = 0.5f;

    [Header("── Air Control ───────────────────────────────────────────────")]
    [Tooltip("Fraction of 'acceleration' available in the air for strafing. SA2 feel: 0.25.")]
    [Range(0f, 1f)]
    public float airControlFactor = 0.25f;

    [Tooltip("Horizontal speed cap while airborne (separate from topSpeed).")]
    public float maxAirStrafeSpeed = 20f;

    [Header("── Visual Lean ───────────────────────────────────────────────")]
    [Tooltip("Child transform of the visible mesh. Only this object is tilted, NOT the physics body.")]
    public Transform visualModel;

    [Tooltip("Maximum lean angle in degrees when strafing at full input.")]
    public float maxLeanAngle = 25f;

    [Tooltip("Lean interpolation speed. Higher = snappier.")]
    public float leanSpeed = 8f;

    [Header("── Camera ────────────────────────────────────────────────────")]
    [Tooltip("Leave null to auto-assign Camera.main on Awake.")]
    public Transform cameraTransform;

    [Header("── Polarity / Rocket Mode ─────────────────────────────────────")]
    [Tooltip("Top speed while Rocket Mode is active.")]
    public float rocketTopSpeed = 50f;

    [Tooltip("Acceleration force while Rocket Mode is active.")]
    public float rocketAcceleration = 70f;

    [Tooltip("Brake friction while Rocket Mode is active — near-zero for maximum slip.")]
    public float rocketBrakeFriction = 1f;

    [Tooltip("Rolling friction while Rocket Mode is active.")]
    public float rocketRollingFriction = 0.1f;

    [Tooltip("Ground sticky force while Rocket Mode is active (reduced — loosens surface grip).")]
    public float rocketGroundStickyForce = 5f;

    [Tooltip("Surface align speed while Rocket Mode is active (sluggish — player drifts off curves).")]
    public float rocketSurfaceAlignSpeed = 3f;

    [Tooltip("Camera FOV in Normal Mode.")]
    public float normalFOV = 60f;

    [Tooltip("Camera FOV in Rocket Mode.")]
    public float rocketFOV = 75f;

    [Tooltip("Speed at which the camera FOV lerps between modes (higher = snappier).")]
    public float fovLerpSpeed = 8f;

    #endregion
    // ─────────────────────────────────────────────────────────────────────────
    #region Private State

    private Rigidbody rb;

    // Ground
    private bool    isGrounded;
    private Vector3 groundNormal   = Vector3.up;
    private float   coyoteTimer;
    private float   jumpGroundIgnoreTimer; // Prevents re-grounding immediately after a jump

    // Input  (written by GatherInput() in Update, consumed in FixedUpdate)
    private Vector2 moveInput;
    private bool    jumpHeld;
    private float   jumpBufferTimer; // Counts down; >0 = a jump was recently pressed

    // Jump
    private bool    isJumping;       // True while variable-height hold window is open
    private float   jumpHoldTimer;

    // Homing
    private bool    homingAvailable; // Refreshed each time the player lands

    // State
    private enum PlayerState { Grounded, Airborne, HomingAttack }
    private PlayerState state = PlayerState.Airborne;

    // Public read-only diagnostics (useful for a HUD speed counter)
    [System.NonSerialized] public float currentSpeed;

    // Set true by LoopBoostTrigger to allow temporary overspeed through a loop.
    // Self-clears in ClampSpeed() once gravity slows the player to topSpeed,
    // or after the safety timeout (whichever comes first).
    [System.NonSerialized] public bool  overrideSpeedCap;
    [System.NonSerialized] public float overrideSpeedCapTimer;

    // True while Rocket Mode (Polarity toggle) is active — readable by other scripts (e.g. HUD).
    [System.NonSerialized] public bool isRocketMode;

    // Animator-readable state flags — written each FixedUpdate by UpdateState().
    [System.NonSerialized] public bool isGroundedPublic;
    [System.NonSerialized] public bool isHomingPublic;

    // Normal Mode value snapshots — captured in Awake() so toggling back always restores them.
    private float _baseTopSpeed;
    private float _baseAcceleration;
    private float _baseBrakeFriction;
    private float _baseRollingFriction;
    private float _baseGroundStickyForce;
    private float _baseSurfaceAlignSpeed;

    // Camera component — cached for FOV lerping.
    private Camera _camera;

    #endregion
    // ─────────────────────────────────────────────────────────────────────────
    #region Unity Lifecycle

    private void Awake()
    {
        rb             = GetComponent<Rigidbody>();
        rb.useGravity  = false;  // We apply gravity ourselves so we can redirect it per-surface
        rb.linearDamping        = 0f;     // SA2: no drag — momentum is sacred
        rb.angularDamping = 0f;
        rb.constraints = RigidbodyConstraints.FreezeRotation; // We rotate transform manually

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        // Cache the Camera component for FOV lerping.
        _camera = cameraTransform != null ? cameraTransform.GetComponent<Camera>() : Camera.main;
        if (_camera != null) _camera.fieldOfView = normalFOV;

        // Snapshot Normal Mode values so toggling back always restores the original inspector values.
        _baseTopSpeed          = topSpeed;
        _baseAcceleration      = acceleration;
        _baseBrakeFriction     = brakeFriction;
        _baseRollingFriction   = rollingFriction;
        _baseGroundStickyForce = groundStickyForce;
        _baseSurfaceAlignSpeed = surfaceAlignSpeed;
    }

    private void FixedUpdate()
    {
        // The homing attack coroutine drives its own movement; pause everything else.
        // Set the public flag before returning so PlayerAnimator sees it this frame.
        if (state == PlayerState.HomingAttack)
        {
            isHomingPublic = true;
            return;
        }

        TickTimers();
        DetectGround();
        UpdateState();
        AlignToSurface();
        ApplyGravity();

        if (state == PlayerState.Grounded)
            GroundedMovement();
        else
            AirborneMovement();

        ClampSpeed();
    }

    private void Update()
    {
        // Capture input every rendered frame so no press is ever missed between physics steps.
        GatherInput();
    }

    private void LateUpdate()
    {
        // Visual lean runs here so it interpolates every rendered frame, not just physics steps.
        UpdateVisualLean();
        UpdateFOV();
    }

    private void TickTimers()
    {
        jumpBufferTimer       = Mathf.Max(0f, jumpBufferTimer       - Time.fixedDeltaTime);
        coyoteTimer           = Mathf.Max(0f, coyoteTimer           - Time.fixedDeltaTime);
        jumpGroundIgnoreTimer = Mathf.Max(0f, jumpGroundIgnoreTimer - Time.fixedDeltaTime);
    }

    #endregion
    // ─────────────────────────────────────────────────────────────────────────
    #region Input  —  Direct device polling (gamepad + keyboard, no PlayerInput needed)
    //
    //  GatherInput() runs every Update so input is captured each rendered frame.
    //  Values are stored in fields and consumed by FixedUpdate — the same pattern
    //  used by ThirdPersonCamera for the right stick, which works on all devices.
    //
    //  WHY direct polling instead of PlayerInput SendMessages?
    //  PlayerInput auto-switches from Keyboard&Mouse → Gamepad scheme only when a
    //  *button* is pressed, not when an analog stick moves. Pushing the left stick
    //  without pressing a button first means the scheme never switches and PlayerInput
    //  never delivers the move event. Direct polling reads the device regardless of
    //  active scheme, so the left stick works the moment you touch it.

    private void GatherInput()
    {
        Vector2 newMoveInput = Vector2.zero;
        bool    newJumpPressed = false;
        bool    newJumpHeld    = false;

        // ── Gamepad ───────────────────────────────────────────────────────────
        if (Gamepad.current != null)
        {
            Vector2 stick = Gamepad.current.leftStick.ReadValue();
            // Dead-zone: sqrMagnitude > 0.04 ≈ magnitude > 0.2
            if (stick.sqrMagnitude > 0.04f)
                newMoveInput = stick;

            if (Gamepad.current.buttonSouth.wasPressedThisFrame) newJumpPressed = true;
            if (Gamepad.current.buttonSouth.isPressed)           newJumpHeld    = true;
        }

        // ── Keyboard (only fills move if the gamepad hasn't already) ──────────
        if (Keyboard.current != null)
        {
            if (newMoveInput.sqrMagnitude < 0.01f) // Don't override gamepad input
            {
                float h = 0f, v = 0f;
                if (Keyboard.current.dKey.isPressed         || Keyboard.current.rightArrowKey.isPressed) h += 1f;
                if (Keyboard.current.aKey.isPressed         || Keyboard.current.leftArrowKey.isPressed)  h -= 1f;
                if (Keyboard.current.wKey.isPressed         || Keyboard.current.upArrowKey.isPressed)    v += 1f;
                if (Keyboard.current.sKey.isPressed         || Keyboard.current.downArrowKey.isPressed)  v -= 1f;
                newMoveInput = new Vector2(h, v).normalized;
            }

            if (Keyboard.current.spaceKey.wasPressedThisFrame) newJumpPressed = true;
            if (Keyboard.current.spaceKey.isPressed)           newJumpHeld    = true;
        }

        // ── Polarity toggle ───────────────────────────────────────────────────
        //  LB or RB (gamepad) / Left Shift or Right Shift (keyboard) — either toggles mode.
        {
            bool togglePressed = false;
            if (Gamepad.current != null &&
                (Gamepad.current.leftShoulder.wasPressedThisFrame ||
                 Gamepad.current.rightShoulder.wasPressedThisFrame))
                togglePressed = true;
            if (Keyboard.current != null &&
                (Keyboard.current.leftShiftKey.wasPressedThisFrame ||
                 Keyboard.current.rightShiftKey.wasPressedThisFrame))
                togglePressed = true;
            if (togglePressed)
            {
                isRocketMode = !isRocketMode;
                ApplyPolarityMode();
            }
        }

        // ── Commit to fields ──────────────────────────────────────────────────
        moveInput = newMoveInput;
        jumpHeld  = newJumpHeld;

        if (newJumpPressed)
        {
            jumpBufferTimer = jumpBufferTime; // Remember the press for coyote-time landing
            jumpHeld        = true;
        }
        if (!newJumpHeld) isJumping = false;  // Early release → cut variable jump height
    }

    #endregion
    // ─────────────────────────────────────────────────────────────────────────
    #region Ground Detection
    //
    //  We use Physics.SphereCast instead of a simple Raycast for two reasons:
    //    1. A sphere gives a stable normal read on curved surfaces and poly edges
    //       — essential for the interior of a loop where normals change rapidly.
    //    2. It avoids missing the ground when the player is slightly offset
    //       from a surface center (e.g., running along the edge of a platform).
    //
    //  WHY cast along -transform.up (local-space down)?
    //    When the player is aligned upside-down inside a loop, transform.up
    //    points toward the loop's center — i.e., "down" relative to the loop
    //    surface. Casting in -transform.up always points toward the player's feet
    //    regardless of how the whole transform is oriented in world space.
    //    This is the key to seamless loop and wall traversal.

    private void DetectGround()
    {
        // While this timer is active (brief period after a jump), skip the check
        // so the player doesn't immediately snap back to grounded state.
        if (jumpGroundIgnoreTimer > 0f)
        {
            isGrounded = false;
            return;
        }

        // Cast origin is raised by one radius + a small bias above the player's feet.
        //
        // WHY the bias?  Physics.SphereCast does not return hits when the starting
        // sphere is already touching or overlapping the surface (Unity limitation —
        // the hit distance would be 0 and is silently discarded).  When the player
        // is standing still, the capsule bottom is flush with the ground, so the
        // sphere at (transform.position + up * radius) would start with its bottom
        // face exactly on the surface → no hit → isGrounded always false → no jump.
        //
        // Adding 0.1 m to the origin and extending the cast distance by the same
        // amount ensures the sphere starts clear of the surface even when standing,
        // so the hit is returned at a non-zero distance every time.
        const float originBias = 0.1f;
        Vector3 castOrigin = transform.position + transform.up * (groundCheckRadius + originBias);

        RaycastHit hit;
        isGrounded = Physics.SphereCast(
            castOrigin,
            groundCheckRadius,
            -transform.up,            // Cast toward local-space feet
            out hit,
            groundCheckDistance + originBias,
            groundLayers,
            QueryTriggerInteraction.Ignore);

        if (isGrounded)
        {
            // ── Surface Normal ────────────────────────────────────────────────
            // hit.normal is the outward face normal of the polygon struck.
            //   • Flat floor        →  (0,  1, 0) world
            //   • Loop at apex      →  (0, -1, 0) world  (inward = our new "up")
            //   • 45° ramp          →  (±0.707, 0.707, 0) world
            //
            // We store this and use it for:
            //   a) Redirecting gravity into the surface   (ApplyGravity)
            //   b) Aligning transform.up to match it      (AlignToSurface)
            //   c) Projecting camera and input vectors     (GroundedMovement)
            // ──────────────────────────────────────────────────────────────────
            groundNormal = hit.normal;
            coyoteTimer  = coyoteTime; // Refresh coyote window every grounded frame
        }
        else
        {
            // Smoothly return the stored normal toward world-up while airborne.
            // This prevents a jarring realignment snap on landing.
            groundNormal = Vector3.Lerp(groundNormal, Vector3.up, 4f * Time.fixedDeltaTime);
        }
    }

    #endregion
    // ─────────────────────────────────────────────────────────────────────────
    #region State Machine

    private void UpdateState()
    {
        if (isGrounded)
        {
            state           = PlayerState.Grounded;
            homingAvailable = true;   // Homing refreshes every time we touch ground (SA2 behaviour)
            isJumping       = false;
        }
        else if (state != PlayerState.HomingAttack)
        {
            state = PlayerState.Airborne;
        }

        isGroundedPublic = isGrounded;
        isHomingPublic   = state == PlayerState.HomingAttack;
    }

    #endregion
    // ─────────────────────────────────────────────────────────────────────────
    #region Surface Alignment
    //
    //  Goal: keep transform.up aligned with the ground normal so the player
    //        stands perpendicular to any surface — floors, walls, loop interiors.
    //
    //  Method (avoids the arbitrary spin of FromToRotation):
    //    1. Project transform.forward onto the target surface plane.
    //       This gives us a "forward" that lies flat on the new surface.
    //    2. Build a full rotation with LookRotation(newForward, targetUp).
    //    3. Slerp toward it at surfaceAlignSpeed.
    //
    //  The Lerp on groundNormal (in DetectGround) ensures that when the player
    //  leaves a loop and goes airborne, the target slowly drifts back toward
    //  world-up rather than snapping.

    private void AlignToSurface()
    {
        // groundNormal is already Lerped toward Vector3.up when airborne (see DetectGround).
        Vector3 targetUp = groundNormal;

        Vector3 projectedForward = Vector3.ProjectOnPlane(transform.forward, targetUp);

        // Edge case: if forward is almost parallel to the normal (e.g., running into a wall),
        // fall back to the right vector to avoid a degenerate LookRotation.
        if (projectedForward.sqrMagnitude < 0.001f)
            projectedForward = Vector3.ProjectOnPlane(transform.right, targetUp);

        projectedForward.Normalize();

        Quaternion targetRotation = Quaternion.LookRotation(projectedForward, targetUp);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            surfaceAlignSpeed * Time.fixedDeltaTime);
    }

    #endregion
    // ─────────────────────────────────────────────────────────────────────────
    #region Gravity
    //
    //  Using ForceMode.Acceleration makes gravity mass-independent (all objects
    //  fall at the same rate regardless of their Rigidbody mass).
    //
    //  Grounded: gravity is directed INTO the surface (-groundNormal).
    //    The sticky force is critical for loops — without it, centrifugal force
    //    at the apex would exceed gravity and the player would fly off.
    //
    //  Airborne: standard world-down gravity for predictable arcs.

    private void ApplyGravity()
    {
        if (isGrounded)
        {
            // Normal force + extra sticky force = player stays on any curved surface
            rb.AddForce(-groundNormal * (gravityForce + groundStickyForce), ForceMode.Acceleration);
        }
        else
        {
            rb.AddForce(Vector3.down * gravityForce, ForceMode.Acceleration);
        }
    }

    #endregion
    // ─────────────────────────────────────────────────────────────────────────
    #region Grounded Movement

    private void GroundedMovement()
    {
        // ── 1. Camera-Relative Input Direction ────────────────────────────────
        //
        //  We project the camera's world-space axes onto the surface plane.
        //  This ensures that "up on the stick" always means "toward the camera's
        //  horizon" — even when the surface is tilted mid-loop.
        //
        //  ProjectOnPlane(v, normal) removes the component of v along 'normal',
        //  leaving only the part that lies on the plane. Normalizing gives us a
        //  unit vector we can safely use as a movement direction.
        Vector3 camForward = Vector3.ProjectOnPlane(cameraTransform.forward, groundNormal).normalized;
        Vector3 camRight   = Vector3.ProjectOnPlane(cameraTransform.right,   groundNormal).normalized;
        Vector3 inputDir   = (camForward * moveInput.y + camRight * moveInput.x).normalized;

        // ── 2. Slope-Based Acceleration ───────────────────────────────────────
        //
        //  Vector3.Angle(groundNormal, Vector3.up) gives the slope steepness:
        //    0°  = perfectly flat   →  no slope bonus
        //    90° = vertical wall    →  maximum slope effect (gravity-driven)
        //
        //  ProjectOnPlane(Vector3.down, groundNormal) gives the "downhill" direction
        //  — the steepest descent vector lying on the surface plane.
        //
        //  Dot(inputDir, downSlopeDir):
        //    +1 = heading directly downhill  → apply full slopeForce bonus
        //    -1 = heading directly uphill    → subtract force (gravity penalty)
        //     0 = traversing horizontally    → no slope effect
        float   slopeAngle    = Vector3.Angle(groundNormal, Vector3.up);
        Vector3 downSlopeDir  = Vector3.ProjectOnPlane(Vector3.down, groundNormal).normalized;
        float   slopeAlign    = Vector3.Dot(inputDir, downSlopeDir);           // -1 to 1
        float   slopeMulti    = 1f + slopeAlign * Mathf.InverseLerp(0f, 90f, slopeAngle);
        slopeMulti            = Mathf.Clamp(slopeMulti, 0.2f, 2.5f);

        // ── 3. Apply Acceleration ─────────────────────────────────────────────
        if (inputDir.sqrMagnitude > 0.01f)
        {
            rb.AddForce(inputDir * acceleration * slopeMulti, ForceMode.Acceleration);

            // Rotate to face the movement direction (not the input direction directly,
            // because inputDir is already surface-projected and camera-relative).
            Quaternion targetRot = Quaternion.LookRotation(inputDir, groundNormal);
            transform.rotation   = Quaternion.Slerp(transform.rotation, targetRot, turnSpeed * Time.fixedDeltaTime);
        }

        // ── 4. Skate Friction ─────────────────────────────────────────────────
        ApplySkateFriction(inputDir);

        // ── 5. Jump ───────────────────────────────────────────────────────────
        HandleGroundJump();
    }

    #endregion
    // ─────────────────────────────────────────────────────────────────────────
    #region Skate Friction (SA2 drift feel)
    //
    //  SA2's signature at high speed: wide, drifting turns. Friction only bites
    //  hard when braking (no input) or attempting a sharp reversal.
    //
    //  How it works:
    //    flatVel  — current velocity projected onto the surface plane.
    //    No input → apply minimal rolling friction (coasting slowdown).
    //    Input    → measure angle between current velocity and desired direction.
    //               Small angle (<brakeFrictionAngle) → near-zero friction.
    //               Large angle (>brakeFrictionAngle) → ramps up to brakeFriction.
    //               Quadratic ramp gives a gentle onset followed by a firm bite.

    private void ApplySkateFriction(Vector3 inputDir)
    {
        Vector3 flatVel = Vector3.ProjectOnPlane(rb.linearVelocity, groundNormal);
        if (flatVel.sqrMagnitude < 0.5f) return; // Skip when nearly stopped

        float friction;

        if (inputDir.sqrMagnitude < 0.01f)
        {
            // Coasting: light friction lets the player glide to a stop naturally
            friction = rollingFriction;
        }
        else
        {
            float angle = Vector3.Angle(flatVel, inputDir);
            float t     = Mathf.Clamp01(angle / brakeFrictionAngle);
            friction    = Mathf.Lerp(0f, brakeFriction, t * t); // Quadratic: gentle→firm
        }

        rb.AddForce(-flatVel.normalized * friction, ForceMode.Acceleration);
    }

    #endregion
    // ─────────────────────────────────────────────────────────────────────────
    #region Jump

    private void HandleGroundJump()
    {
        bool canJump = isGrounded || coyoteTimer > 0f;
        if (jumpBufferTimer <= 0f || !canJump) return;

        // Consume the buffer so this jump can't fire again
        jumpBufferTimer       = 0f;
        coyoteTimer           = 0f;
        jumpGroundIgnoreTimer = 0.2f; // Don't re-detect ground for 2 physics frames

        // ── SA2 Momentum Conservation ──────────────────────────────────────────
        //
        //  We decompose the current velocity into:
        //    flatVel  — the component along the surface plane (KEEP 100%)
        //    vertVel  — the component along the surface normal (REPLACE with jumpForce)
        //
        //  Crucially, we assign rb.velocity directly rather than using AddForce.
        //  AddForce would stack on top of any existing vertical velocity.
        //  Direct assignment gives us exact, predictable launch speed every time.
        //
        //  Result: jumping at 30 m/s means flying at 30 m/s. The vertical component
        //  is replaced cleanly. No speed loss. This is the core of SA2 feel.
        Vector3 flatVel = Vector3.ProjectOnPlane(rb.linearVelocity, groundNormal);
        rb.linearVelocity     = flatVel + groundNormal * jumpForce;

        isJumping     = true;
        jumpHoldTimer = 0f;
    }

    private void HandleAirJump()
    {
        // Variable jump height: apply sustained force while jump is held
        if (isJumping && jumpHeld && jumpHoldTimer < jumpHoldTime)
        {
            rb.AddForce(transform.up * jumpHoldForce, ForceMode.Acceleration);
            jumpHoldTimer += Time.fixedDeltaTime;
        }
        else if (!jumpHeld || jumpHoldTimer >= jumpHoldTime)
        {
            isJumping = false;
        }
    }

    #endregion
    // ─────────────────────────────────────────────────────────────────────────
    #region Airborne Movement

    private void AirborneMovement()
    {
        HandleAirJump();

        // Homing attack: jump pressed while airborne + homing is available
        if (jumpBufferTimer > 0f && homingAvailable)
        {
            jumpBufferTimer = 0f;
            TryHomingAttack();
            return;
        }

        // Air strafe — camera-relative, projected onto world-up plane
        // (We don't know what surface we'll land on, so world-up is the right reference.)
        if (moveInput.sqrMagnitude < 0.01f) return;

        Vector3 camForward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
        Vector3 camRight   = Vector3.ProjectOnPlane(cameraTransform.right,   Vector3.up).normalized;
        Vector3 inputDir   = (camForward * moveInput.y + camRight * moveInput.x).normalized;

        // Only apply strafe if we haven't hit the air strafe cap
        Vector3 horizontalVel = Vector3.ProjectOnPlane(rb.linearVelocity, Vector3.up);
        if (horizontalVel.magnitude < maxAirStrafeSpeed)
            rb.AddForce(inputDir * acceleration * airControlFactor, ForceMode.Acceleration);
    }

    #endregion
    // ─────────────────────────────────────────────────────────────────────────
    #region Homing Attack
    //
    //  SA2 Homing Attack flow:
    //    1. Build an aim direction from the camera-relative stick input.
    //       Fallback chain: stick → velocity direction → camera forward.
    //    2. Score every "Target" in range by alignment with that aim direction
    //       and distance, then lock on to the highest-scoring candidate.
    //    3. Zero velocity, orient toward target, drive with MovePosition each frame.
    //    4. On proximity:
    //         a. Freeze-frame (Time.timeScale = 0, WaitForSecondsRealtime) — the crunch.
    //         b. Restore time scale.
    //         c. Broadcast OnHomingHit to the target (it handles VFX, destruction, score).
    //         d. Apply upward bounce impulse so the player can chain attacks.
    //         e. Re-grant homingAvailable so chaining works immediately.
    //
    //  SCORING FORMULA:
    //    score = alignment × (1 − dist/homingRange × 0.5)
    //
    //    alignment = dot(dirToTarget, aimDir) ∈ [−1, 1]
    //      +1 = target is dead-ahead on the stick → highest priority
    //       0 = target is 90° to the side         → neutral
    //      −1 = target is directly behind          → lowest priority
    //
    //    The 0.5-weighted distance factor means a well-aligned far target still
    //    beats a nearby one that is off-axis, so stick intent always wins.

    private void TryHomingAttack()
    {
        // ── Build aim direction ────────────────────────────────────────────────
        //
        //  We project onto the horizontal plane so "stick right" always maps to
        //  world-right relative to the camera, regardless of camera pitch.
        Vector3 aimDir = Vector3.zero;

        if (cameraTransform != null && moveInput.sqrMagnitude > 0.1f)
        {
            Vector3 camFwd   = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
            Vector3 camRight = Vector3.ProjectOnPlane(cameraTransform.right,   Vector3.up).normalized;
            aimDir = (camFwd * moveInput.y + camRight * moveInput.x).normalized;
        }

        if (aimDir.sqrMagnitude < 0.01f)
        {
            // No stick input — aim along current velocity, or camera forward if near-still.
            Vector3 horizVel = Vector3.ProjectOnPlane(rb.linearVelocity, Vector3.up);
            aimDir = horizVel.sqrMagnitude > 1f
                ? horizVel.normalized
                : (cameraTransform != null
                    ? Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized
                    : transform.forward);
        }

        // ── Score candidates ───────────────────────────────────────────────────
        Collider[] cols    = Physics.OverlapSphere(transform.position, homingRange);
        Transform  best    = null;
        float      bestScore = float.NegativeInfinity;

        foreach (Collider col in cols)
        {
            if (!col.CompareTag("Target")) continue;

            Vector3 toTarget  = col.transform.position - transform.position;
            float   dist      = toTarget.magnitude;
            if (dist < 0.01f) continue;

            float alignment = Vector3.Dot(toTarget.normalized, aimDir);
            float score     = alignment * (1f - (dist / homingRange) * 0.5f);

            if (score > bestScore)
            {
                bestScore = score;
                best      = col.transform;
            }
        }

        homingAvailable = false;

        if (best == null)
        {
            StartCoroutine(TargetlessHomingRoutine(aimDir));
            return;
        }

        StartCoroutine(HomingAttackRoutine(best));
    }

    private IEnumerator HomingAttackRoutine(Transform target)
    {
        state       = PlayerState.HomingAttack;
        rb.linearVelocity = Vector3.zero;

        const float hitRadius = 1.2f;
        const float timeout   = 1.5f;
        float       elapsed   = 0f;

        while (elapsed < timeout)
        {
            // Target destroyed mid-flight (already hit by something else, etc.)
            if (target == null) break;

            Vector3 toTarget  = target.position - rb.position;
            Vector3 direction = toTarget.normalized;

            // Face the target and fly toward it
            if (direction.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(direction, transform.up);

            rb.MovePosition(rb.position + direction * homingSpeed * Time.fixedDeltaTime);

            if (toTarget.sqrMagnitude <= hitRadius * hitRadius)
            {
                // ── Freeze Frame ────────────────────────────────────────────────
                //  Setting Time.timeScale = 0 stops all physics and animation.
                //  WaitForSecondsRealtime ignores timeScale, so we actually wait
                //  the full 0.05 real seconds before resuming. This is the "crunch"
                //  that gives SA2's homing attack its satisfying weight.
                Time.timeScale = 0f;
                yield return new WaitForSecondsRealtime(homingFreezeFrameDuration);
                Time.timeScale = 1f;
                // ────────────────────────────────────────────────────────────────

                // Notify the target — it handles its own VFX, score, and destruction.
                // We use DontRequireReceiver so targets without the handler don't throw.
                target.SendMessage("OnHomingHit", SendMessageOptions.DontRequireReceiver);

                // Bounce up — gives the player air to chain the next homing attack
                rb.linearVelocity     = Vector3.up * homingBounceForce;
                homingAvailable = true; // Immediately allow chaining

                state = PlayerState.Airborne;
                yield break;
            }

            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        // Timeout or target gone — resume airborne with a small forward push
        rb.linearVelocity = transform.forward * (homingSpeed * 0.4f);
        state       = PlayerState.Airborne;
    }

    private IEnumerator TargetlessHomingRoutine(Vector3 dashDir)
    {
        state             = PlayerState.HomingAttack;
        rb.linearVelocity = dashDir * homingSpeed;

        if (dashDir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(dashDir, transform.up);

        yield return new WaitForSeconds(homingDashDuration);

        // Resume airborne — gravity and air control take over naturally
        state = PlayerState.Airborne;
    }

    #endregion
    // ─────────────────────────────────────────────────────────────────────────
    #region Speed Clamping
    //
    //  We clamp only the surface-planar (horizontal) component of velocity.
    //  Vertical velocity (jump arcs, falling) is never capped.
    //
    //  While grounded on steep downhill slopes, SA2 lets the player exceed topSpeed
    //  slightly (gravity does the work). We allow up to 25% overspeed to honour this.

    private void ClampSpeed()
    {
        if (state == PlayerState.HomingAttack) return;

        Vector3 up       = isGrounded ? groundNormal : Vector3.up;
        Vector3 flatVel  = Vector3.ProjectOnPlane(rb.linearVelocity, up);
        float   vertComp = Vector3.Dot(rb.linearVelocity, up);

        currentSpeed = flatVel.magnitude;

        // A LoopBoostTrigger sets overrideSpeedCap to let the player exceed topSpeed
        // through a loop.  Gravity decelerates them naturally as they climb.
        // Once they've slowed back to topSpeed, normal capping resumes automatically.
        if (overrideSpeedCap)
        {
            overrideSpeedCapTimer -= Time.fixedDeltaTime;
            if (currentSpeed <= topSpeed || overrideSpeedCapTimer <= 0f)
                overrideSpeedCap = false;
            return;
        }

        // Slight overspeed allowance on downhill slopes
        float cap = isGrounded
            ? topSpeed * Mathf.Lerp(1f, 1.25f, Mathf.Clamp01(currentSpeed / topSpeed))
            : topSpeed;

        if (currentSpeed > cap)
            rb.linearVelocity = flatVel.normalized * cap + up * vertComp;
    }

    #endregion
    // ─────────────────────────────────────────────────────────────────────────
    #region Visual Lean
    //
    //  We tilt the visual model's local Z axis based on horizontal input magnitude.
    //  The physics body (this transform) is never touched here — only the child mesh.
    //  Running in LateUpdate means it runs after all physics, giving smooth results
    //  at any combination of physics and render framerates.

    private void UpdateVisualLean()
    {
        if (visualModel == null) return;

        float   effectiveLean = isRocketMode ? maxLeanAngle * 2f : maxLeanAngle;
        float   targetLean = -moveInput.x * effectiveLean; // Negative: lean into the turn
        Vector3 euler      = visualModel.localEulerAngles;

        // Remap Z from [0, 360] to [-180, 180] so Lerp interpolates the short way
        float currentZ = euler.z > 180f ? euler.z - 360f : euler.z;
        float newZ     = Mathf.Lerp(currentZ, targetLean, leanSpeed * Time.deltaTime);

        visualModel.localEulerAngles = new Vector3(euler.x, euler.y, newZ);
    }

    #endregion
    // ─────────────────────────────────────────────────────────────────────────
    #region Polarity System
    //
    //  ApplyPolarityMode() writes directly to the serialized public fields so all
    //  existing physics methods (GroundedMovement, ClampSpeed, etc.) pick up the
    //  new values without any changes.  The base values were snapshotted in Awake()
    //  so toggling back always restores the original Normal Mode inspector values.

    private void ApplyPolarityMode()
    {
        if (isRocketMode)
        {
            topSpeed          = rocketTopSpeed;
            acceleration      = rocketAcceleration;
            brakeFriction     = rocketBrakeFriction;
            rollingFriction   = rocketRollingFriction;
            groundStickyForce = rocketGroundStickyForce;
            surfaceAlignSpeed = rocketSurfaceAlignSpeed;
        }
        else
        {
            topSpeed          = _baseTopSpeed;
            acceleration      = _baseAcceleration;
            brakeFriction     = _baseBrakeFriction;
            rollingFriction   = _baseRollingFriction;
            groundStickyForce = _baseGroundStickyForce;
            surfaceAlignSpeed = _baseSurfaceAlignSpeed;
        }
    }

    private void UpdateFOV()
    {
        if (_camera == null) return;
        float targetFOV = isRocketMode ? rocketFOV : normalFOV;
        _camera.fieldOfView = Mathf.Lerp(_camera.fieldOfView, targetFOV, fovLerpSpeed * Time.deltaTime);
    }

    #endregion
    // ─────────────────────────────────────────────────────────────────────────
    #region Debug Gizmos

    private void OnDrawGizmosSelected()
    {
        // Ground check sphere position
        Vector3 spherePos = transform.position - transform.up * (groundCheckDistance - groundCheckRadius);
        Gizmos.color = Application.isPlaying ? (isGrounded ? Color.green : Color.red) : Color.yellow;
        Gizmos.DrawWireSphere(spherePos, groundCheckRadius);

        // Surface normal arrow
        if (Application.isPlaying && isGrounded)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(transform.position, groundNormal * 2f);
        }

        // Homing range indicator
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.12f);
        Gizmos.DrawWireSphere(transform.position, homingRange);

        // Velocity vector (scaled for readability)
        if (Application.isPlaying && rb != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(transform.position, rb.linearVelocity * 0.15f);
        }
    }

    #endregion
}
