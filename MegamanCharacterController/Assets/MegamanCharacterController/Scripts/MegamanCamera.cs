using UnityEngine;
using System.Collections;

public class MegamanCamera : MonoBehaviour
{

    public float Distance = 5.0f;
    public float Height = 2.0f;

    public GameObject PlayerTarget;

    private PlayerInputController input;
    private Transform target;
    private MegamanMachine machine;
    private float yRotation;

    private SuperCharacterController controller;

    // Use this for initialization
    void Start()
    {
        input = PlayerTarget.GetComponent<PlayerInputController>();
        machine = PlayerTarget.GetComponent<MegamanMachine>();
        controller = PlayerTarget.GetComponent<SuperCharacterController>();
        target = PlayerTarget.transform;
    }

    // Update is called once per frame
    void LateUpdate()
    {
        transform.position = target.position;

        if (!machine.IsSideScrolling)
        {
            yRotation += input.Current.MouseInput.y;

            Vector3 left = Vector3.Cross(machine.lookDirection, controller.up);

            transform.rotation = Quaternion.LookRotation(machine.lookDirection, controller.up);
            transform.rotation = Quaternion.AngleAxis(yRotation, left) * transform.rotation;
        }

        transform.position -= transform.forward * Distance;
        transform.position += controller.up * Height;
    }
}
