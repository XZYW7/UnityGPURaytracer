using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Range(0,10)] public float speed = 2f;
    [Range(0,10)] public float RotateSpeed = 2f;
    // Start is called before the first frame update

    private float horizontalInput;
    private float forwardInput;


    private float deltaX;
    private float deltaY;

    void Start()
    {
        deltaX = transform.localEulerAngles.y;
        deltaY = transform.localEulerAngles.x;
        //DontDestroyOnLoad(transform.gameObject);
    }

    // Update is called once per frame
    void Update()
    {
        horizontalInput = Input.GetAxis("Horizontal");
        forwardInput = Input.GetAxis("Vertical");
        transform.Translate(Vector3.forward * Time.deltaTime * forwardInput* speed);
        transform.Translate(Vector3.right * Time.deltaTime * horizontalInput* speed);

        if (Input.GetKey(KeyCode.Q))
        {
            transform.Translate(Vector3.down * Time.deltaTime * speed);
        }
        if (Input.GetKey(KeyCode.E))
        {
            transform.Translate(Vector3.up * Time.deltaTime * speed);
        }

        if (Input.GetMouseButton(1))
        {
            deltaX += Input.GetAxis("Mouse X") * RotateSpeed;
            deltaY -= Input.GetAxis("Mouse Y") * RotateSpeed;


            deltaX = ClampAngle(deltaX, -360, 360);
            deltaY = ClampAngle(deltaY, -70, 70);

            transform.localRotation = Quaternion.Euler(deltaY, deltaX, 0);
        }

    }
    float ClampAngle(float angle, float minAngle, float maxAgnle)
    {
        if (angle <= -360)
            angle += 360;
        if (angle >= 360)
            angle -= 360;

        return Mathf.Clamp(angle, minAngle, maxAgnle);
    }


}
