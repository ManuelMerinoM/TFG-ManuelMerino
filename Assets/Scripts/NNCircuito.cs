using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;

// Neural Network agent for racing circuit
// Controls a car using ML-Agents, learning to navigate the track
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(MeshCollider))]
[RequireComponent(typeof(DecisionRequester))]
public class NNCircuito : Agent
{
    // Reward configuration for the agent's learning
    [System.Serializable]
    public class RewardInfo
    {
        public float no_movement = -0.1f;      // Penalty for not moving
        public float mult_forward = 0.001f;    // Reward multiplier for moving forward
        public float mult_backward = -0.001f;  // Penalty multiplier for moving backward
        public float mult_barrier = -0.8f;     // Penalty for hitting barriers
        public float mult_car = -0.5f;         // Penalty for car collisions
    }

    // Movement and control settings
    public float Movespeed = 30;               // Forward/backward movement speed
    public float Turnspeed = 100;              // Rotation speed
    public RewardInfo rwd = new RewardInfo();  // Reward configuration
    public bool doEpisodes = true;             // Whether to end episodes on collisions

    // Internal components and state
    private Rigidbody rb = null;
    private Vector3 posicion_original;         // Starting position
    private Quaternion rotacion_original;      // Starting rotation
    private Bounds bnd;                        // Car's bounds for raycasting

    // Initialize the agent's components and save initial state
    public override void Initialize()
    {
        // Setup physics
        rb = this.GetComponent<Rigidbody>();
        rb.drag = 1;
        rb.angularDrag = 5;
        rb.interpolation = RigidbodyInterpolation.Extrapolate;

        // Setup collider and decision making
        this.GetComponent<MeshCollider>().convex = true;
        this.GetComponent<DecisionRequester>().DecisionPeriod = 1;
        bnd = this.GetComponent<MeshRenderer>().bounds;

        // Save initial position and rotation
        posicion_original = this.transform.position;
        rotacion_original = this.transform.rotation;
    }

    // Reset agent state at the start of each episode
    public override void OnEpisodeBegin()
    {
        rb.velocity = Vector3.zero;
        this.transform.position = posicion_original;
        this.transform.rotation = rotacion_original;
    }

    // Process actions from the neural network
    // Handles movement and rotation based on discrete actions
    public override void OnActionReceived(ActionBuffers actions)
    {
        // Only process actions if car is upright
        if (isBocaArriba() == false)
            return;

        float mag = Mathf.Abs(rb.velocity.sqrMagnitude);

        // Handle movement actions (0: no movement, 1: backward, 2: forward)
        switch (actions.DiscreteActions.Array[0])
        {
            case 0:
                AddReward(rwd.no_movement);    // Penalty for not moving
                break;
            case 1:
                rb.AddRelativeForce(Vector3.back * Movespeed * Time.deltaTime, ForceMode.VelocityChange);
                AddReward(mag * rwd.mult_backward);  // Penalty for moving backward
                break;
            case 2:
                rb.AddRelativeForce(Vector3.forward * Movespeed * Time.deltaTime, ForceMode.VelocityChange);
                AddReward(mag * rwd.mult_forward);   // Reward for moving forward
                break;
        }

        // Handle rotation actions (0: no turn, 1: left, 2: right)
        switch (actions.DiscreteActions.Array[1])
        {
            case 0:
                break;  // No rotation
            case 1:
                this.transform.Rotate(Vector3.up, -Turnspeed * Time.deltaTime);  // Turn left
                break;
            case 2:
                this.transform.Rotate(Vector3.up, Turnspeed * Time.deltaTime);   // Turn right
                break;
        }
    }

    // Manual control for testing the agent
    // Maps keyboard input to agent actions
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        // Initialize actions to no movement/no turn
        actionsOut.DiscreteActions.Array[0] = 0;
        actionsOut.DiscreteActions.Array[1] = 0;

        // Get input from keyboard
        float move = Input.GetAxis("Vertical");     
        float turn = Input.GetAxis("Horizontal");

        // Map vertical input to movement
        if (move < 0)
            actionsOut.DiscreteActions.Array[0] = 1;    // Backward
        else if (move > 0)
            actionsOut.DiscreteActions.Array[0] = 2;    // Forward

        // Map horizontal input to rotation
        if (turn < 0)
            actionsOut.DiscreteActions.Array[1] = 1;    // Turn left
        else if (turn > 0)
            actionsOut.DiscreteActions.Array[1] = 2;    // Turn right
    }

    // Handle collisions with track barriers and other cars
    private void OnCollisionEnter(Collision collision)
    {
        float mag = collision.relativeVelocity.sqrMagnitude;

        // Check for barrier collisions
        if (collision.gameObject.CompareTag("ParedExt") == true
            || collision.gameObject.CompareTag("ParedInt") == true)
        {
            AddReward(mag * rwd.mult_barrier);  // Apply barrier collision penalty
            if (doEpisodes == true)
                EndEpisode();
        }
        // Check for car collisions
        else if (collision.gameObject.CompareTag("Coche") == true)
        {
            AddReward(mag * rwd.mult_car);      // Apply car collision penalty
            if (doEpisodes == true)
                EndEpisode();
        }
    }

    // Check if the car is upright by raycasting downward
    // Returns true if the car is not flipped over
    private bool isBocaArriba()
    {
        return Physics.Raycast(this.transform.position, -this.transform.up, bnd.size.y * 0.55f);
    }
}