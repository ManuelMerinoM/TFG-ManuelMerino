using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CarController : MonoBehaviour
{
    public float acceleration = 10f;
    public float steering = 2f;
    public float maxSpeed = 20f;

    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        float moveInput = Input.GetAxis("Vertical");
        float steerInput = Input.GetAxis("Horizontal");

        // Limitar la velocidad máxima
        if (rb.velocity.magnitude < maxSpeed)
        {
            rb.AddForce(transform.forward * moveInput * acceleration, ForceMode.Acceleration);
        }

        // Girar el coche
        if (rb.velocity.magnitude > 0.1f) // Solo girar si el coche se mueve
        {
            float turn = steerInput * steering * Time.fixedDeltaTime;
            Quaternion turnRotation = Quaternion.Euler(0f, turn * Mathf.Rad2Deg, 0f);
            rb.MoveRotation(rb.rotation * turnRotation);
        }
    }

}
