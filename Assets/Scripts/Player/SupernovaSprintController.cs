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

    [Tooltip("Minimum dot product between the hit surface normal and transform.up for it to count as ground.\n" +
             "Rejects undersides of flat tracks (dot ≈ −1) while still accepting loop interiors (dot ≈ +1).\n" +
             "0.1 = only surfaces roughly facing the player's feet. Lower values allow steeper overhangs.")]
    [Range(-1f, 1f)]
    public float minGroundNormalAlignment = 0.1f;

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
    [Tooltip("Base air control force as a fraction of acceleration. Higher = more responsive in air.")]
    [Range(0f, 1f)]
    public float airControlFactor = 0.5f;

    [Tooltip("Horizontal speed at which air control reaches zero. Control tapers smoothly toward this value.")]
    public float maxAirStrafeSpeed = 20f;

    [Tooltip("Curve shape of the speed-based air control falloff.\n" +
             "1 = linear drop  |  2 = holds well at mid-speed then tapers sharply at high speed (recommended)\n" +
             "Higher values = more control at low speed, heavier feeling at high speed.")]
    [Range(0.5f, 4f)]
    public float airControlFalloffExponent = 2f;

    [Header("── Force Boost ──────────────────────────────────────────────")]
    [Tooltip("Horizontal dash speed of the Force Boost (similar to homingSpeed).")]
    public float forceBoostSpeed = 21f;

    [Tooltip("Upward velocity added alongside the horizontal dash for a slight lift.")]
    public float forceBoostUpForce = 8f;

    [Header("── Gravity Slam ─────────────────────────────────────────────")]
    [Tooltip("Initial downward speed (m/s) at the start of the slam.")]
    public float slamSpeed = 40f;

    [Tooltip("How quickly the downward slam speed increases per second (m/s²). Keep small for a subtle effect.")]
    public float slamAcceleration = 5f;

    [Tooltip("Upward bounce force on slam impact (m/s). √(2 × gravity × height) = velocity.\n" +
             "3.5 players (8.33 m) → 20.41  |  4.5 players (10.71 m) → 23.14")]
    public float slamBounceForce = 20.41f;

    [Tooltip("How strongly the player can steer horizontal direction mid-slam (m/s² of influence).")]
    public float slamAirControl = 20f;

    [Tooltip("Time-scale freeze duration on slam impact — the 'crunch' (seconds).")]
    public float slamFreezeFrameDuration = 0.05f;

    [Tooltip("Ground proximity distance (m) at which slam impact triggers.")]
    public float slamGroundBuffer = 0.2f;

    [Tooltip("How long after impact before the player can slam again (seconds). " +
             "Match this to GravitySlamVFX.impactDuration so the cooldown ends when the rings finish animating.")]
    public float slamCooldown = 0.125f;

    [Header("── Nova Surge ───────────────────────────────────────────────")]
    [Tooltip("Flat top speed bonus added on top of Normal or Rocket Mode speed (m/s).")]
    public float surgeSpeedBonus = 7.4f;

    [Tooltip("How much turn speed is reduced while Nova Surge is active.")]
    public float surgeTurnSpeedReduction = 9f;

    [Tooltip("How much brakeFriction is reduced while Nova Surge is active (less grip, more slide).")]
    public float surgeBrakeFrictionReduction = 0.9f;

    [Tooltip("How much brakeFrictionAngle is raised while Nova Surge is active (wider turning arc).")]
    public float surgeBrakeFrictionAngleBonus = 45f;

    [Tooltip("How much rollingFriction is reduced while Nova Surge is active.\n" +
             "rollingFriction ÷ (rollingFriction − reduction) scales the coast-to-stop time.\n" +
             "e.g. rocketRollingFriction 25 − 6 = 19 → 57.4 ÷ 19 ≈ 3 s to stop.")]
    public float surgeRollingFrictionReduction = 6f;

    [Tooltip("Multiplier applied to maxLeanAngle while Nova Surge is active (e.g. 0.5 = half lean).")]
    [Range(0f, 1f)]
    public float surgeLeanMultiplier = 0.5f;

    [Tooltip("Instant velocity boost (m/s) added along the current travel direction on Nova Surge activation. Ignored if the player is stationary.")]
    public float surgeActivationBoost = 8.75f;

    [Tooltip("Acceleration bonus added while Nova Surge is active.")]
    public float surgeAccelerationBonus = 10f;

    [Tooltip("How long the speed boost lasts (seconds).")]
    public float surgeDuration = 6f;

    [Tooltip("Cooldown before Nova Surge can be used again (seconds).")]
    public float surgeCooldown = 30f;

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
    private bool    _wasGrounded;
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
    private bool      homingAvailable; // Refreshed each time the player lands
    private Coroutine _activeHomingCoroutine;

    // Force Boost
    private bool forceBoostPressed;
    private bool forceBoostAvailable;

    // Nova Surge
    [System.NonSerialized] public bool surgeRechargeQueued; // Set when a recharge target is hit mid-surge

    // Gravity Slam
    private bool      _gravSlamPressed;
    private Coroutine _slamCoroutine;
    private bool      _slamHasHorizontal; // True if stick was pushed at activation
    private Vector3   _slamHorizVel;      // Horizontal velocity locked in at activation
    private float     _slamCooldownTimer;

    // Nova Surge
    private bool      _novaSurgeInputPressed;
    [System.NonSerialized] public bool canSurge = true;
    private Coroutine _novaSurgeCoroutine;

    // State
    private enum PlayerState { Grounded, Airborne, HomingAttack, Grinding }
    private PlayerState state = PlayerState.Airborne;

    // Public read-only diagnostics (useful for a HUD speed counter)
    [System.NonSerialized] public float currentSpeed;

    // Audio events — subscribe in PlayerAudio
    [System.NonSerialized] public System.Action        OnJump;
    [System.NonSerialized] public System.Action        OnLand;
    [System.NonSerialized] public System.Action        OnHomingAttack;
    [System.NonSerialized] public System.Action        OnHomingHit;
    [System.NonSerialized] public System.Action        OnForceBoost;
    [System.NonSerialized] public System.Action<bool>  OnRocketToggle; // true = rocket on
    [System.NonSerialized] public System.Action        OnGravitySlam;          // slam activated
    [System.NonSerialized] public System.Action        OnGravitySlamImpact;    // slam hit ground
    [System.NonSerialized] public System.Action        OnGravitySlamCancelled; // slam cancelled mid-air
    [System.NonSerialized] public System.Action        OnNovaSurge;
    [System.NonSerialized] public System.Action        OnSurgeRecharged;
    [System.NonSerialized] public System.Action        OnGrindStart;
    [System.NonSerialized] public System.Action        OnGrindEnd;

    // Nova Surge readable state — use for screen shake, motion blur, HUD, etc.
    [System.NonSerialized] public bool  isNovaSurging;
    [System.NonSerialized] public float surgeCooldownRemaining;

    // Set true by LoopBoostTrigger to allow temporary overspeed through a loop.
    // Self-clears in ClampSpeed() once gravity slows the player to topSpeed,
    // or after the safety timeout (whichever comes first).
    [System.NonSerialized] public bool  overrideSpeedCap;
    [System.NonSerialized] public float overrideSpeedCapTimer;

    // True while Rocket Mode (Polarity toggle) is active — readable by other scripts (e.g. HUD).
    [System.NonSerialized] public bool isRocketMode;

    // True while Gravity Slam is in progress — readable by VFX, HUD, etc.
    [System.NonSerialized] public bool isSlamming;

    // True while the player is grinding a rail — readable by VFX, HUD, animator, etc.
    [System.NonSerialized] public bool isGrindingPublic;

    // Animator-readable state flags — written each FixedUpdate by UpdateState().
    [System.NonSerialized] public bool isGroundedPublic;
    [System.NonSerialized] public bool isHomingPublic;

    // Rail grinding
    private RailTrack _activeRail;
    private float     _railDistance;
    private float     _grindDirectionSign; // +1 = toward last waypoint, −1 = toward first

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
        rb.useGravity       = false;  // We apply gravity ourselves so we can redirect it per-surface
        forceBoostAvailable = true;
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

    private void OnDisable()
    {
        // Clear slam state so a mid-slam death doesn't trigger a bounce on respawn.
        if (_slamCoroutine != null)
        {
            StopCoroutine(_slamCoroutine);
            _slamCoroutine = null;
        }
        isSlamming = false;

        // Clear grind state so a mid-grind death doesn't lock the player to the rail.
        _activeRail      = null;
        isGrindingPublic = false;
        if (state == PlayerState.Grinding)
            state = PlayerState.Airborne;
    }

    private void FixedUpdate()
    {
        // Runs first — may set isSlamming / cancel homing before the block below.
        ExecuteGravitySlam();
        ExecuteNovaSurge();

        // The homing attack coroutine drives its own movement; pause everything else.
        // Set the public flag before returning so PlayerAnimator sees it this frame.
        if (state == PlayerState.HomingAttack)
        {
            isHomingPublic = true;
            HandleForceBoost(); // Allow cancelling mid-attack
            return;
        }

        // Rail grinding drives its own movement — skip all normal physics while active.
        if (state == PlayerState.Grinding)
        {
            GrindingMovement();
            return;
        }

        TickTimers();
        DetectGround();
        UpdateState();
        AlignToSurface();

        // While slamming, the GravitySlamRoutine controls velocity directly each frame.
        // Skipping gravity and movement prevents them from fighting the locked slam speed.
        if (!isSlamming)
        {
            ApplyGravity();
            if (state == PlayerState.Grounded)
                GroundedMovement();
            else
                AirborneMovement();
        }

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
        _slamCooldownTimer    = Mathf.Max(0f, _slamCooldownTimer    - Time.fixedDeltaTime);
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
        if (Time.timeScale < 0.01f) return; // Don't read input while paused or time-frozen

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

        // ── Force Boost (X / West / Q) ────────────────────────────────────────
        if (Gamepad.current != null && Gamepad.current.buttonWest.wasPressedThisFrame)
            forceBoostPressed = true;
        if (Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame)
            forceBoostPressed = true;

        // ── Gravity Slam (B / East / E) ───────────────────────────────────────
        if (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame)
            _gravSlamPressed = true;
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            _gravSlamPressed = true;

        // ── Nova Surge (Y / North / Tab) ──────────────────────────────────────
        if (Gamepad.current  != null && Gamepad.current.buttonNorth.wasPressedThisFrame)
            _novaSurgeInputPressed = true;
        if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
            _novaSurgeInputPressed = true;

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
                OnRocketToggle?.Invoke(isRocketMode);
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

        // Reject surfaces whose normal doesn't sufficiently face the player's feet.
        // dot(hit.normal, transform.up) ≈ +1 = floor/loop interior (accept)
        //                               ≈  0 = side wall (reject)
        //                               ≈ −1 = underside (reject)
        if (isGrounded && Vector3.Dot(hit.normal, transform.up) < minGroundNormalAlignment)
            isGrounded = false;

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
            homingAvailable    = true; // Homing refreshes every time we touch ground (SA2 behaviour)
            forceBoostAvailable = true;
            isJumping       = false;
        }
        else if (state != PlayerState.HomingAttack)
        {
            state = PlayerState.Airborne;
        }

        if (isGrounded && !_wasGrounded) OnLand?.Invoke();
        _wasGrounded     = isGrounded;
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
        // Discard any boost input that was buffered while grounded
        forceBoostPressed = false;

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
        OnJump?.Invoke();
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
        HandleForceBoost();

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

        // Rotate to face input direction in the air
        Quaternion targetRot = Quaternion.LookRotation(inputDir, Vector3.up);
        transform.rotation   = Quaternion.Slerp(transform.rotation, targetRot, turnSpeed * Time.fixedDeltaTime);

        // Air control tapers with speed — faster = less ability to drift.
        // t = 0 (still) → full airControlFactor.  t = 1 (maxAirStrafeSpeed) → zero control.
        // 1 − t^exponent: holds strong at low/mid speed, drops off sharply near the cap.
        Vector3 horizontalVel    = Vector3.ProjectOnPlane(rb.linearVelocity, Vector3.up);
        float   t                = Mathf.Clamp01(horizontalVel.magnitude / maxAirStrafeSpeed);
        float   controlMult      = 1f - Mathf.Pow(t, airControlFalloffExponent);
        rb.AddForce(inputDir * acceleration * airControlFactor * controlMult, ForceMode.Acceleration);
    }

    private void HandleForceBoost()
    {
        if (!forceBoostPressed) return;
        forceBoostPressed = false;
        if (!forceBoostAvailable) return;
        forceBoostAvailable = false;

        // Cancel any active homing attack
        if (state == PlayerState.HomingAttack && _activeHomingCoroutine != null)
        {
            StopCoroutine(_activeHomingCoroutine);
            _activeHomingCoroutine = null;
            state = PlayerState.Airborne;
        }

        // Camera-relative direction from stick — same logic as homing attack aim.
        // Fall back to current velocity direction, then camera forward if near-still.
        Vector3 boostDir;
        if (cameraTransform != null && moveInput.sqrMagnitude > 0.1f)
        {
            Vector3 camFwd   = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
            Vector3 camRight = Vector3.ProjectOnPlane(cameraTransform.right,   Vector3.up).normalized;
            boostDir = (camFwd * moveInput.y + camRight * moveInput.x).normalized;
        }
        else
        {
            Vector3 horizVel = Vector3.ProjectOnPlane(rb.linearVelocity, Vector3.up);
            boostDir = horizVel.sqrMagnitude > 1f
                ? horizVel.normalized
                : (cameraTransform != null
                    ? Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized
                    : transform.forward);
        }

        // Dash in boost direction + small vertical lift, same pattern as homing attack
        rb.linearVelocity = boostDir * forceBoostSpeed + Vector3.up * forceBoostUpForce;

        OnForceBoost?.Invoke();
    }

    #endregion
    // ─────────────────────────────────────────────────────────────────────────
    #region Gravity Slam

    private void ExecuteGravitySlam()
    {
        if (!_gravSlamPressed) return;
        _gravSlamPressed = false;
        if (isSlamming) return;
        if (_slamCooldownTimer > 0f) return;
        if (state == PlayerState.Grounded) return;  // Airborne only
        if (state == PlayerState.Grinding) return;  // Not while on a rail

        // Interrupt homing attack
        if (state == PlayerState.HomingAttack && _activeHomingCoroutine != null)
        {
            StopCoroutine(_activeHomingCoroutine);
            _activeHomingCoroutine = null;
            isHomingPublic         = false;
            state                  = PlayerState.Airborne;
        }

        // Cancel jump hold so variable-height doesn't fight the slam
        isJumping = false;

        // Capture stick intent at the moment of press
        _slamHasHorizontal = moveInput.sqrMagnitude > 0.1f;
        _slamHorizVel      = _slamHasHorizontal
            ? Vector3.ProjectOnPlane(rb.linearVelocity, transform.up)
            : Vector3.zero;

        _slamCoroutine = StartCoroutine(GravitySlamRoutine());
        OnGravitySlam?.Invoke();
    }

    private IEnumerator GravitySlamRoutine()
    {
        isSlamming = true;
        float currentSlamSpeed = slamSpeed;

        while (true)
        {
            currentSlamSpeed += slamAcceleration * Time.fixedDeltaTime;
            // ── Cancel into Force Boost ────────────────────────────────────────
            if (forceBoostPressed && forceBoostAvailable)
            {
                isSlamming     = false;
                _slamCoroutine = null;
                OnGravitySlamCancelled?.Invoke();
                HandleForceBoost();
                yield break;
            }

            // ── Cancel into Homing Attack / Jump ──────────────────────────────
            if (jumpBufferTimer > 0f && homingAvailable)
            {
                isSlamming     = false;
                _slamCoroutine = null;
                OnGravitySlamCancelled?.Invoke();
                yield break; // jumpBufferTimer still live — HandleHomingAttack fires next frame
            }

            // ── Steer horizontal component with stick, then lock downward speed ──
            if (cameraTransform != null && moveInput.sqrMagnitude > 0.1f)
            {
                Vector3 camFwd   = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
                Vector3 camRight = Vector3.ProjectOnPlane(cameraTransform.right,   Vector3.up).normalized;
                Vector3 inputDir = (camFwd * moveInput.y + camRight * moveInput.x).normalized;
                _slamHorizVel += inputDir * slamAirControl * Time.fixedDeltaTime;
                if (_slamHorizVel.magnitude > topSpeed)
                    _slamHorizVel = _slamHorizVel.normalized * topSpeed;
            }
            rb.linearVelocity = _slamHorizVel - transform.up * currentSlamSpeed;

            // ── Ground proximity check ─────────────────────────────────────────
            //  Uses the same origin/radius as DetectGround so results are consistent.
            const float originBias = 0.1f;
            Vector3     castOrigin = transform.position + transform.up * (groundCheckRadius + originBias);
            bool nearGround = Physics.SphereCast(
                castOrigin, groundCheckRadius, -transform.up,
                out RaycastHit _, slamGroundBuffer + originBias,
                groundLayers, QueryTriggerInteraction.Ignore);

            if (nearGround || isGrounded)
            {
                // ── Impact ────────────────────────────────────────────────────
                Time.timeScale = 0f;
                yield return new WaitForSecondsRealtime(slamFreezeFrameDuration);
                Time.timeScale = 1f;

                float bounceForce = slamBounceForce;

                // Bounce continues the horizontal momentum locked in at activation.
                // If stick was neutral (straight down), bounce goes straight up.
                rb.linearVelocity = _slamHorizVel + transform.up * bounceForce;

                forceBoostAvailable = true;  // Player can dash out of the bounce
                homingAvailable     = true;
                isSlamming          = false;
                state               = PlayerState.Airborne;
                _slamCoroutine      = null;
                _slamCooldownTimer  = slamCooldown;

                OnGravitySlamImpact?.Invoke();
                yield break;
            }

            yield return new WaitForFixedUpdate();
        }
    }

    #endregion
    // ─────────────────────────────────────────────────────────────────────────
    #region Nova Surge

    private void ExecuteNovaSurge()
    {
        if (!_novaSurgeInputPressed) return;
        _novaSurgeInputPressed = false;
        if (!canSurge) return;
        if (!isRocketMode) return; // Nova Surge is only available in Rocket Mode

        _novaSurgeCoroutine = StartCoroutine(NovaSurgeRoutine());
    }

    // Called when Rocket Mode is turned off while Nova Surge is active.
    // Stops the coroutine cleanly, reverts the two stats ApplyPolarityMode doesn't own,
    // and starts a cooldown-only countdown (the rest resets via ApplyPolarityMode).
    private void CancelNovaSurge()
    {
        if (_novaSurgeCoroutine != null)
            StopCoroutine(_novaSurgeCoroutine);
        isNovaSurging      = false;
        turnSpeed         += surgeTurnSpeedReduction;
        brakeFrictionAngle -= surgeBrakeFrictionAngleBonus;
        rollingFriction   += surgeRollingFrictionReduction;
        // topSpeed, acceleration, brakeFriction are restored by the ApplyPolarityMode call that follows
        _novaSurgeCoroutine = StartCoroutine(NovaSurgeCooldownOnly());
    }

    private IEnumerator NovaSurgeCooldownOnly()
    {
        surgeCooldownRemaining = surgeCooldown;
        while (surgeCooldownRemaining > 0f)
        {
            surgeCooldownRemaining -= Time.deltaTime;
            yield return null;
        }
        surgeCooldownRemaining = 0f;
        canSurge            = true;
        _novaSurgeCoroutine = null;
    }

    // Called externally (e.g. by a homing target) to restore force boost availability.
    public void RechargeForceBoost()
    {
        forceBoostAvailable = true;
    }

    // Called externally (e.g. by a recharge homing target) to reset the cooldown.
    // If Nova Surge is currently active, queues the recharge so cooldown is skipped when it ends.
    public void RechargeSurge()
    {
        if (isNovaSurging)
        {
            surgeRechargeQueued = true;
            return;
        }
        if (_novaSurgeCoroutine != null)
            StopCoroutine(_novaSurgeCoroutine);
        _novaSurgeCoroutine    = null;
        surgeCooldownRemaining = 0f;
        canSurge               = true;
        OnSurgeRecharged?.Invoke();
    }

    private IEnumerator NovaSurgeRoutine()
    {
        // ── Activate ──────────────────────────────────────────────────────────
        canSurge      = false;
        isNovaSurging = true;
        topSpeed          += surgeSpeedBonus;
        acceleration      += surgeAccelerationBonus;
        turnSpeed         -= surgeTurnSpeedReduction;
        brakeFriction     -= surgeBrakeFrictionReduction;
        brakeFrictionAngle += surgeBrakeFrictionAngleBonus;
        rollingFriction   -= surgeRollingFrictionReduction;

        // Instant velocity kick along current travel direction (ignored if stationary)
        if (surgeActivationBoost > 0f && rb.linearVelocity.sqrMagnitude > 0.1f)
            rb.linearVelocity += rb.linearVelocity.normalized * surgeActivationBoost;

        OnNovaSurge?.Invoke();

        // ── Active window ─────────────────────────────────────────────────────
        //  Ticks every frame. Exits early if isNovaSurging is set to false externally.
        float elapsed = 0f;
        while (elapsed < surgeDuration && isNovaSurging)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        // ── Deactivate boost ──────────────────────────────────────────────────
        isNovaSurging = false;
        topSpeed           -= surgeSpeedBonus;
        acceleration       -= surgeAccelerationBonus;
        turnSpeed          += surgeTurnSpeedReduction;
        brakeFriction      += surgeBrakeFrictionReduction;
        brakeFrictionAngle -= surgeBrakeFrictionAngleBonus;
        rollingFriction    += surgeRollingFrictionReduction;

        // ── Cooldown (skipped if a recharge was queued mid-surge) ────────────
        if (surgeRechargeQueued)
        {
            surgeRechargeQueued = false;
        }
        else
        {
            surgeCooldownRemaining = surgeCooldown;
            while (surgeCooldownRemaining > 0f)
            {
                surgeCooldownRemaining -= Time.deltaTime;
                yield return null;
            }
        }

        surgeCooldownRemaining = 0f;
        canSurge               = true;
        OnSurgeRecharged?.Invoke();
        _novaSurgeCoroutine    = null;
    }

    #endregion
    // ─────────────────────────────────────────────────────────────────────────
    #region Rail Grinding

    // Called by RailTrack.OnTriggerEnter when the player contacts a rail while airborne/slamming.
    public void StartGrinding(RailTrack rail)
    {
        // Notify grind start FIRST so VFX listeners can suppress disc cleanup
        // before OnGravitySlamCancelled fires below.
        OnGrindStart?.Invoke();

        // Cancel any active states that would conflict
        if (isSlamming && _slamCoroutine != null)
        {
            StopCoroutine(_slamCoroutine);
            _slamCoroutine = null;
            isSlamming     = false;
            OnGravitySlamCancelled?.Invoke();
        }
        if (state == PlayerState.HomingAttack && _activeHomingCoroutine != null)
        {
            StopCoroutine(_activeHomingCoroutine);
            _activeHomingCoroutine = null;
        }
        isJumping = false;

        _activeRail   = rail;
        _railDistance = rail.GetClosestDistance(transform.position);

        // Travel toward the end waypoint by default; reverse if current velocity opposes that direction.
        var (_, railDir) = rail.GetPointAtDistance(_railDistance);
        _grindDirectionSign = Vector3.Dot(rb.linearVelocity, railDir) >= 0f ? 1f : -1f;

        state               = PlayerState.Grinding;
        isGrindingPublic    = true;
        homingAvailable     = true;
        forceBoostAvailable = true;
    }

    private void GrindingMovement()
    {
        if (_activeRail == null) { ExitGrinding(launchOff: false); return; }

        // Discard inputs that are disabled during a grind
        forceBoostPressed = false;
        _gravSlamPressed  = false;
        _slamCooldownTimer = Mathf.Max(0f, _slamCooldownTimer - Time.fixedDeltaTime);

        // ── Advance along rail ────────────────────────────────────────────────
        float grindSpeed = topSpeed * 1.7f;
        _railDistance += grindSpeed * _grindDirectionSign * Time.fixedDeltaTime;

        // ── End of rail — launch off in travel direction ──────────────────────
        if (_railDistance >= _activeRail.TotalLength || _railDistance <= 0f)
        {
            ExitGrinding(launchOff: true);
            return;
        }

        // ── Snap to rail position and lock velocity ───────────────────────────
        var (railPos, railDir) = _activeRail.GetPointAtDistance(_railDistance);
        Vector3 travelDir      = railDir * _grindDirectionSign;

        rb.MovePosition(railPos);
        rb.linearVelocity = travelDir * grindSpeed;
        currentSpeed = grindSpeed;

        // ── Face 90° left of travel direction (snowboard stance) ─────────────
        if (travelDir.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(travelDir, Vector3.up) * Quaternion.Euler(0f, -75f, 0f);
            transform.rotation   = Quaternion.Slerp(transform.rotation, targetRot, turnSpeed * Time.fixedDeltaTime);
        }

        isGrindingPublic = true;

        // ── Jump off rail ─────────────────────────────────────────────────────
        if (jumpBufferTimer > 0f)
        {
            jumpBufferTimer = 0f;
            ExitGrinding(launchOff: false);

            // Preserve full rail momentum into the jump — same pattern as ground jump
            rb.linearVelocity = travelDir * grindSpeed + Vector3.up * jumpForce;
            isJumping     = true;
            jumpHoldTimer = 0f;
            OnJump?.Invoke();
        }
    }

    private void ExitGrinding(bool launchOff)
    {
        if (launchOff && _activeRail != null)
        {
            // Fly off the end at full rail speed
            var (_, railDir) = _activeRail.GetPointAtDistance(_railDistance);
            rb.linearVelocity = railDir * _grindDirectionSign * (topSpeed * 1.7f);
        }

        _activeRail      = null;
        state            = PlayerState.Airborne;
        isGrindingPublic = false;
        OnGrindEnd?.Invoke();
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
        OnHomingAttack?.Invoke();

        if (best == null)
        {
            _activeHomingCoroutine = StartCoroutine(TargetlessHomingRoutine(aimDir));
            return;
        }

        _activeHomingCoroutine = StartCoroutine(HomingAttackRoutine(best));
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
                OnHomingHit?.Invoke();

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
        if (isNovaSurging) effectiveLean *= surgeLeanMultiplier;
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
            if (isNovaSurging) CancelNovaSurge(); // Surge is rocket-only; cancel on mode exit
            topSpeed          = _baseTopSpeed;
            acceleration      = _baseAcceleration;
            brakeFriction     = _baseBrakeFriction;
            rollingFriction   = _baseRollingFriction;
            groundStickyForce = _baseGroundStickyForce;
            surfaceAlignSpeed = _baseSurfaceAlignSpeed;
        }

        // Re-apply surge bonus on top of whichever mode is now active
        if (isNovaSurging) topSpeed += surgeSpeedBonus;
    }

    private void UpdateFOV()
    {
        if (_camera == null) return;
        float targetFOV = (isRocketMode ? rocketFOV : normalFOV) + (isNovaSurging ? 7.5f : 0f);
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
