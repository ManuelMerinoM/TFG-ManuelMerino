using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using System.IO;
using Unity.MLAgents.Demonstrations;

public class NNCursor : Agent
{
    [Header("Movement Settings")]
    public float movementSpeed = 30f;
    public float turnSpeed = 100f;
    
    [Header("Training Settings")]
    [Tooltip("Enable/disable episode management for testing")]
    public bool enableEpisodes = true;
    [Tooltip("Enable recording demonstrations for imitation learning")]
    public bool recordDemonstrations = false;
    [Tooltip("Name for the demonstration recording")]
    public string demonstrationName = "NNCursorDemo";
    
    [Header("Reward Settings")]
    [Tooltip("Reward for passing through the correct checkpoint")]
    public float checkpointReward = 1.0f;
    [Tooltip("Additional reward multiplier for passing through the center of a checkpoint")]
    public float centerCheckpointMultiplier = 0.5f;
    [Tooltip("Penalty for hitting walls")]
    public float wallCollisionPenalty = -1.0f;
    [Tooltip("Penalty for going the wrong way or hitting the wrong checkpoint")]
    public float wrongDirectionPenalty = -0.5f;
    [Tooltip("Small negative reward per step to encourage efficiency")]
    public float timeStepPenalty = -0.001f;
    
    [Header("References")]
    [SerializeField] private TrackCheckpoints trackCheckpoints;
    
    // Private variables
    private Rigidbody rb;
    private Vector3 startPosition;
    private Quaternion startRotation;
    private Bounds carBounds;
    private float distanceToNextCheckpoint;
    private float lastDistanceToCheckpoint;
    private Transform nextCheckpointTransform;
    private bool hasRayPerceptionSensor = false;
    
    // Demonstration recorder
    private DemonstrationRecorder demoRecorder;

    public override void Initialize()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }
        
        rb.drag = 1f;
        rb.angularDrag = 5f;
        rb.interpolation = RigidbodyInterpolation.Extrapolate;
        
        // Store initial position and rotation for episode resets
        startPosition = transform.position;
        startRotation = transform.rotation;
        
        // Get car bounds for ground check
        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null)
        {
            carBounds = renderer.bounds;
        }
        else
        {
            carBounds = new Bounds(transform.position, Vector3.one);
        }
        
        // Check if we have a RayPerceptionSensor attached
        hasRayPerceptionSensor = GetComponent<RayPerceptionSensorComponent3D>() != null;
        if (!hasRayPerceptionSensor)
        {
            Debug.LogWarning("No RayPerceptionSensorComponent3D found on " + gameObject.name + ". Please add one for better performance.");
        }
        
        // Register checkpoint events
        if (trackCheckpoints != null)
        {
            trackCheckpoints.OnCarCorrectCheckpoint += OnCarCorrectCheckpoint;
            trackCheckpoints.OnCarWrongCheckpoint += OnCarWrongCheckpoint;
        }
        else
        {
            Debug.LogError("TrackCheckpoints reference is missing!");
        }
        
        // Setup demo recorder if needed
        SetupDemonstrationRecorder();
    }
    
    private void SetupDemonstrationRecorder()
    {
        if (recordDemonstrations)
        {
            // Check if we already have a DemonstrationRecorder
            demoRecorder = GetComponent<DemonstrationRecorder>();
            
            // If not, add one
            if (demoRecorder == null)
            {
                demoRecorder = gameObject.AddComponent<DemonstrationRecorder>();
                demoRecorder.Record = true;
                demoRecorder.DemonstrationName = demonstrationName;
                
                string path = Path.Combine(Application.dataPath, "Demonstrations");
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }
                
                demoRecorder.DemonstrationDirectory = "Assets/Demonstrations";
                Debug.Log("Demonstration recorder set up. Recording to: " + path);
            }
        }
        else if (demoRecorder != null)
        {
            // Disable recording if it exists but should not be active
            demoRecorder.Record = false;
        }
    }

    public override void OnEpisodeBegin()
    {
        // Reset car position, rotation and velocity
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        transform.position = startPosition;
        transform.rotation = startRotation;
        
        // Reset checkpoints
        if (trackCheckpoints != null)
        {
            trackCheckpoints.ResetCheckpoint(transform);
            UpdateNextCheckpointInfo();
        }
        
        lastDistanceToCheckpoint = GetDistanceToNextCheckpoint();
        
        // Update demo recorder settings if needed
        if (recordDemonstrations && demoRecorder != null)
        {
            demoRecorder.Record = true;
        }
    }
    
    private void UpdateNextCheckpointInfo()
    {
        CheckpointSingle nextCheckpoint = trackCheckpoints.GetNextCheckpointPosition(transform);
        if (nextCheckpoint != null)
        {
            nextCheckpointTransform = nextCheckpoint.transform;
            distanceToNextCheckpoint = Vector3.Distance(transform.position, nextCheckpointTransform.position);
        }
    }
    
    private float GetDistanceToNextCheckpoint()
    {
        if (nextCheckpointTransform != null)
        {
            return Vector3.Distance(transform.position, nextCheckpointTransform.position);
        }
        return 0f;
    }
    
    public override void CollectObservations(VectorSensor sensor)
    {
        if (trackCheckpoints == null || nextCheckpointTransform == null) return;
        
        // Direction to the next checkpoint
        Vector3 dirToCheckpoint = (nextCheckpointTransform.position - transform.position).normalized;
        
        // Dot product between car's forward direction and direction to checkpoint
        // 1 = facing directly toward checkpoint, -1 = facing directly away
        float forwardDot = Vector3.Dot(transform.forward, dirToCheckpoint);
        sensor.AddObservation(forwardDot);
        
        // Right dot product to help with steering
        float rightDot = Vector3.Dot(transform.right, dirToCheckpoint);
        sensor.AddObservation(rightDot);
        
        // Distance to next checkpoint (normalized)
        sensor.AddObservation(distanceToNextCheckpoint / 100f);
        
        // Car's velocity (normalized)
        sensor.AddObservation(rb.velocity.magnitude / 20f);
        
        // Car's angular velocity (normalized)
        sensor.AddObservation(rb.angularVelocity.magnitude / 10f);
        
        // Is car grounded?
        sensor.AddObservation(IsGrounded() ? 1f : 0f);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        // Apply small negative reward per step to encourage efficiency
        AddReward(timeStepPenalty);
        
        // Check if car is flipped
        if (!IsGrounded())
        {
            AddReward(-0.1f);
            EndEpisode();
            return;
        }
        
        // Process movement actions
        int moveAction = actions.DiscreteActions[0];
        int turnAction = actions.DiscreteActions[1];
        
        // Apply forward/backward movement
        switch (moveAction)
        {
            case 1: // Forward
                rb.AddRelativeForce(Vector3.forward * movementSpeed * Time.deltaTime, ForceMode.VelocityChange);
                break;
            case 2: // Backward
                rb.AddRelativeForce(Vector3.back * movementSpeed * Time.deltaTime, ForceMode.VelocityChange);
                break;
        }
        
        // Apply turning
        switch (turnAction)
        {
            case 1: // Left
                transform.Rotate(Vector3.up, -turnSpeed * Time.deltaTime);
                break;
            case 2: // Right
                transform.Rotate(Vector3.up, turnSpeed * Time.deltaTime);
                break;
        }
        
        // Reward progress toward checkpoint
        if (nextCheckpointTransform != null)
        {
            float currentDistance = GetDistanceToNextCheckpoint();
            float deltaDistance = lastDistanceToCheckpoint - currentDistance;
            
            if (deltaDistance > 0)
            {
                // Moving toward checkpoint
                AddReward(0.001f * deltaDistance);
            }
            else if (deltaDistance < -0.5f)
            {
                // Moving away from checkpoint significantly
                AddReward(0.001f * deltaDistance);
            }
            
            lastDistanceToCheckpoint = currentDistance;
        }
    }
    
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var discreteActions = actionsOut.DiscreteActions;
        
        // Default: no movement, no turning
        discreteActions[0] = 0;
        discreteActions[1] = 0;
        
        // Get input from keyboard
        float vertical = Input.GetAxis("Vertical");
        float horizontal = Input.GetAxis("Horizontal");
        
        // Set forward/backward action
        if (vertical > 0)
            discreteActions[0] = 1; // Forward
        else if (vertical < 0)
            discreteActions[0] = 2; // Backward
            
        // Set turning action
        if (horizontal > 0)
            discreteActions[1] = 2; // Right
        else if (horizontal < 0)
            discreteActions[1] = 1; // Left
    }
    
    private void OnCarCorrectCheckpoint(object sender, TrackCheckpoints.CarCheckPointEventArgs e)
    {
        if (e.carTransform == transform)
        {
            // Base reward for correct checkpoint
            AddReward(checkpointReward);
            
            // Extra reward for passing through center
            if (IsNearCenter(e.carTransform.position, nextCheckpointTransform.position, 1.5f))
            {
                AddReward(centerCheckpointMultiplier);
            }
            
            // Update next checkpoint info
            UpdateNextCheckpointInfo();
            lastDistanceToCheckpoint = GetDistanceToNextCheckpoint();
        }
    }
    
    private void OnCarWrongCheckpoint(object sender, TrackCheckpoints.CarCheckPointEventArgs e)
    {
        if (e.carTransform == transform)
        {
            AddReward(wrongDirectionPenalty);
        }
    }
    
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("ParedExt") || 
            collision.gameObject.CompareTag("ParedInt"))
        {
            // Penalty for hitting walls
            AddReward(wallCollisionPenalty);
            
            // End episode on wall collision only if episodes are enabled
            if (enableEpisodes)
            {
                EndEpisode();
            }
        }
        else if (collision.gameObject.CompareTag("Coche"))
        {
            // Penalty for hitting other cars
            AddReward(-0.05f);
        }       
    }
    
    private bool IsGrounded()
    {
        // Check if car is upright using raycast
        float rayLength = carBounds.extents.y * 1.1f;
        return Physics.Raycast(transform.position, -transform.up, rayLength);
    }
    
    private bool IsNearCenter(Vector3 carPosition, Vector3 checkpointPosition, float threshold)
    {
        // Project positions to XZ plane (ignore Y)
        Vector2 carPos2D = new Vector2(carPosition.x, carPosition.z);
        Vector2 checkpointPos2D = new Vector2(checkpointPosition.x, checkpointPosition.z);
        
        // Calculate distance in XZ plane
        float distance = Vector2.Distance(carPos2D, checkpointPos2D);
        
        return distance < threshold;
    }
    
    // Unity method called when component settings are changed in the editor
    private void OnValidate()
    {
        if (Application.isPlaying && recordDemonstrations != (demoRecorder != null && demoRecorder.Record))
        {
            SetupDemonstrationRecorder();
        }
    }
}
