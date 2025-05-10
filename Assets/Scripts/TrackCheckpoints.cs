using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class TrackCheckpoints : MonoBehaviour
{
    [SerializeField] private List<Transform> carTransformList;
    [SerializeField] private float checkpointSpacing = 10f; // Distancia base entre checkpoints
    [SerializeField] private float curveCheckpointMultiplier = 2f; // Multiplicador de densidad en curvas
    [SerializeField] private float curveDetectionAngle = 30f; // Ángulo para detectar curvas
    [SerializeField] private float checkpointHeight = 1f; // Altura de los checkpoints sobre el suelo

    private List<CheckpointSingle> checkpointSingleList;
    private List<int> nextCheckpointSingleIndexList;

    public event EventHandler<CarCheckPointEventArgs> OnCarCorrectCheckpoint;
    public event EventHandler<CarCheckPointEventArgs> OnCarWrongCheckpoint;

    private void Awake()
    {
        GenerateCheckpoints();
        InitializeCheckpointLists();
    }

    private void GenerateCheckpoints()
    {
        // Crear el contenedor de checkpoints si no existe
        Transform checkpointsTransform = transform.Find("Checkpoints");
        if (checkpointsTransform == null)
        {
            GameObject checkpointsContainer = new GameObject("Checkpoints");
            checkpointsContainer.transform.SetParent(transform);
            checkpointsTransform = checkpointsContainer.transform;
        }

        // Obtener el mesh del circuito
        MeshFilter circuitMesh = GetComponentInChildren<MeshFilter>();
        if (circuitMesh == null)
        {
            Debug.LogError("No se encontró el mesh del circuito!");
            return;
        }

        // Obtener los vértices del mesh
        Vector3[] vertices = circuitMesh.mesh.vertices;
        List<Vector3> checkpointPositions = new List<Vector3>();

        // Convertir vértices a posiciones del mundo
        for (int i = 0; i < vertices.Length; i++)
        {
            vertices[i] = circuitMesh.transform.TransformPoint(vertices[i]);
        }

        // Generar puntos a lo largo del circuito
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 currentPoint = vertices[i];
            Vector3 nextPoint = vertices[(i + 1) % vertices.Length];
            Vector3 direction = (nextPoint - currentPoint).normalized;
            float distance = Vector3.Distance(currentPoint, nextPoint);

            // Determinar si estamos en una curva
            Vector3 nextNextPoint = vertices[(i + 2) % vertices.Length];
            float angle = Vector3.Angle(direction, (nextNextPoint - nextPoint).normalized);
            bool isCurve = angle > curveDetectionAngle;

            // Calcular el número de checkpoints para este segmento
            float segmentSpacing = isCurve ? checkpointSpacing / curveCheckpointMultiplier : checkpointSpacing;
            int numCheckpoints = Mathf.CeilToInt(distance / segmentSpacing);

            // Generar checkpoints para este segmento
            for (int j = 0; j < numCheckpoints; j++)
            {
                float t = j / (float)numCheckpoints;
                Vector3 position = Vector3.Lerp(currentPoint, nextPoint, t);
                position.y += checkpointHeight; // Elevar el checkpoint sobre el suelo
                checkpointPositions.Add(position);
            }
        }

        // Crear los checkpoints
        checkpointSingleList = new List<CheckpointSingle>();
        foreach (Vector3 position in checkpointPositions)
        {
            GameObject checkpointObj = new GameObject("Checkpoint");
            checkpointObj.transform.SetParent(checkpointsTransform);
            checkpointObj.transform.position = position;

            // Añadir componentes necesarios
            BoxCollider collider = checkpointObj.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.size = new Vector3(5f, 5f, 5f); // Tamaño del trigger

            CheckpointSingle checkpoint = checkpointObj.AddComponent<CheckpointSingle>();
            checkpoint.SetTrackCheckpoints(this);
            checkpointSingleList.Add(checkpoint);
        }
    }

    private void InitializeCheckpointLists()
    {
        nextCheckpointSingleIndexList = new List<int>();
        foreach(Transform carTransform in carTransformList)
        {
            nextCheckpointSingleIndexList.Add(0);
        }
    }

    public void CarThroughCheckpoint(CheckpointSingle checkpointSingle, Transform carTransform)
    {
        int nextCheckpointSingleIndex = nextCheckpointSingleIndexList[carTransformList.IndexOf(carTransform)];
        if (checkpointSingleList.IndexOf(checkpointSingle) == nextCheckpointSingleIndex)
        {
            Debug.Log("Correcto");
            nextCheckpointSingleIndexList[carTransformList.IndexOf(carTransform)]
                = (nextCheckpointSingleIndex + 1) % checkpointSingleList.Count;
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
        int nextCheckpointSingleIndex = nextCheckpointSingleIndexList[carTransformList.IndexOf(carTransform)];
        return checkpointSingleList[nextCheckpointSingleIndex];
    }

    public void ResetCheckpoint(Transform carTransform)
    {
        nextCheckpointSingleIndexList[carTransformList.IndexOf(carTransform)] = 0;
    }

}
