using UnityEngine;
using System.Collections;

/*
 * Example implementation of the SuperStateMachine and SuperCharacterController
 */
[RequireComponent(typeof(SuperCharacterController))]
[RequireComponent(typeof(MegamanInputController))]
public class MegamanMachine : SuperStateMachine
{

    public Transform AnimatedMesh;

    public bool IsSideScrolling = false;
    public bool IsLockPositionZ = false;
    public float RunSpeed = 10f;
    public float DashSpeed = 20f;
    public float DashDuration = 2f;
    public float JumpHeight = 6f;
    public float JumpSpeed = 40f;
    public float Gravity = 55f;
    public float MaxGravity = 65f;
    public float AirMoveCount = 1f;
    public float InterruptJumpForce = 100f;

    // Add more states by comma separating them
    enum PlayerStates { Idle, Walk, Jump, Fall, Dash, AirJump, AirDash }

    private SuperCharacterController controller;

    // current velocity
    private Vector3 moveDirection;
    // current direction our character's art is facing
    public Vector3 lookDirection { get; private set; }

    private MegamanInputController input;
    private float spawnPositionZ;
    private float beforeDashTime;
    private bool isDashJump;
    private int airMoveCount;

    #region Generic update functions
    void Start()
    {
        // Put any code here you want to run ONCE, when the object is initialized

        input = gameObject.GetComponent<MegamanInputController>();

        // Grab the controller object from our object
        controller = gameObject.GetComponent<SuperCharacterController>();

        // Our character's current facing direction, planar to the ground
        if (!IsSideScrolling)
        {
            lookDirection = transform.forward;
        }
        else
        {
            lookDirection = transform.right;
        }

        // Set our currentState to idle on startup
        currentState = PlayerStates.Idle;

        // Keep temporary z position to locking
        spawnPositionZ = transform.position.z;
    }

    protected override void EarlyGlobalSuperUpdate()
    {
        // Rotate out facing direction horizontally based on mouse input
        if (!IsSideScrolling)
            lookDirection = Quaternion.AngleAxis(input.Current.MouseInput.x, controller.up) * lookDirection;
        else
        {
            if (input.Current.MoveInput.magnitude > 0)
            {
                if (!IsLockPositionZ)
                {
                    lookDirection = input.Current.MoveInput.normalized;
                }
                else if (input.Current.MoveInput.x != 0)
                {
                    lookDirection = transform.right * input.Current.MoveInput.x;
                }
            }
        }
        // Put any code in here you want to run BEFORE the state's update function.
        // This is run regardless of what state you're in
    }

    protected override void LateGlobalSuperUpdate()
    {
        // Put any code in here you want to run AFTER the state's update function.
        // This is run regardless of what state you're in

        // Move the player by our velocity every frame
        transform.position += moveDirection * controller.deltaTime;

        if (IsLockPositionZ)
            transform.position = new Vector3(transform.position.x, transform.position.y, spawnPositionZ);

        // Rotate our mesh to face where we are "looking"
        AnimatedMesh.rotation = Quaternion.LookRotation(lookDirection, controller.up);
    }

    public void RotateGravity(Vector3 up)
    {
        lookDirection = Quaternion.FromToRotation(transform.up, up) * lookDirection;
    }

    /// <summary>
    /// Constructs a vector representing our movement local to our lookDirection, which is
    /// controlled by the camera
    /// </summary>
    private Vector3 LocalMovement()
    {
        Vector3 right = Vector3.Cross(controller.up, lookDirection);

        Vector3 local = Vector3.zero;

        if (!IsSideScrolling)
        {
            if (input.Current.MoveInput.x != 0)
            {
                local += right * input.Current.MoveInput.x;
            }

            if (input.Current.MoveInput.z != 0)
            {
                local += lookDirection * input.Current.MoveInput.z;
            }
        }
        else
        {
            local += transform.right * input.Current.MoveInput.x;
        }

        if (IsDashing() && !currentState.Equals(PlayerStates.Jump))
            local += lookDirection;

        return local.normalized;
    }

    /*void Update () {
	 * Update is normally run once on every frame update. We won't be using it
     * in this case, since the SuperCharacterController component sends a callback Update 
     * called SuperUpdate. SuperUpdate is recieved by the SuperStateMachine, and then fires
     * further callbacks depending on the state
	}*/
    #endregion

    #region Character states functions
    // Below are the three state functions. Each one is called based on the name of the state,
    // so when currentState = Idle, we call Idle_EnterState. If currentState = Jump, we call
    // Jump_SuperUpdate()
    void Idle_EnterState()
    {
        controller.EnableSlopeLimit();
        controller.EnableClamping();
        isDashJump = false;
        airMoveCount = 0;
    }

    void Idle_SuperUpdate()
    {
        // Run every frame we are in the idle state
        if (input.Current.DashInput)
        {
            currentState = PlayerStates.Dash;
            return;
        }

        if (input.Current.JumpInput)
        {
            currentState = PlayerStates.Jump;
            return;
        }

        if (!MaintainingGround())
        {
            currentState = PlayerStates.Fall;
            return;
        }

        if (input.Current.MoveInput != Vector3.zero)
        {
            currentState = PlayerStates.Walk;
            return;
        }

        // Apply friction to slow us to a halt
        moveDirection = Vector3.MoveTowards(moveDirection, Vector3.zero, 100.0f * controller.deltaTime);
    }

    void Idle_ExitState()
    {
        // Run once when we exit the idle state
    }

    void Walk_SuperUpdate()
    {
        if (input.Current.DashInput)
        {
            currentState = PlayerStates.Dash;
            return;
        }

        if (input.Current.JumpInput)
        {
            currentState = PlayerStates.Jump;
            return;
        }

        if (!MaintainingGround())
        {
            currentState = PlayerStates.Fall;
            return;
        }

        if (input.Current.MoveInput != Vector3.zero)
        {
            moveDirection = Vector3.MoveTowards(moveDirection, LocalMovement() * RunSpeed, RunSpeed);
        }
        else
        {
            currentState = PlayerStates.Idle;
            return;
        }
    }

    void Jump_EnterState()
    {
        controller.DisableClamping();
        controller.DisableSlopeLimit();

        if (IsDashing() || input.Current.DashInput)
            isDashJump = true;

        moveDirection.y = 0;
        moveDirection += controller.up * CalculateJumpSpeed(JumpHeight, Gravity);
    }

    void Jump_SuperUpdate()
    {
        if (IsAirJumping() || IsAirDashing())
            return;

        Vector3 planarMoveDirection = Math3d.ProjectVectorOnPlane(controller.up, moveDirection);
        Vector3 verticalMoveDirection = moveDirection - planarMoveDirection;

        if (IsJumpingInterrupt(planarMoveDirection, verticalMoveDirection))
            return;

        ApplyGravity(planarMoveDirection, verticalMoveDirection);
    }

    void AirJump_EnterState()
    {
        controller.DisableClamping();
        controller.DisableSlopeLimit();

        moveDirection.y = 0;
        moveDirection += controller.up * CalculateJumpSpeed(JumpHeight, Gravity);

        ++airMoveCount;
    }

    void AirJump_SuperUpdate()
    {
        Jump_SuperUpdate();
    }

    void Fall_EnterState()
    {
        controller.DisableClamping();
        controller.DisableSlopeLimit();
    }

    void Fall_SuperUpdate()
    {
        if (IsAirJumping() || IsAirDashing())
            return;

        Vector3 planarMoveDirection = Math3d.ProjectVectorOnPlane(controller.up, moveDirection);
        Vector3 verticalMoveDirection = moveDirection - planarMoveDirection;

        if (AcquiringGround())
        {
            moveDirection = planarMoveDirection;
            currentState = PlayerStates.Idle;
            return;
        }

        ApplyGravity(planarMoveDirection, verticalMoveDirection);
    }

    void Dash_EnterState()
    {
        beforeDashTime = Time.time;
        if (input.Current.JumpInput)
        {
            currentState = PlayerStates.Jump;
            return;
        }
    }

    void Dash_SuperUpdate()
    {
        if (input.Current.JumpInput)
        {
            currentState = PlayerStates.Jump;
            return;
        }

        if (!MaintainingGround())
        {
            currentState = PlayerStates.Fall;
            return;
        }

        if (!IsDashing())
        {
            currentState = PlayerStates.Idle;
            return;
        }

        moveDirection = Vector3.MoveTowards(moveDirection, LocalMovement() * DashSpeed, DashSpeed);
    }

    void AirDash_EnterState()
    {
        beforeDashTime = Time.time;
        moveDirection.y = 0;
        ++airMoveCount;
    }

    void AirDash_SuperUpdate()
    {
        if (IsAirJumping() || IsAirDashing())
            return;

        if (!IsDashing())
        {
            if (!MaintainingGround())
                currentState = PlayerStates.Fall;
            else
                currentState = PlayerStates.Idle;
            return;
        }

        moveDirection = Vector3.MoveTowards(moveDirection, LocalMovement() * DashSpeed, DashSpeed);
    }
    #endregion

    #region Helper functions
    private bool AcquiringGround()
    {
        return controller.currentGround.IsGrounded(false, 0.01f);
    }

    private bool MaintainingGround()
    {
        return controller.currentGround.IsGrounded(true, 0.5f);
    }

    // Calculate the initial velocity of a jump based off gravity and desired maximum height attained
    private float CalculateJumpSpeed(float jumpHeight, float gravity)
    {
        return Mathf.Sqrt(2 * jumpHeight * gravity);
    }

    private float CalculateMoveSpeedWhileAir(bool isDash)
    {
        return isDash ? DashSpeed : RunSpeed;
    }

    void ApplyGravity(Vector3 planarMoveDirection, Vector3 verticalMoveDirection)
    {
        // Player not hold jump button, interrupt jumping
        if (!input.Current.HoldJumpInput && verticalMoveDirection.y > 0)
            verticalMoveDirection.y = Mathf.MoveTowards(verticalMoveDirection.y, 0, InterruptJumpForce * controller.deltaTime);

        planarMoveDirection = LocalMovement() * CalculateMoveSpeedWhileAir(isDashJump);
        verticalMoveDirection.y = Mathf.MoveTowards(verticalMoveDirection.y, -MaxGravity, Gravity * controller.deltaTime);

        moveDirection = planarMoveDirection + verticalMoveDirection;
    }

    bool IsDashing()
    {
        return (input.Current.HoldDashInput && Time.time - beforeDashTime < DashDuration);
    }

    bool IsJumpingInterrupt(Vector3 planarMoveDirection, Vector3 verticalMoveDirection)
    {
        if (Vector3.Angle(verticalMoveDirection, controller.up) > 90 && AcquiringGround())
        {
            moveDirection = planarMoveDirection;
            currentState = PlayerStates.Idle;
            return true;
        }

        if (verticalMoveDirection.y < 0)
        {
            moveDirection = planarMoveDirection;
            currentState = PlayerStates.Fall;
            return true;
        }
        return false;
    }

    bool IsAirJumping()
    {
        if (CanAirJump() && input.Current.JumpInput)
        {
            currentState = PlayerStates.AirJump;
            return true;
        }
        return false;
    }

    bool IsAirDashing()
    {
        if (CanAirJump() && input.Current.DashInput)
        {
            currentState = PlayerStates.AirDash;
            return true;
        }
        return false;
    }

    bool CanAirJump()
    {
        return !isDashJump && (airMoveCount < AirMoveCount || AirMoveCount < 0);
    }
    #endregion
}
