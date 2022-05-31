using UnityEngine;
using System.Collections;
using UnityEngine.XR.WSA;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Animator))]
public class PlayerController : MonoBehaviour
{
    public float maxForwardSpeed = 8f;        // How fast Ellen can run.
    public float gravity = 20f;               // How fast Ellen accelerates downwards when airborne.
    public float minTurnSpeed = 400f;         // How fast Ellen turns when moving at maximum speed.
    public float maxTurnSpeed = 1200f;        // How fast Ellen turns when stationary.
    public float idleTimeout = 5f;            // How long before Ellen starts considering random idles.

    protected AnimatorStateInfo _currentStateInfo;    // Information about the base layer of the animator cached.
    protected AnimatorStateInfo _nextStateInfo;
    protected bool _isAnimatorTransitioning;
    protected AnimatorStateInfo _previousCurrentStateInfo;    // Information about the base layer of the animator from last frame.
    protected AnimatorStateInfo _previousNextStateInfo;
    protected bool _previousIsAnimatorTransitioning;
    protected float _desiredForwardSpeed;         // How fast Ellen aims be going along the ground based on input.
    protected float _forwardSpeed;                // How fast Ellen is currently going along the ground.
    protected float _verticalSpeed;               // How fast Ellen is currently moving up or down.
    protected PlayerInput _input;                 // Reference used to determine how Ellen should move.
    protected CharacterController _charCtrl;      // Reference used to actually move Ellen.
    protected Animator _animator;                 // Reference used to make decisions based on Ellen's current animation and to set parameters.
    protected Material _currentWalkingSurface;    // Reference used to make decisions about audio.
    protected Quaternion _targetRotation;         // What rotation Ellen is aiming to have based on input.
    protected float _angleDiff;                   // Angle in degrees between Ellen's current rotation and her target rotation.
    protected Collider[] _overlapResult = new Collider[8];    // Used to cache colliders that are near Ellen.
    protected Renderer[] _renderers;              // References used to make sure Renderers are reset properly.
    protected float _idleTimer;                   // Used to count up to Ellen considering a random idle.

    // These constants are used to ensure Ellen moves and behaves properly.
    // It is advised you don't change them without fully understanding what they do in code.
    const float k_GroundedRayDistance = 1f;
    const float k_InverseOneEighty = 1f / 180f;
    const float k_StickingGravityProportion = 0.3f;
    const float k_GroundAcceleration = 20f;
    const float k_GroundDeceleration = 25f;

    // Parameters
    readonly int _hashForwardSpeed = Animator.StringToHash("ForwardSpeed");
    readonly int _hashAngleDeltaRad = Animator.StringToHash("AngleDeltaRad");
    readonly int _hashTimeoutToIdle = Animator.StringToHash("TimeoutToIdle");
    readonly int _hashGrounded = Animator.StringToHash("Grounded");
    readonly int _hashStateTime = Animator.StringToHash("StateTime");
    readonly int _hashInputDetected = Animator.StringToHash("InputDetected");

    protected bool IsMoveInput
    {
        get { return !Mathf.Approximately(_input.MoveInput.sqrMagnitude, 0f); }
    }

    // Called automatically by Unity when the script first exists in the scene.
    void Awake()
    {
        _input = GetComponent<PlayerInput>();
        _animator = GetComponent<Animator>();
        _charCtrl = GetComponent<CharacterController>();
    }

    // Called automatically by Unity after Awake whenever the script is enabled.
    void OnEnable()
    {
        _renderers = GetComponentsInChildren<Renderer>();
    }

    // Called automatically by Unity whenever the script is disabled.
    void OnDisable()
    {
        for (int i = 0; i < _renderers.Length; ++i)
        {
            _renderers[i].enabled = true;
        }
    }

    // Called automatically by Unity once every Physics step.
    void FixedUpdate()
    {
        CacheAnimatorState();
        _animator.SetFloat(_hashStateTime, Mathf.Repeat(_animator.GetCurrentAnimatorStateInfo(0).normalizedTime, 1f));

        CalculateForwardMovement();

        SetTargetRotation();

        if (IsMoveInput)
            UpdateOrientation();

        TimeoutToIdle();
    }

    // Called at the start of FixedUpdate to record the current state of the base layer of the animator.
    void CacheAnimatorState()
    {
        _previousCurrentStateInfo = _currentStateInfo;
        _previousNextStateInfo = _nextStateInfo;
        _previousIsAnimatorTransitioning = _isAnimatorTransitioning;

        _currentStateInfo = _animator.GetCurrentAnimatorStateInfo(0);
        _nextStateInfo = _animator.GetNextAnimatorStateInfo(0);
        _isAnimatorTransitioning = _animator.IsInTransition(0);
    }

    // Called each physics step.
    void CalculateForwardMovement()
    {
        // Cache the move input and cap it's magnitude at 1.
        Vector2 moveInput = _input.MoveInput;
        if (moveInput.sqrMagnitude > 1f)
            moveInput.Normalize();

        // Calculate the speed intended by input.
        _desiredForwardSpeed = moveInput.magnitude * maxForwardSpeed;

        // Determine change to speed based on whether there is currently any move input.
        float acceleration = IsMoveInput ? k_GroundAcceleration : k_GroundDeceleration;

        // Adjust the forward speed towards the desired speed.
        _forwardSpeed = Mathf.MoveTowards(_forwardSpeed, _desiredForwardSpeed, acceleration * Time.deltaTime);

        // Set the animator parameter to control what animation is being played.
        _animator.SetFloat(_hashForwardSpeed, _forwardSpeed);
    }

    // Called each physics step to set the rotation Ellen is aiming to have.
    void SetTargetRotation()
    {
        // Create three variables, move input local to the player, flattened forward direction of the camera and a local target rotation.
        Vector2 moveInput = _input.MoveInput;
        Vector3 localMovementDirection = new Vector3(moveInput.x, 0f, moveInput.y).normalized;

        Vector3 forward = Quaternion.Euler(0f, transform.rotation.eulerAngles.x, 0f) * Vector3.forward;
        forward.y = 0f;
        forward.Normalize();

        Quaternion targetRotation;

        // If the local movement direction is the opposite of forward then the target rotation should be towards the camera.
        if (Mathf.Approximately(Vector3.Dot(localMovementDirection, Vector3.forward), -1.0f))
        {
            targetRotation = Quaternion.LookRotation(-forward);
        }
        else
        {
            // Otherwise the rotation should be the offset of the input from the camera's forward.
            Quaternion cameraToInputOffset = Quaternion.FromToRotation(Vector3.forward, localMovementDirection);
            targetRotation = Quaternion.LookRotation(cameraToInputOffset * forward);
        }

        // The desired forward direction of Ellen.
        Vector3 resultingForward = targetRotation * Vector3.forward;

        // Find the difference between the current rotation of the player and the desired rotation of the player in radians.
        float angleCurrent = Mathf.Atan2(transform.forward.x, transform.forward.z) * Mathf.Rad2Deg;
        float targetAngle = Mathf.Atan2(resultingForward.x, resultingForward.z) * Mathf.Rad2Deg;

        _angleDiff = Mathf.DeltaAngle(angleCurrent, targetAngle);
        _targetRotation = targetRotation;
    }

    // Called each physics step after SetTargetRotation if there is move input and Ellen is in the correct animator state according to IsOrientationUpdated.
    void UpdateOrientation()
    {
        _animator.SetFloat(_hashAngleDeltaRad, _angleDiff * Mathf.Deg2Rad);

        Vector3 localInput = new Vector3(_input.MoveInput.x, 0f, _input.MoveInput.y);
        float groundedTurnSpeed = Mathf.Lerp(maxTurnSpeed, minTurnSpeed, _forwardSpeed / _desiredForwardSpeed);
        float actualTurnSpeed = groundedTurnSpeed;
        _targetRotation = Quaternion.RotateTowards(transform.rotation, _targetRotation, actualTurnSpeed * Time.deltaTime);

        transform.rotation = _targetRotation;
    }

    // Called each physics step to count up to the point where Ellen considers a random idle.
    void TimeoutToIdle()
    {
        bool inputDetected = IsMoveInput;
        if (!inputDetected)
        {
            _idleTimer += Time.deltaTime;

            if (_idleTimer >= idleTimeout)
            {
                _idleTimer = 0f;
                _animator.SetTrigger(_hashTimeoutToIdle);
            }
        }
        else
        {
            _idleTimer = 0f;
            _animator.ResetTrigger(_hashTimeoutToIdle);
        }

        _animator.SetBool(_hashInputDetected, inputDetected);
    }

    // Called each physics step (so long as the Animator component is set to Animate Physics) after FixedUpdate to override root motion.
    void OnAnimatorMove()
    {
        Vector3 movement;

        // // If Ellen is on the ground...
        // {
        //     // ... raycast into the ground...
        //     RaycastHit hit;
        //     Ray ray = new Ray(transform.position + Vector3.up * k_GroundedRayDistance * 0.5f, -Vector3.up);
        //     if (Physics.Raycast(ray, out hit, k_GroundedRayDistance, Physics.AllLayers, QueryTriggerInteraction.Ignore))
        //     {
        //         // ... and get the movement of the root motion rotated to lie along the plane of the ground.
        //         movement = Vector3.ProjectOnPlane(_animator.deltaPosition, hit.normal);
        //     }
        //     else
        //     {
        //         // If no ground is hit just get the movement as the root motion.
        //         // Theoretically this should rarely happen as when grounded the ray should always hit.
        //         movement = _animator.deltaPosition;
        //         _currentWalkingSurface = null;
        //     }
        // }

        // Rotate the transform of the character controller by the animation's root rotation.
        _charCtrl.transform.rotation *= _animator.deltaRotation;

        Vector3 direction = Vector3.forward * _input.MoveInput.y + Vector3.right * _input.MoveInput.x;
        _charCtrl.Move(direction * maxForwardSpeed * Time.fixedDeltaTime);

        // Add to the movement with the calculated vertical speed.
        // movement += _verticalSpeed * Vector3.up * Time.deltaTime;

        // Move the character controller.
        // _charCtrl.Move(movement);
        Debug.Log(direction);
        _animator.SetBool(_hashGrounded, true);
    }
}