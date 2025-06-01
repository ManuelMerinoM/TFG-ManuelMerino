using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Individual checkpoint behavior for racing track
// Detects when cars or AI agents pass through and notifies the track system
public class CheckpointSingle : MonoBehaviour
{
    // Reference to the main track checkpoint system
    private TrackCheckpoints trackCheckpoints;

    // Called when a car or agent enters the checkpoint trigger
    // Handles both player cars and AI agents
    private void OnTriggerEnter(Collider other)
    {
        // Check for either CarController (player) or NNCheck (AI agent) component
        CarController player = other.GetComponent<CarController>();
        NNCheck agent = other.GetComponent<NNCheck>();

        // Handle player car passing through
        if (player != null)
        {
            trackCheckpoints.CarThroughCheckpoint(this, other.transform);
            Debug.Log("Checkpoint passed by Player");
        }
        // Handle AI agent passing through
        else if (agent != null)
        {
            trackCheckpoints.CarThroughCheckpoint(this, other.transform);
            Debug.Log("Checkpoint passed by Agent");
        }
    }

    // Sets up the connection to the main track checkpoint system
    // Called when the checkpoint is created
    public void SetTrackCheckpoints(TrackCheckpoints trackCheckpoints)
    {
        this.trackCheckpoints = trackCheckpoints;
    }
}
