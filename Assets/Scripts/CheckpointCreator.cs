using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class CheckpointCreator : MonoBehaviour
{
    [Header("Checkpoint Settings")]
    [SerializeField] private GameObject checkpointPrefab;
    [SerializeField] private Transform trackMesh;
    [SerializeField] private int numberOfCheckpoints = 10;
    [SerializeField] private float checkpointHeight = 1.5f;
    [SerializeField] private Vector3 checkpointScale = new Vector3(5f, 3f, 0.5f);
    [SerializeField] private float trackOffset = 0f; // Distance from track center
    
    [Header("Orientación de Checkpoints")]
    [SerializeField] private bool orientacionPerpendicular = true;
    [SerializeField] private Vector3 rotacionAdicional = Vector3.zero;
    [SerializeField] private bool evitarSuperposicionDeCheckpoints = true;
    [SerializeField] private float distanciaMinimaEntreCheckpoints = 5f;
    
    [Header("Colocación Inteligente")]
    [SerializeField] private bool priorizar_curvas = true;
    [SerializeField] private bool checkpoint_en_cada_curva = false;
    [SerializeField] private float escala_especial_en_curvas = 1.2f;
    
    [Header("Track Analysis")]
    [SerializeField] private LayerMask trackLayer;
    [SerializeField] private Transform startPosition;
    [SerializeField] private bool useExistingAnalyzer = false;
    [SerializeField] private TrackAnalyzer existingAnalyzer;
    
    [Header("Generated Objects")]
    [SerializeField] private Transform checkpointsParent;

    private TrackAnalyzer trackAnalyzer;
    
    private void OnValidate()
    {
        if (useExistingAnalyzer && existingAnalyzer == null)
        {
            existingAnalyzer = GetComponent<TrackAnalyzer>();
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Generate Checkpoints")]
    public void GenerateCheckpoints()
    {
        if (checkpointPrefab == null)
        {
            Debug.LogError("Checkpoint prefab is not assigned!");
            return;
        }

        if (trackMesh == null)
        {
            Debug.LogError("Track mesh is not assigned!");
            return;
        }

        // Create checkpoints parent if it doesn't exist
        if (checkpointsParent == null)
        {
            GameObject checkpointsParentObj = new GameObject("Checkpoints");
            checkpointsParentObj.transform.SetParent(transform);
            checkpointsParent = checkpointsParentObj.transform;
        }
        else
        {
            // Clear existing checkpoints
            while (checkpointsParent.childCount > 0)
            {
                DestroyImmediate(checkpointsParent.GetChild(0).gameObject);
            }
        }

        // Get or create track analyzer
        if (useExistingAnalyzer && existingAnalyzer != null)
        {
            trackAnalyzer = existingAnalyzer;
        }
        else
        {
            // Create a new analyzer
            if (trackAnalyzer == null)
            {
                // Check if this GameObject already has a TrackAnalyzer
                trackAnalyzer = GetComponent<TrackAnalyzer>();
                
                if (trackAnalyzer == null)
                {
                    // Create a new TrackAnalyzer component
                    trackAnalyzer = gameObject.AddComponent<TrackAnalyzer>();
                }
            }
            
            // Configure the analyzer
            SerializedObject serializedAnalyzer = new SerializedObject(trackAnalyzer);
            
            var trackMeshProp = serializedAnalyzer.FindProperty("trackMesh");
            trackMeshProp.objectReferenceValue = trackMesh;
            
            var trackLayerProp = serializedAnalyzer.FindProperty("trackLayer");
            trackLayerProp.intValue = trackLayer.value;
            
            var startProp = serializedAnalyzer.FindProperty("startPosition");
            startProp.objectReferenceValue = startPosition;
            
            // Configurar que no se creen objetos de debug
            var crearObjetosDebugProp = serializedAnalyzer.FindProperty("crearObjetosDebug");
            if (crearObjetosDebugProp != null)
            {
                crearObjetosDebugProp.boolValue = false;
            }
            
            serializedAnalyzer.ApplyModifiedProperties();
            
            // Analyze the track
            trackAnalyzer.AnalyzeTrackWithLayerDetection();
        }
        
        // Get track points from analyzer
        List<TrackAnalyzer.TrackPoint> allTrackPoints = trackAnalyzer.GetTrackPoints();
        
        if (allTrackPoints == null || allTrackPoints.Count < 2)
        {
            Debug.LogError("Track analysis failed! No track points found.");
            return;
        }
        
        // Create checkpoints based on track points
        if (priorizar_curvas && checkpoint_en_cada_curva)
        {
            CreateCheckpointsWithPriorityOnCurves(allTrackPoints);
        }
        else
        {
            CreateCheckpointsFromTrackPoints(allTrackPoints);
        }
        
        // Setup TrackCheckpoints component
        SetupTrackCheckpoints();
    }

    // Nuevo método que coloca checkpoints priorizando curvas
    private void CreateCheckpointsWithPriorityOnCurves(List<TrackAnalyzer.TrackPoint> trackPoints)
    {
        // Primero identificar todas las curvas
        List<int> curveIndices = new List<int>();
        
        for (int i = 0; i < trackPoints.Count; i++)
        {
            if (trackPoints[i].isCurve)
            {
                curveIndices.Add(i);
            }
        }
        
        Debug.Log($"Detected {curveIndices.Count} curves in the track");
        
        // Lista para almacenar las posiciones de los checkpoints creados
        List<Vector3> checkpointPositions = new List<Vector3>();
        
        // Si no hay suficientes curvas, mezclamos con puntos normales
        int totalCheckpointsNeeded = numberOfCheckpoints;
        int checkpointsFromCurves = Mathf.Min(curveIndices.Count, totalCheckpointsNeeded);
        int remainingCheckpoints = totalCheckpointsNeeded - checkpointsFromCurves;
        
        // Espaciado uniforme para seleccionar curvas si hay demasiadas
        List<int> selectedCurveIndices = new List<int>();
        if (curveIndices.Count > 0)
        {
            float step = (float)curveIndices.Count / checkpointsFromCurves;
            for (int i = 0; i < checkpointsFromCurves; i++)
            {
                int index = Mathf.Min(Mathf.FloorToInt(i * step), curveIndices.Count - 1);
                selectedCurveIndices.Add(curveIndices[index]);
            }
        }
        
        // Crear checkpoints en curvas
        foreach (int curveIndex in selectedCurveIndices)
        {
            CreateCheckpointAtPoint(trackPoints[curveIndex], checkpointPositions, true);
        }
        
        // Si necesitamos más checkpoints, distribuir uniformemente los restantes
        if (remainingCheckpoints > 0)
        {
            int stride = Mathf.Max(1, trackPoints.Count / remainingCheckpoints);
            
            // Para equilibrar, comenzar en un punto intermedio
            int offset = stride / 2;
            
            for (int i = offset; i < trackPoints.Count && checkpointsParent.childCount < totalCheckpointsNeeded; i += stride)
            {
                // Verificar que este punto no esté demasiado cerca de un checkpoint existente
                bool tooClose = false;
                foreach (int curveIdx in selectedCurveIndices)
                {
                    if (Mathf.Abs(i - curveIdx) < stride / 2)
                    {
                        tooClose = true;
                        break;
                    }
                }
                
                if (!tooClose)
                {
                    CreateCheckpointAtPoint(trackPoints[i], checkpointPositions, false);
                }
            }
        }
        
        Debug.Log($"Created {checkpointsParent.childCount} checkpoints (including {selectedCurveIndices.Count} on curves)");
    }

    private void CreateCheckpointsFromTrackPoints(List<TrackAnalyzer.TrackPoint> trackPoints)
    {
        int totalPoints = trackPoints.Count;
        
        // Calculate how many points to skip between checkpoints to get approximately the desired number
        int pointsToSkip = Mathf.Max(1, totalPoints / numberOfCheckpoints);
        
        // Lista para almacenar las posiciones de los checkpoints creados
        List<Vector3> checkpointPositions = new List<Vector3>();
        
        // Contador para saber cuántos checkpoints hemos creado
        int checkpointsCreated = 0;
        
        // Create checkpoints at regular intervals along the track
        for (int i = 0; i < totalPoints; i += pointsToSkip)
        {
            if (checkpointsCreated >= numberOfCheckpoints)
                break;
                
            TrackAnalyzer.TrackPoint trackPoint = trackPoints[i];
            
            // Si estamos cerca del final y queremos evitar superposición, comprobar distancia al primer checkpoint
            if (evitarSuperposicionDeCheckpoints && checkpointPositions.Count > 0 && i > totalPoints * 0.75f)
            {
                float distanciaAlInicio = Vector3.Distance(trackPoint.position, checkpointPositions[0]);
                if (distanciaAlInicio < distanciaMinimaEntreCheckpoints)
                {
                    Debug.Log($"Evitando crear checkpoint final cerca del inicio (distancia: {distanciaAlInicio})");
                    continue;
                }
            }
            
            if (CreateCheckpointAtPoint(trackPoint, checkpointPositions, trackPoint.isCurve))
            {
                checkpointsCreated++;
            }
        }
        
        Debug.Log($"Created {checkpointsParent.childCount} checkpoints.");
    }
    
    // Método auxiliar para crear un checkpoint en un punto específico
    private bool CreateCheckpointAtPoint(TrackAnalyzer.TrackPoint trackPoint, List<Vector3> checkpointPositions, bool isOnCurve)
    {
        // Create checkpoint GameObject
        GameObject checkpoint = Instantiate(checkpointPrefab, checkpointsParent);
        checkpoint.name = "Checkpoint_" + checkpointsParent.childCount;
        
        // Apply position, adjusted for height
        Vector3 checkpointPos = trackPoint.position + Vector3.up * checkpointHeight;
        
        // Si estamos en una curva y evitamos superposición, verificar distancia a checkpoints existentes
        if (evitarSuperposicionDeCheckpoints && checkpointPositions.Count > 0)
        {
            foreach (Vector3 existingPos in checkpointPositions)
            {
                float distancia = Vector3.Distance(checkpointPos, existingPos);
                if (distancia < distanciaMinimaEntreCheckpoints)
                {
                    // Demasiado cerca, destruir y retornar false
                    DestroyImmediate(checkpoint);
                    return false;
                }
            }
        }
        
        // Si pasó la verificación, continuar con la creación
        checkpoint.transform.position = checkpointPos;
        checkpointPositions.Add(checkpointPos);
        
        // Apply rotation to face direction of track
        if (trackPoint.direction != Vector3.zero)
        {
            Vector3 forward;
            
            if (orientacionPerpendicular)
            {
                // Perpendicular a la dirección de la pista (forma un portal que cruza la pista)
                forward = Vector3.Cross(trackPoint.direction, Vector3.up).normalized;
            }
            else
            {
                // Alineado con la dirección de la pista (mirando hacia adelante en la dirección de carrera)
                forward = trackPoint.direction;
            }
            
            // Si queremos el checkpoint con un offset lateral:
            if (trackOffset != 0)
            {
                Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
                checkpointPos += right * trackOffset;
                checkpoint.transform.position = checkpointPos;
            }
            
            // Aplicar rotación
            Quaternion baseRotation = Quaternion.LookRotation(forward);
            
            // Aplicar rotación adicional si está configurada
            if (rotacionAdicional != Vector3.zero)
            {
                baseRotation *= Quaternion.Euler(rotacionAdicional);
            }
            
            checkpoint.transform.rotation = baseRotation;
        }
        
        // Apply scale, potentially adjusted for track width and curve factor
        Vector3 scale = checkpointScale;
        
        // Si está en una curva y tenemos escala especial, ajustar
        if (isOnCurve && escala_especial_en_curvas != 1.0f)
        {
            scale *= escala_especial_en_curvas;
        }
        else
        {
            // Para puntos normales, ajustar según el ancho de la pista
            scale.y = Mathf.Min(scale.y, trackPoint.width * 0.8f);
        }
        
        checkpoint.transform.localScale = scale;
        
        // Ensure it has the CheckpointSingle component
        if (checkpoint.GetComponent<CheckpointSingle>() == null)
        {
            checkpoint.AddComponent<CheckpointSingle>();
        }
        
        return true;
    }

    private void SetupTrackCheckpoints()
    {
        // Get or add TrackCheckpoints component
        TrackCheckpoints trackCheckpoints = GetComponent<TrackCheckpoints>();
        if (trackCheckpoints == null)
        {
            trackCheckpoints = gameObject.AddComponent<TrackCheckpoints>();
        }
        
        // Connect all checkpoints to the TrackCheckpoints component
        List<Transform> carTransforms = new List<Transform>();
        if (trackCheckpoints != null)
        {
            // Use reflection to get the carTransformList field from TrackCheckpoints
            System.Reflection.FieldInfo carTransformListField = typeof(TrackCheckpoints).GetField("carTransformList", 
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            
            if (carTransformListField != null)
            {
                // Get the existing car transforms list
                var existingList = carTransformListField.GetValue(trackCheckpoints) as List<Transform>;
                if (existingList != null)
                {
                    carTransforms = new List<Transform>(existingList);
                }
            }
        }
        
        // Update TrackCheckpoints component via SerializedObject
        SerializedObject serializedTrackCheckpoints = new SerializedObject(trackCheckpoints);
        var carTransformListProp = serializedTrackCheckpoints.FindProperty("carTransformList");
        
        // Preserve existing car references
        if (carTransformListProp.arraySize != carTransforms.Count)
        {
            carTransformListProp.arraySize = carTransforms.Count;
            for (int i = 0; i < carTransforms.Count; i++)
            {
                carTransformListProp.GetArrayElementAtIndex(i).objectReferenceValue = carTransforms[i];
            }
        }
        
        serializedTrackCheckpoints.ApplyModifiedProperties();
        
        // Connect all checkpoints to the TrackCheckpoints component
        foreach (Transform checkpoint in checkpointsParent)
        {
            CheckpointSingle checkpointSingle = checkpoint.GetComponent<CheckpointSingle>();
            if (checkpointSingle != null)
            {
                checkpointSingle.SetTrackCheckpoints(trackCheckpoints);
            }
        }
        
        Debug.Log("Successfully set up TrackCheckpoints controller.");
    }
    
    // Visualization in the editor
    private void OnDrawGizmos()
    {
        if (checkpointsParent != null && checkpointsParent.childCount > 0)
        {
            Gizmos.color = Color.green;
            
            // Draw lines connecting checkpoints
            for (int i = 0; i < checkpointsParent.childCount; i++)
            {
                Transform current = checkpointsParent.GetChild(i);
                Transform next = i < checkpointsParent.childCount - 1 ? 
                    checkpointsParent.GetChild(i + 1) : 
                    checkpointsParent.GetChild(0);
                
                Gizmos.DrawLine(current.position, next.position);
            }
        }
    }
#endif
}

#if UNITY_EDITOR
[CustomEditor(typeof(CheckpointCreator))]
public class CheckpointCreatorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        
        CheckpointCreator creator = (CheckpointCreator)target;
        
        EditorGUILayout.Space();
        if (GUILayout.Button("Generate Checkpoints", GUILayout.Height(30)))
        {
            creator.GenerateCheckpoints();
        }
    }
}
#endif 