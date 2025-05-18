using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class TrackCheckpoints : MonoBehaviour
{

    [SerializeField] private List<Transform> carTransformList;

    private List<CheckpointSingle> checkpointSingleList;
    private List<int> nextCheckpointSingleIndexList;

    public event EventHandler<CarCheckPointEventArgs> OnCarWrongCheckpoint;
    public event EventHandler<CarCheckPointEventArgs> OnCarCorrectCheckpoint;
    private void Awake()
    {
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
        
        // Initialize the next checkpoint index list
        nextCheckpointSingleIndexList = new List<int>();
        foreach (Transform carTransform in carTransformList)
        {
            nextCheckpointSingleIndexList.Add(0);
        }
    }

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
        
        int nextCheckpointSingleIndex = nextCheckpointSingleIndexList[carIndex];
        if (checkpointSingleList.IndexOf(checkpointSingle) == nextCheckpointSingleIndex)
        {
            Debug.Log("Correcto");
            nextCheckpointSingleIndexList[carIndex] = (nextCheckpointSingleIndex + 1) % checkpointSingleList.Count;
            OnCarCorrectCheckpoint?.Invoke(this, new CarCheckPointEventArgs { carTransform = carTransform });
        }
        else
        {
            OnCarWrongCheckpoint?.Invoke(this, new CarCheckPointEventArgs { carTransform = carTransform });
        }
    }

    public class CarCheckPointEventArgs : EventArgs
    {
        public Transform carTransform { get; set; }
    }

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

    public void ResetCheckpoint(Transform carTransform)
    {
        int carIndex = carTransformList.IndexOf(carTransform);
        if (carIndex != -1)
        {
            nextCheckpointSingleIndexList[carIndex] = 0;
        }
    }
    
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
        
        // Find all objects with CarController component
        CarController[] cars = FindObjectsOfType<CarController>();
        foreach (CarController car in cars)
        {
            carTransformList.Add(car.transform);
        }
        
        Debug.Log($"Found {carTransformList.Count} car(s) with CarController component.");
    }
    
    // Editor helper method to reset and find cars
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
        
        FindCarTransforms();
        
        // Reset checkpoint indices
        nextCheckpointSingleIndexList = new List<int>();
        foreach (Transform carTransform in carTransformList)
        {
            nextCheckpointSingleIndexList.Add(0);
        }
    }
}