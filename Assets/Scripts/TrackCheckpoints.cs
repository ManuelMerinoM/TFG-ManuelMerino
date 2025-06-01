using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

// Manages checkpoint system for racing track
// Tracks multiple cars/agents and their progress through checkpoints
public class TrackCheckpoints : MonoBehaviour
{
    // List of cars and agents being tracked
    [SerializeField] public List<Transform> carTransformList;

    // List of all checkpoints in the track
    public List<CheckpointSingle> checkpointSingleList;
    // Tracks the next checkpoint index for each car
    private List<int> nextCheckpointSingleIndexList;

    // Events triggered when cars pass checkpoints
    public event EventHandler<CarCheckPointEventArgs> OnCarWrongCheckpoint;   // Wrong checkpoint passed
    public event EventHandler<CarCheckPointEventArgs> OnCarCorrectCheckpoint; // Correct checkpoint passed

    // Initialize checkpoint system and find all checkpoints
    private void Awake()
    {
        // Find the checkpoints container
        Transform checkpointsTransform = transform.Find("Checkpoints");

        if (checkpointsTransform == null)
        {
            // Try finding a child named "Checkpoints"
            foreach (Transform child in transform)
            {
                if (child.name == "Checkpoints")
                {
                    checkpointsTransform = child;
                    break;
                }
            }
            
            if (checkpointsTransform == null)
            {
                Debug.LogWarning("No 'Checkpoints' child found. Using this GameObject as parent.");
                checkpointsTransform = transform;
            }
        }

        // Initialize checkpoint list and connect all checkpoints
        checkpointSingleList = new List<CheckpointSingle>();

        foreach (Transform chekpointSingleTransform in checkpointsTransform)
        {
            CheckpointSingle checkpointSingle = chekpointSingleTransform.GetComponent<CheckpointSingle>();
            
            if (checkpointSingle == null)
            {
                // Skip if this child doesn't have a CheckpointSingle component
                continue;
            }

            checkpointSingle.SetTrackCheckpoints(this);
            checkpointSingleList.Add(checkpointSingle);
        }
        
        // Auto-detect cars if the car list is empty
        if (carTransformList == null || carTransformList.Count == 0)
        {
            FindCarTransforms();
        }
        
        // Initialize checkpoint tracking for each car
        nextCheckpointSingleIndexList = new List<int>();
        foreach (Transform carTransform in carTransformList)
        {
            nextCheckpointSingleIndexList.Add(0);
        }
    }

    // Called when a car passes through a checkpoint
    // Handles checkpoint validation and progress tracking
    public void CarThroughCheckpoint(CheckpointSingle checkpointSingle, Transform carTransform)
    {
        // Make sure the car is in our list
        int carIndex = carTransformList.IndexOf(carTransform);
        if (carIndex == -1)
        {
            // Car isn't in the list yet, add it
            carTransformList.Add(carTransform);
            nextCheckpointSingleIndexList.Add(0);
            carIndex = carTransformList.Count - 1;
            
            Debug.Log($"Added new car to tracking: {carTransform.name}");
        }
        
        // Check if this is the correct next checkpoint
        int nextCheckpointSingleIndex = nextCheckpointSingleIndexList[carIndex];
        if (checkpointSingleList.IndexOf(checkpointSingle) == nextCheckpointSingleIndex)
        {
            Debug.Log("Correct checkpoint passed");
            // Move to next checkpoint (loop back to start if at end)
            nextCheckpointSingleIndexList[carIndex] = (nextCheckpointSingleIndex + 1) % checkpointSingleList.Count;
            OnCarCorrectCheckpoint?.Invoke(this, new CarCheckPointEventArgs { carTransform = carTransform, checkpointSingle = checkpointSingle });
        }
        else
        {
            // Wrong checkpoint passed
            OnCarWrongCheckpoint?.Invoke(this, new CarCheckPointEventArgs { carTransform = carTransform });
        }
    }

    // Event arguments for checkpoint events
    public class CarCheckPointEventArgs : EventArgs
    {
        public Transform carTransform { get; set; }      // The car that triggered the event
        public CheckpointSingle checkpointSingle { get; set; }  // The checkpoint involved (if correct)
    }

    // Get the next checkpoint a car should reach
    public CheckpointSingle GetNextCheckpointPosition(Transform carTransform)
    {
        int carIndex = carTransformList.IndexOf(carTransform);
        if (carIndex == -1)
        {
            Debug.LogWarning($"Car {carTransform.name} not found in carTransformList");
            return checkpointSingleList[0];
        }
        
        int nextCheckpointSingleIndex = nextCheckpointSingleIndexList[carIndex];
        return checkpointSingleList[nextCheckpointSingleIndex];
    }

    // Reset a car's checkpoint progress to the start
    public void ResetCheckpoint(Transform carTransform)
    {
        int carIndex = carTransformList.IndexOf(carTransform);
        if (carIndex != -1)
        {
            nextCheckpointSingleIndexList[carIndex] = 0;
        }
    }
    
    // Find all cars and AI agents in the scene
    public void FindCarTransforms()
    {
        if (carTransformList == null)
        {
            carTransformList = new List<Transform>();
        }
        else
        {
            carTransformList.Clear();
        }
        
        // Find all player cars
        CarController[] cars = FindObjectsOfType<CarController>();
        foreach (CarController car in cars)
        {
            carTransformList.Add(car.transform);
        }
        
        // Find all AI agents
        NNCheck[] agents = FindObjectsOfType<NNCheck>();
        foreach (NNCheck agent in agents)
        {
            // Avoid adding the same transform twice if an object has both components
            if (!carTransformList.Contains(agent.transform))
            {
                carTransformList.Add(agent.transform);
            }
        }
        
        Debug.Log($"Found {carTransformList.Count} tracked object(s) (Players and Agents).");
    }
    
    // Editor helper method to reset and find cars
    // Used for manual refresh of car list and checkpoint indices
    public void ResetAndFindCars()
    {
        if (carTransformList == null)
        {
            carTransformList = new List<Transform>();
        }
        else
        {
            carTransformList.Clear();
        }
        
        // Find all player cars
        CarController[] cars = FindObjectsOfType<CarController>();
        foreach (CarController car in cars)
        {
            carTransformList.Add(car.transform);
        }
        
        // Find all AI agents
        NNCheck[] agents = FindObjectsOfType<NNCheck>();
        foreach (NNCheck agent in agents)
        {
             if (!carTransformList.Contains(agent.transform))
            {
                carTransformList.Add(agent.transform);
            }
        }
        
        // Reset all checkpoint indices to start
        nextCheckpointSingleIndexList = new List<int>();
        foreach (Transform carTransform in carTransformList)
        {
            nextCheckpointSingleIndexList.Add(0);
        }
    }
}