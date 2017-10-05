using UnityEngine;
using System.Collections;

public class MegamanInputController : MonoBehaviour
{

    public MegamanInput Current;
    public Vector2 RightStickMultiplier = new Vector2(3, -1.5f);
    private bool holdJumpInput;
    private bool holdDashInput;

    // Use this for initialization
    void Start()
    {
        Current = new MegamanInput();
    }

    // Update is called once per frame
    void Update()
    {

        // Retrieve our current WASD or Arrow Key input
        // Using GetAxisRaw removes any kind of gravity or filtering being applied to the input
        // Ensuring that we are getting either -1, 0 or 1
        Vector3 moveInput = new Vector3(Input.GetAxisRaw("Horizontal"), 0, Input.GetAxisRaw("Vertical"));

        Vector2 mouseInput = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));

        Vector2 rightStickInput = new Vector2(Input.GetAxisRaw("RightH"), Input.GetAxisRaw("RightV"));

        // pass rightStick values in place of mouse when non-zero
        mouseInput.x = rightStickInput.x != 0 ? rightStickInput.x * RightStickMultiplier.x : mouseInput.x;
        mouseInput.y = rightStickInput.y != 0 ? rightStickInput.y * RightStickMultiplier.y : mouseInput.y;

        bool jumpInput = Input.GetButtonDown("Jump");
        bool dashInput = Input.GetButtonDown("Dash");

        if (jumpInput)
            holdJumpInput = true;

        if (dashInput)
            holdDashInput = true;

        if (holdJumpInput && Input.GetButtonUp("Jump"))
            holdJumpInput = false;

        if (holdDashInput && Input.GetButtonUp("Dash"))
            holdDashInput = false;

        Current = new MegamanInput()
        {
            MoveInput = moveInput,
            MouseInput = mouseInput,
            JumpInput = jumpInput,
            HoldJumpInput = holdJumpInput,
            DashInput = dashInput,
            HoldDashInput = holdDashInput,
        };
    }
}

public struct MegamanInput
{
    public Vector3 MoveInput;
    public Vector2 MouseInput;
    public bool JumpInput;
    public bool HoldJumpInput;
    public bool DashInput;
    public bool HoldDashInput;
}
