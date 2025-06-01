using UnityEngine;
using System.Collections;

// Main controller for car physics and movement
// Handles wheel control, drift mechanics, and car effects
public class CarController : MonoBehaviour
{
    [Header("Car Settings")]
    public float maxMotorTorque = 1500f; // Maximum torque the motor can apply to wheel
    public float maxSteeringAngle = 30f; // Maximum steer angle the wheels can have
    public float brakeForce = 5000f; // Force applied when braking
    public Vector3 centerOfMassOffset = new Vector3(0, -0.5f, 0); // Offset for the center of mass
    public float maxSpeed = 50f; // Maximum car speed (m/s)
    public float maxReverseSpeed = 20f; // Maximum speed in reverse (m/s) - Added for smoothing logic
    public float driftSlipThreshold = 0.35f; // Sensitivity to detect drift

    [Header("Wheels")]
    public WheelCollider frontLeftWheelCollider;
    public Transform frontLeftWheelMesh;
    public WheelCollider frontRightWheelCollider;
    public Transform frontRightWheelMesh;
    public WheelCollider rearLeftWheelCollider;
    public Transform rearLeftWheelMesh;
    public WheelCollider rearRightWheelCollider;
    public Transform rearRightWheelMesh;

    [Header("Effects")]
    public ParticleSystem rearLeftSmokeParticles;
    public ParticleSystem rearRightSmokeParticles;
    public TrailRenderer rearLeftTireSkid;
    public TrailRenderer rearRightTireSkid;
    public float skidThreshold = 0.8f; // Slip threshold to start showing skids & smoke

    // Core variables for car control
    private Rigidbody rb;
    private float motorInput;
    private float steeringInput;
    private bool brakingInput;

    // Variables for smooth acceleration/deceleration
    private float throttleAxis; // Smooth throttle value from -1 to 1
    private bool deceleratingCar;

    // Original wheel friction values for drift control
    private WheelFrictionCurve originalRearFriction;
    private bool isHandbrakeActive = false;

    // Initialize car physics and save original wheel settings
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb != null && centerOfMassOffset != Vector3.zero)
        {
            rb.centerOfMass += centerOfMassOffset;
        }

        // Save original rear wheel friction
        originalRearFriction = rearLeftWheelCollider.sidewaysFriction;
    }

    // Handle player input and update car state every frame
    void Update()
    {
        // Get Player input
        motorInput = Input.GetAxis("Vertical");
        steeringInput = Input.GetAxis("Horizontal");
        brakingInput = Input.GetKey(KeyCode.Space);

        // Apply smoothing based on Player input
        UpdateSmoothedInputs(motorInput, brakingInput);

        // Handbrake for drift
        if (Input.GetKeyDown(KeyCode.Space) && !isHandbrakeActive)
        {
            SetRearFriction(0.3f); // Low value = more drift
            isHandbrakeActive = true;
        }
        if (Input.GetKeyUp(KeyCode.Space) && isHandbrakeActive)
        {
            SetRearFriction(originalRearFriction.stiffness); // Restore original grip
            isHandbrakeActive = false;
        }

        HandleEffects();
    }

    // Smoothly adjust throttle and handle braking input
    // This creates more realistic acceleration and deceleration
    void UpdateSmoothedInputs(float rawMotorInput, bool rawBrakingInput)
    {
        // Smooth throttleAxis
        if (rawMotorInput > 0.1f) // Accelerating forward
        {
            CancelInvoke("DecelerateCar");
            deceleratingCar = false;
            throttleAxis += Time.deltaTime * 5f; // Smooth increase
            if (throttleAxis > 1f) throttleAxis = 1f;
        } else if (rawMotorInput < -0.1f) // Accelerating backward
        {
            CancelInvoke("DecelerateCar");
            deceleratingCar = false;
            throttleAxis -= Time.deltaTime * 3f; // Smooth decrease
            if (throttleAxis < -1f) throttleAxis = -1f;
        } else // No manual throttle input
        {
            if (!rawBrakingInput) // Only decelerate if not manually braking
            {
                 if (!deceleratingCar) // Start deceleration process
                 {
                    InvokeRepeating("DecelerateCar", 0f, 0.05f); // Repeat every 0.05s
                    deceleratingCar = true;
                 }
            }
        }

        // Handle manual braking
        if (rawBrakingInput)
        {
            CancelInvoke("DecelerateCar"); // Stop auto-deceleration if manually braking
            deceleratingCar = false;
            // When braking, throttleAxis should go to 0 or even slightly negative to simulate braking force
            if (throttleAxis > 0) throttleAxis -= Time.deltaTime * 5f; // Faster decrease when braking
            if (throttleAxis < 0) throttleAxis += Time.deltaTime * 5f;
            if (Mathf.Abs(throttleAxis) < 0.1f) throttleAxis = 0f;
        }
         // Ensure throttleAxis is 0 when fully stopped and not accelerating/braking
        if (Mathf.Approximately(rawMotorInput, 0f) && !rawBrakingInput && Mathf.Abs(rb.velocity.magnitude) < 0.1f)
        {
             throttleAxis = 0f;
             CancelInvoke("DecelerateCar");
             deceleratingCar = false;
        }

    }

    // Main physics update - handles wheel movement, speed limits, and forces
    void FixedUpdate()
    {
        // Limit maximum speed
        if (rb.velocity.magnitude > maxSpeed && throttleAxis > 0) // Only clamp if trying to go faster than max speed forward
        {
            rb.velocity = rb.velocity.normalized * maxSpeed;
            throttleAxis = Mathf.Lerp(throttleAxis, 0, Time.fixedDeltaTime * 2f); // Reduce torque if above max speed
        }
        if (rb.velocity.magnitude > maxReverseSpeed && throttleAxis < 0) // Clamp reverse speed
        {
            rb.velocity = rb.velocity.normalized * maxReverseSpeed;
            throttleAxis = Mathf.Lerp(throttleAxis, 0, Time.fixedDeltaTime * 2f); // Reduce torque if above max reverse speed
        }

        // Apply steering
        float steeringAngle = maxSteeringAngle * steeringInput;
        frontLeftWheelCollider.steerAngle = steeringAngle;
        frontRightWheelCollider.steerAngle = steeringAngle;

        // Apply motor torque or brake based on smoothed throttleAxis and brakingInput
        if (brakingInput) // Use the raw braking input for immediate brake force
        {
            ApplyBrakes();
        }
        else
        {
            ApplyMotorTorqueSmoothed(); // Use the new smoothed torque method
            ReleaseBrakes(); // Release brakes if not braking
        }

        // Update wheel mesh positions and rotations
        UpdateWheelMeshes();
    }

    // Apply motor force to rear wheels using smoothed throttle
    // This creates more realistic acceleration
    void ApplyMotorTorqueSmoothed()
    {
        // Apply torque to rear wheels based on smoothed throttleAxis
        rearLeftWheelCollider.motorTorque = maxMotorTorque * throttleAxis;
        rearRightWheelCollider.motorTorque = maxMotorTorque * throttleAxis;

        // Ensure front wheels don't have torque in this RWD setup (adjust if AWD/FWD)
        frontLeftWheelCollider.motorTorque = 0;
        frontRightWheelCollider.motorTorque = 0;
    }

    // Apply brake force to all wheels
    // Stops the car immediately when braking
    void ApplyBrakes()
    {
        // Apply brake torque to all wheels
        rearLeftWheelCollider.brakeTorque = brakeForce;
        rearRightWheelCollider.brakeTorque = brakeForce;
        frontLeftWheelCollider.brakeTorque = brakeForce;
        frontRightWheelCollider.brakeTorque = brakeForce;

        // Stop motor torque immediately when braking
        rearLeftWheelCollider.motorTorque = 0;
        rearRightWheelCollider.motorTorque = 0;
        frontLeftWheelCollider.motorTorque = 0;
        frontRightWheelCollider.motorTorque = 0;
    }

    // Remove brake force from all wheels
    void ReleaseBrakes()
    {
         rearLeftWheelCollider.brakeTorque = 0;
         rearRightWheelCollider.brakeTorque = 0;
         frontLeftWheelCollider.brakeTorque = 0;
         frontRightWheelCollider.brakeTorque = 0;
    }

    // Update visual wheel positions to match physics
    void UpdateWheelMeshes()
    {
        UpdateWheel(frontLeftWheelCollider, frontLeftWheelMesh);
        UpdateWheel(frontRightWheelCollider, frontRightWheelMesh);
        UpdateWheel(rearLeftWheelCollider, rearLeftWheelMesh);
        UpdateWheel(rearRightWheelCollider, rearRightWheelMesh);
    }

    // Update a single wheel's visual position and rotation
    void UpdateWheel(WheelCollider col, Transform mesh)
    {
        if (mesh == null) return;
        Vector3 position;
        Quaternion rotation;
        col.GetWorldPose(out position, out rotation);
        mesh.position = position;
        mesh.rotation = rotation;
    }

    // Handle visual effects like smoke and skid marks during drift
    void HandleEffects()
    {
        WheelHit hitLeft, hitRight;
        float carSpeed = rb.velocity.magnitude;

        // Check rear left wheel
        if (rearLeftWheelCollider.GetGroundHit(out hitLeft))
        {
            if (Mathf.Abs(hitLeft.sidewaysSlip) > driftSlipThreshold && carSpeed > 5f)
            {
                if (rearLeftSmokeParticles != null && !rearLeftSmokeParticles.isEmitting) rearLeftSmokeParticles.Play();
                if (rearLeftTireSkid != null) rearLeftTireSkid.emitting = true;
            }
            else
            {
                if (rearLeftSmokeParticles != null && rearLeftSmokeParticles.isEmitting) rearLeftSmokeParticles.Stop();
                if (rearLeftTireSkid != null) rearLeftTireSkid.emitting = false;
            }
        }

        // Check rear right wheel
        if (rearRightWheelCollider.GetGroundHit(out hitRight))
        {
            if (Mathf.Abs(hitRight.sidewaysSlip) > driftSlipThreshold && carSpeed > 5f)
            {
                if (rearRightSmokeParticles != null && !rearRightSmokeParticles.isEmitting) rearRightSmokeParticles.Play();
                if (rearRightTireSkid != null) rearRightTireSkid.emitting = true;
            }
            else
            {
                if (rearRightSmokeParticles != null && rearRightSmokeParticles.isEmitting) rearRightSmokeParticles.Stop();
                if (rearRightTireSkid != null) rearRightTireSkid.emitting = false;
            }
        }
    }

    // Adjust rear wheel friction for drift control
    // Lower values create more drift
    void SetRearFriction(float stiffness)
    {
        WheelFrictionCurve friction = rearLeftWheelCollider.sidewaysFriction;
        friction.stiffness = stiffness;
        rearLeftWheelCollider.sidewaysFriction = friction;
        rearRightWheelCollider.sidewaysFriction = friction;
    }

    // Gradually slow down the car when no input is given
    // Creates natural deceleration
    void DecelerateCar()
    {
        // Reduce throttleAxis towards 0 smoothly
        if (throttleAxis > 0f)
        {
            throttleAxis -= Time.deltaTime * 100f; // Adjust deceleration rate
            if (throttleAxis < 0) throttleAxis = 0f;
        } else if (throttleAxis < 0f)
        {
            throttleAxis += Time.deltaTime * 5f;
            if (throttleAxis > 0) throttleAxis = 0f;
        }

        // Apply drag based on decelerationMultiplier (optional, Rigidbody drag also works)
        // carRigidbody.velocity = carRigidbody.velocity * (1f / (1f + (0.025f * decelerationMultiplier)));

        // Remove motor torque
        rearLeftWheelCollider.motorTorque = 0;
        rearRightWheelCollider.motorTorque = 0;

        // If speed is very low, stop completely and cancel deceleration invoke
        if (Mathf.Abs(rb.velocity.magnitude) < 0.1f && Mathf.Approximately(throttleAxis, 0f))
        {
            rb.velocity = Vector3.zero;
            CancelInvoke("DecelerateCar");
            deceleratingCar = false;
        }
    }
}
