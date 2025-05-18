using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class CheckpointCreatorWindow : EditorWindow
{
    private GameObject trackObject;
    private GameObject checkpointPrefab;
    private int numberOfCheckpoints = 10;
    private float checkpointHeight = 1.5f;
    private Vector3 checkpointScale = new Vector3(5f, 3f, 0.5f);
    private bool isClosedTrack = true;
    private LayerMask trackLayer = 1; // Default to "Default" layer
    
    // Nuevas opciones de orientación
    private bool orientacionPerpendicular = true;
    private Vector3 rotacionAdicional = Vector3.zero;
    private bool evitarSuperposicion = true;
    private float distanciaMinima = 5f;
    
    // Opciones de colocación inteligente
    private bool priorizarCurvas = true;
    private bool checkpointEnCadaCurva = false;
    private float escalaEspecialEnCurvas = 1.2f;
    
    private GameObject createdCheckpointManager;
    
    [MenuItem("Tools/Racing/Checkpoint Creator")]
    public static void ShowWindow()
    {
        GetWindow<CheckpointCreatorWindow>("Checkpoint Creator");
    }
    
    private void OnGUI()
    {
        GUILayout.Label("Checkpoint Creator", EditorStyles.boldLabel);
        EditorGUILayout.Space();
        
        EditorGUILayout.LabelField("Track Settings", EditorStyles.boldLabel);
        
        trackObject = EditorGUILayout.ObjectField("Track Mesh", trackObject, typeof(GameObject), true) as GameObject;
        
        trackLayer = EditorGUILayout.LayerField("Track Layer", trackLayer);
        isClosedTrack = EditorGUILayout.Toggle("Is Closed Track", isClosedTrack);
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Checkpoint Settings", EditorStyles.boldLabel);
        
        checkpointPrefab = EditorGUILayout.ObjectField("Checkpoint Prefab", checkpointPrefab, typeof(GameObject), false) as GameObject;
        numberOfCheckpoints = EditorGUILayout.IntSlider("Number of Checkpoints", numberOfCheckpoints, 3, 50);
        checkpointHeight = EditorGUILayout.FloatField("Checkpoint Height", checkpointHeight);
        checkpointScale = EditorGUILayout.Vector3Field("Checkpoint Scale", checkpointScale);
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Orientación de Checkpoints", EditorStyles.boldLabel);
        
        orientacionPerpendicular = EditorGUILayout.Toggle("Perpendicular a Pista", orientacionPerpendicular);
        if (!orientacionPerpendicular)
        {
            EditorGUILayout.HelpBox("Los checkpoints se orientarán en la dirección de carrera", MessageType.Info);
        }
        
        rotacionAdicional = EditorGUILayout.Vector3Field("Rotación Adicional (Euler)", rotacionAdicional);
        
        EditorGUILayout.Space();
        evitarSuperposicion = EditorGUILayout.Toggle("Evitar Superposición", evitarSuperposicion);
        if (evitarSuperposicion)
        {
            distanciaMinima = EditorGUILayout.Slider("Distancia Mínima", distanciaMinima, 1f, 20f);
        }
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Colocación Inteligente", EditorStyles.boldLabel);
        
        priorizarCurvas = EditorGUILayout.Toggle("Priorizar Curvas", priorizarCurvas);
        
        using (new EditorGUI.DisabledScope(!priorizarCurvas))
        {
            checkpointEnCadaCurva = EditorGUILayout.Toggle("Checkpoint en Cada Curva", checkpointEnCadaCurva);
            
            if (checkpointEnCadaCurva)
            {
                EditorGUILayout.HelpBox("Se colocará un checkpoint en cada curva detectada, además de checkpoints en tramos rectos según sea necesario", MessageType.Info);
            }
            
            escalaEspecialEnCurvas = EditorGUILayout.Slider("Escala en Curvas", escalaEspecialEnCurvas, 0.5f, 2f);
        }
        
        EditorGUILayout.Space();
        
        EditorGUI.BeginDisabledGroup(trackObject == null || checkpointPrefab == null);
        
        if (GUILayout.Button("Create Checkpoint System", GUILayout.Height(30)))
        {
            CreateCheckpointSystem();
        }
        
        EditorGUI.EndDisabledGroup();
        
        if (createdCheckpointManager != null)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Last Created Object", EditorStyles.boldLabel);
            EditorGUILayout.ObjectField("Checkpoint Manager", createdCheckpointManager, typeof(GameObject), true);
            
            if (GUILayout.Button("Select Created Object"))
            {
                Selection.activeGameObject = createdCheckpointManager;
            }
        }
    }
    
    private void CreateCheckpointSystem()
    {
        if (trackObject == null || checkpointPrefab == null)
        {
            Debug.LogError("Track and Checkpoint Prefab must be assigned!");
            return;
        }
        
        // Create a parent GameObject for the checkpoint system
        GameObject checkpointManager = new GameObject(trackObject.name + "_CheckpointSystem");
        Undo.RegisterCreatedObjectUndo(checkpointManager, "Create Checkpoint System");
        
        // Position the manager at the track's position
        checkpointManager.transform.position = trackObject.transform.position;
        
        // Add the necessary components
        TrackAnalyzer trackAnalyzer = checkpointManager.AddComponent<TrackAnalyzer>();
        CheckpointCreator checkpointCreator = checkpointManager.AddComponent<CheckpointCreator>();
        
        // Configure TrackAnalyzer
        SerializedObject serializedAnalyzer = new SerializedObject(trackAnalyzer);
        serializedAnalyzer.FindProperty("trackMesh").objectReferenceValue = trackObject.transform;
        serializedAnalyzer.FindProperty("trackLayer").intValue = trackLayer.value;
        serializedAnalyzer.FindProperty("closedTrack").boolValue = isClosedTrack;
        serializedAnalyzer.FindProperty("showExtendedDebug").boolValue = true;
        serializedAnalyzer.FindProperty("autoDetectTrackLayer").boolValue = true;
        serializedAnalyzer.FindProperty("crearObjetosDebug").boolValue = false;
        
        // Configuración de análisis de curvas
        var curvatureThresholdProp = serializedAnalyzer.FindProperty("curvatureThreshold");
        if (curvatureThresholdProp != null)
        {
            curvatureThresholdProp.floatValue = 0.2f; // Umbral para detectar curvas (ajustable)
        }
        
        var centrarEnCurvasProp = serializedAnalyzer.FindProperty("centrarEnCurvas");
        if (centrarEnCurvasProp != null)
        {
            centrarEnCurvasProp.boolValue = true;
        }
        
        serializedAnalyzer.ApplyModifiedProperties();
        
        // Configure CheckpointCreator
        SerializedObject serializedCreator = new SerializedObject(checkpointCreator);
        serializedCreator.FindProperty("checkpointPrefab").objectReferenceValue = checkpointPrefab;
        serializedCreator.FindProperty("trackMesh").objectReferenceValue = trackObject.transform;
        serializedCreator.FindProperty("numberOfCheckpoints").intValue = numberOfCheckpoints;
        serializedCreator.FindProperty("checkpointHeight").floatValue = checkpointHeight;
        serializedCreator.FindProperty("checkpointScale").vector3Value = checkpointScale;
        serializedCreator.FindProperty("trackLayer").intValue = trackLayer.value;
        
        // Nuevas opciones
        serializedCreator.FindProperty("orientacionPerpendicular").boolValue = orientacionPerpendicular;
        serializedCreator.FindProperty("rotacionAdicional").vector3Value = rotacionAdicional;
        serializedCreator.FindProperty("evitarSuperposicionDeCheckpoints").boolValue = evitarSuperposicion;
        serializedCreator.FindProperty("distanciaMinimaEntreCheckpoints").floatValue = distanciaMinima;
        
        // Opciones de colocación inteligente
        serializedCreator.FindProperty("priorizar_curvas").boolValue = priorizarCurvas;
        serializedCreator.FindProperty("checkpoint_en_cada_curva").boolValue = checkpointEnCadaCurva;
        serializedCreator.FindProperty("escala_especial_en_curvas").floatValue = escalaEspecialEnCurvas;
        
        serializedCreator.FindProperty("useExistingAnalyzer").boolValue = true;
        serializedCreator.FindProperty("existingAnalyzer").objectReferenceValue = trackAnalyzer;
        serializedCreator.ApplyModifiedProperties();
        
        // Store reference to created object
        createdCheckpointManager = checkpointManager;
        
        // Analyze track first
        trackAnalyzer.AnalyzeTrackWithLayerDetection();
        
        // Generate checkpoints
        checkpointCreator.GenerateCheckpoints();
        
        // Focus on the created object
        Selection.activeGameObject = checkpointManager;
        SceneView.FrameLastActiveSceneView();
        
        Debug.Log("Checkpoint system created successfully!");
    }
} 