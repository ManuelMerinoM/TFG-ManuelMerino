using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TrackAnalyzer : MonoBehaviour
{
    // Represents a point on the track with position and direction
    [System.Serializable]
    public class TrackPoint
    {
        public Vector3 position;
        public Vector3 direction;
        public float width;
        public bool isCurve;       // Nuevo: indica si este punto está en una curva
        public float curvature;    // Nuevo: cuánto se curva la pista en este punto (0 = recto, 1 = curva cerrada)

        public TrackPoint(Vector3 pos, Vector3 dir, float w, bool curve = false, float curv = 0f)
        {
            position = pos;
            direction = dir;
            width = w;
            isCurve = curve;
            curvature = curv;
        }
    }

    [Header("Track Settings")]
    [SerializeField] private Transform trackMesh;
    [SerializeField] private float sampleDistance = 5f;
    [SerializeField] private float raycastHeight = 10f;
    [SerializeField] private LayerMask trackLayer;
    [SerializeField] private bool closedTrack = true;
    [SerializeField] private Transform startPosition;

    [Header("Análisis de Curvas")]
    [SerializeField] private float curvatureThreshold = 0.2f; // Umbral para considerar un punto como curva
    [SerializeField] private bool centrarEnCurvas = true;     // Centrar puntos en el medio de las curvas

    [Header("Debug Options")]
    [SerializeField] private bool showDebugVisuals = true;
    [SerializeField] private bool showExtendedDebug = true;
    [SerializeField] private bool autoDetectTrackLayer = true;
    [SerializeField] private bool crearObjetosDebug = false;  // Nueva opción para crear/eliminar objetos de debug
    [SerializeField] private float gizmoSize = 0.5f;
    
    // Output: Racing line points
    private List<TrackPoint> trackPoints = new List<TrackPoint>();
    
    // Analysis results
    public List<TrackPoint> TrackPoints => trackPoints;
    public bool IsTrackClosed => closedTrack;
    
    // Referencias a objetos de debug para poder limpiarlos después
    private List<GameObject> debugObjects = new List<GameObject>();
    
    private void OnValidate()
    {
        if (trackMesh == null && transform.parent != null)
        {
            // Try to find track mesh in parent
            trackMesh = transform.parent;
        }
    }
    
    public void AnalyzeTrackWithLayerDetection()
    {
        if (autoDetectTrackLayer && trackMesh != null)
        {
            // Intentar detectar el mesh del suelo si trackMesh es un padre
            Transform detectedMesh = FindActualTrackMesh(trackMesh);
            if (detectedMesh != null)
            {
                // Usar la capa del mesh detectado
                int detectedLayer = detectedMesh.gameObject.layer;
                LayerMask newLayerMask = 1 << detectedLayer;
                
                Debug.Log($"¡Capa de pista detectada automáticamente! Usando capa: {LayerMask.LayerToName(detectedLayer)} ({detectedLayer})");
                Debug.Log($"Objeto detectado: {detectedMesh.name}");
                
                // Guardar la capa anterior para poder compararla
                LayerMask previousLayerMask = trackLayer;
                trackLayer = newLayerMask;
                
                if (previousLayerMask != newLayerMask)
                {
                    Debug.Log($"Capa cambiada de {LayerMaskToString(previousLayerMask)} a {LayerMaskToString(newLayerMask)}");
                }
            }
        }
        
        // Continuar con el análisis normal
        AnalyzeTrack();
    }
    
    // Método para encontrar el mesh real de la pista
    private Transform FindActualTrackMesh(Transform parent)
    {
        // Primero verificar si el objeto actual tiene un MeshRenderer o MeshFilter
        if (parent.GetComponent<MeshRenderer>() != null && parent.GetComponent<MeshFilter>() != null)
        {
            return parent;
        }
        
        // Si el objeto actual no tiene mesh, buscar en sus hijos
        foreach (Transform child in parent)
        {
            // Buscar primero en objetos con nombres como "suelo", "pista", "track", etc.
            string lowerName = child.name.ToLower();
            if (lowerName.Contains("suelo") || lowerName.Contains("pista") || 
                lowerName.Contains("track") || lowerName.Contains("floor") ||
                lowerName.Contains("ground"))
            {
                if (child.GetComponent<MeshRenderer>() != null && child.GetComponent<MeshFilter>() != null)
                {
                    return child;
                }
            }
        }
        
        // Si no encontramos por nombre, buscar cualquier hijo con mesh
        foreach (Transform child in parent)
        {
            if (child.GetComponent<MeshRenderer>() != null && child.GetComponent<MeshFilter>() != null)
            {
                return child;
            }
            
            // Buscar recursivamente
            Transform found = FindActualTrackMesh(child);
            if (found != null)
            {
                return found;
            }
        }
        
        return null;
    }
    
    // Limpiar objetos de debug creados anteriormente
    private void CleanupDebugObjects()
    {
        foreach (var obj in debugObjects)
        {
            if (obj != null)
            {
                DestroyImmediate(obj);
            }
        }
        
        debugObjects.Clear();
        
        // Buscar y eliminar objetos RayVisualizer y HitMarker que pudieran quedar
        GameObject[] visualizers = GameObject.FindGameObjectsWithTag("EditorOnly");
        foreach (var visualizer in visualizers)
        {
            if (visualizer.name.Contains("RayVisualizer") || visualizer.name.Contains("HitMarker"))
            {
                DestroyImmediate(visualizer);
            }
        }
        
        // Buscar por nombre
        var rayVisualizers = FindObjectsOfType<GameObject>();
        foreach (var obj in rayVisualizers)
        {
            if (obj.name == "RayVisualizer" || obj.name == "HitMarker")
            {
                DestroyImmediate(obj);
            }
        }
    }
    
    public void AnalyzeTrack()
    {
        // Limpiar objetos de debug anteriores
        CleanupDebugObjects();
        
        trackPoints.Clear();
        
        if (trackMesh == null)
        {
            Debug.LogError("Track mesh not assigned!");
            return;
        }
        
        if (startPosition == null)
        {
            Debug.LogWarning("Start position not set. Using current position.");
            // Create a temporary transform at the current position
            GameObject tempObj = new GameObject("TempStartPosition");
            startPosition = tempObj.transform;
            startPosition.position = transform.position;
            startPosition.rotation = transform.rotation;
        }
        
        // Intentar detectar la capa correcta si está activada la opción
        if (autoDetectTrackLayer)
        {
            // Crear un rayo que intente golpear cualquier cosa (todas las capas)
            Vector3 autoDetectRayStart = startPosition.position + Vector3.up * raycastHeight;
            RaycastHit autoDetectHit;
            
            if (Physics.Raycast(autoDetectRayStart, Vector3.down, out autoDetectHit, raycastHeight * 2))
            {
                Debug.Log($"¡Pista detectada! Objeto: {autoDetectHit.collider.gameObject.name}, Capa: {LayerMask.LayerToName(autoDetectHit.collider.gameObject.layer)}");
                
                // Usar la capa del objeto golpeado
                trackLayer = 1 << autoDetectHit.collider.gameObject.layer;
                Debug.Log($"LayerMask actualizada a: {LayerMaskToString(trackLayer)}");
                
                // Opcionalmente, podemos actualizar también el trackMesh
                if (autoDetectHit.collider.gameObject.transform != trackMesh)
                {
                    Debug.Log($"Actualizando trackMesh de {trackMesh.name} a {autoDetectHit.collider.gameObject.name}");
                    trackMesh = autoDetectHit.collider.gameObject.transform;
                }
            }
        }
        
        // Información detallada sobre cómo se está intentando el primer raycast
        Vector3 rayStart = startPosition.position + Vector3.up * raycastHeight;
        Vector3 rayDirection = Vector3.down;
        float rayDistance = raycastHeight * 2;
        
        if (showExtendedDebug)
        {
            Debug.Log($"Intentando primer raycast desde: {rayStart}, dirección: {rayDirection}, distancia: {rayDistance}");
            Debug.Log($"Layer mask: {LayerMaskToString(trackLayer)}, Layer de Track Mesh: {LayerMaskToString(1 << trackMesh.gameObject.layer)}");
            
            // Probar raycast con todas las capas para verificar
            RaycastHit testHit;
            if (Physics.Raycast(rayStart, rayDirection, out testHit, rayDistance))
            {
                Debug.Log($"Test raycast exitoso (todas las capas): golpeó {testHit.collider.gameObject.name} en capa {LayerMask.LayerToName(testHit.collider.gameObject.layer)}");
                
                // Si el raycast de prueba tuvo éxito pero usará una capa diferente, advertir
                if ((trackLayer & (1 << testHit.collider.gameObject.layer)) == 0)
                {
                    Debug.LogWarning($"¡ATENCIÓN! La pista fue detectada con raycast pero está en una capa ({LayerMask.LayerToName(testHit.collider.gameObject.layer)}) " +
                                    $"diferente a la configurada en Track Layer ({LayerMaskToString(trackLayer)}). Considera usar autodetección.");
                }
            }
            
            // Crear visualización de raycast solo si está activa la opción de objetos debug
            if (crearObjetosDebug)
            {
                GameObject rayVisualizer = new GameObject("RayVisualizer");
                rayVisualizer.tag = "EditorOnly";
                LineRenderer lr = rayVisualizer.AddComponent<LineRenderer>();
                lr.startWidth = 0.1f;
                lr.endWidth = 0.1f;
                lr.positionCount = 2;
                lr.SetPosition(0, rayStart);
                lr.SetPosition(1, rayStart + rayDirection * rayDistance);
                lr.material = new Material(Shader.Find("Sprites/Default"));
                lr.startColor = Color.yellow;
                lr.endColor = Color.yellow;
                
                debugObjects.Add(rayVisualizer);
            }
        }
        
        // Find the starting point on the track
        RaycastHit hit;
        if (Physics.Raycast(rayStart, rayDirection, out hit, rayDistance, trackLayer))
        {
            if (showExtendedDebug)
            {
                Debug.Log($"¡ÉXITO! Primer raycast golpeó: {hit.collider.gameObject.name} en posición: {hit.point}");
                
                // Visualizar el punto de impacto solo si está activa la opción
                if (crearObjetosDebug)
                {
                    GameObject hitMarker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    hitMarker.tag = "EditorOnly";
                    hitMarker.name = "HitMarker";
                    hitMarker.transform.position = hit.point;
                    hitMarker.transform.localScale = Vector3.one * 0.5f;
                    hitMarker.GetComponent<Renderer>().material.color = Color.green;
                    Destroy(hitMarker.GetComponent<Collider>());
                    
                    debugObjects.Add(hitMarker);
                }
            }
            
            Vector3 startPoint = hit.point;
            Vector3 startDirection = startPosition.forward;
            
            // Find track width at starting point
            float trackWidth = FindTrackWidth(startPoint, startDirection);
            
            // Create first track point
            TrackPoint first = new TrackPoint(startPoint, startDirection, trackWidth);
            trackPoints.Add(first);
            
            // Find points along the track
            FollowTrack(first);
            
            // Detectar curvas analizando cambios de dirección
            DetectCurvesAndOptimizePoints();
            
            Debug.Log($"Track analysis complete. Found {trackPoints.Count} points.");
        }
        else
        {
            if (showExtendedDebug)
            {
                Debug.LogError($"FALLO: El primer raycast no golpeó nada. Verifica que el startPosition ({startPosition.name}) está sobre la pista y que la LayerMask es correcta.");
                Debug.LogError($"RayStart: {rayStart}, RayDirection: {rayDirection}, RayDistance: {rayDistance}");
                Debug.LogError($"Track Layer: {LayerMaskToString(trackLayer)}, Track Mesh Layer: {trackMesh.gameObject.layer} ({LayerMask.LayerToName(trackMesh.gameObject.layer)})");
                
                // Realizar un raycast en todas las capas para ver si golpea algo
                RaycastHit fallbackHit;
                if (Physics.Raycast(rayStart, rayDirection, out fallbackHit, rayDistance))
                {
                    Debug.LogError($"Sin embargo, un raycast sin máscara de capa golpeó: {fallbackHit.collider.gameObject.name} en la capa {fallbackHit.collider.gameObject.layer} ({LayerMask.LayerToName(fallbackHit.collider.gameObject.layer)})");
                    
                    // Sugerir usar esa capa
                    Debug.LogError($"Sugerencia: Cambia Track Layer para incluir la capa '{LayerMask.LayerToName(fallbackHit.collider.gameObject.layer)}' o activa Auto Detect Track Layer");
                }
                else
                {
                    Debug.LogError("No se detectó ninguna colisión incluso sin filtro de capa. Verifica la posición de Start Position.");
                }
            }
            else
            {
                Debug.LogError("Could not find track under start position!");
            }
        }
        
        // Clean up temporary object if we created one
        if (startPosition.gameObject.name == "TempStartPosition")
        {
            DestroyImmediate(startPosition.gameObject);
            startPosition = null;
        }
    }
    
    // Nuevo método para detectar curvas y optimizar puntos
    private void DetectCurvesAndOptimizePoints()
    {
        if (trackPoints.Count < 3)
            return;
        
        List<TrackPoint> optimizedPoints = new List<TrackPoint>();
        optimizedPoints.Add(trackPoints[0]); // Mantener el primer punto
        
        // Detectar curvas analizando cambios de dirección
        for (int i = 1; i < trackPoints.Count - 1; i++)
        {
            TrackPoint prevPoint = trackPoints[i - 1];
            TrackPoint currentPoint = trackPoints[i];
            TrackPoint nextPoint = trackPoints[i + 1];
            
            // Calcular el cambio de dirección
            float angleChange = Vector3.Angle(prevPoint.direction, nextPoint.direction);
            float curvature = angleChange / 180f; // Normalizar entre 0 y 1
            
            // Marcar punto como curva si el cambio de dirección es significativo
            bool isCurve = curvature > curvatureThreshold;
            
            // Actualizar el punto actual con la información de curvatura
            currentPoint.isCurve = isCurve;
            currentPoint.curvature = curvature;
            
            // Si estamos en una curva y queremos centrar los puntos
            if (isCurve && centrarEnCurvas)
            {
                // Calcular una mejor posición para el punto (más centrado en la pista)
                Vector3 leftEdge = currentPoint.position - Vector3.Cross(currentPoint.direction, Vector3.up).normalized * (currentPoint.width / 2);
                Vector3 rightEdge = currentPoint.position + Vector3.Cross(currentPoint.direction, Vector3.up).normalized * (currentPoint.width / 2);
                
                // Ajustar la posición hacia el centro de la curva
                Vector3 curveCenter = (leftEdge + rightEdge) / 2f;
                
                // Hacer un raycast hacia abajo desde el centro calculado para asegurar que estamos sobre la pista
                RaycastHit centerHit;
                if (Physics.Raycast(curveCenter + Vector3.up * raycastHeight, Vector3.down, out centerHit, raycastHeight * 2, trackLayer))
                {
                    currentPoint.position = centerHit.point;
                }
            }
            
            // Añadir el punto actual a la lista optimizada
            optimizedPoints.Add(currentPoint);
        }
        
        optimizedPoints.Add(trackPoints[trackPoints.Count - 1]); // Mantener el último punto
        
        // Reemplazar la lista original con la optimizada
        trackPoints = optimizedPoints;
    }
    
    // Utilidad para convertir una LayerMask a string para depuración
    private string LayerMaskToString(LayerMask layerMask)
    {
        string result = "";
        for (int i = 0; i < 32; i++)
        {
            if ((layerMask & (1 << i)) != 0)
            {
                result += LayerMask.LayerToName(i) + ", ";
            }
        }
        return result.TrimEnd(',', ' ');
    }
    
    private void FollowTrack(TrackPoint startPoint)
    {
        TrackPoint currentPoint = startPoint;
        int maxPoints = 1000; // Safety limit
        int pointCount = 1; // We already added the first point

        float minSampleDistance = 0.5f;
        float originalSampleDistance = sampleDistance;

        while (pointCount < maxPoints)
        {
            float currentSampleDistance = sampleDistance;
            bool foundTrack = false;
            TrackPoint newPoint = null;

            // Probar con el sampleDistance original y, si no encuentra, reducirlo progresivamente
            for (int reduceStep = 0; reduceStep < 4 && !foundTrack; reduceStep++)
            {
                if (reduceStep > 0)
                    currentSampleDistance = Mathf.Max(minSampleDistance, sampleDistance * Mathf.Pow(0.5f, reduceStep));

                // Probar dirección principal y muchos ángulos alternativos
                float[] angles = { 0f, 15f, -15f, 30f, -30f, 45f, -45f, 60f, -60f, 75f, -75f, 90f, -90f };
                foreach (float angle in angles)
                {
                    Vector3 testDirection = Quaternion.Euler(0, angle, 0) * currentPoint.direction;
                    Vector3 nextSearchPos = currentPoint.position + testDirection * currentSampleDistance;
                    Vector3 rayStart = nextSearchPos + Vector3.up * raycastHeight;

                    RaycastHit hit;
                    if (Physics.Raycast(rayStart, Vector3.down, out hit, raycastHeight * 2, trackLayer))
                    {
                        Vector3 dirToHit = (hit.point - currentPoint.position).normalized;
                        Vector3 newDirection = Vector3.Lerp(currentPoint.direction, dirToHit, 0.7f).normalized;
                        newDirection.y = 0;
                        newDirection.Normalize();

                        float trackWidth = FindTrackWidth(hit.point, newDirection);

                        newPoint = new TrackPoint(hit.point, newDirection, trackWidth);
                        foundTrack = true;
                        break;
                    }
                }
            }

            if (foundTrack && newPoint != null)
            {
                trackPoints.Add(newPoint);
                currentPoint = newPoint;
                pointCount++;

                // Check if we've completed a closed loop
                if (closedTrack && pointCount > 5)
                {
                    float distanceToStart = Vector3.Distance(newPoint.position, startPoint.position);
                    if (distanceToStart < sampleDistance * 0.9f)
                    {
                        Debug.Log("Closed loop detected. Track analysis complete.");
                        break;
                    }
                }
            }
            else
            {
                Debug.LogWarning("Lost track. Analysis stopped.");
                break;
            }
        }

        if (pointCount >= maxPoints)
        {
            Debug.LogWarning("Reached maximum point count. Track may be too large or not properly closed.");
        }
    }
    
    private float FindTrackWidth(Vector3 position, Vector3 direction)
    {
        // Cast rays to the left and right to find the track edges
        Vector3 rightDir = Quaternion.Euler(0, 90, 0) * direction;
        
        float maxRayDist = 50f;
        float leftDist = CastToEdge(position, -rightDir, maxRayDist);
        float rightDist = CastToEdge(position, rightDir, maxRayDist);
        
        return leftDist + rightDist;
    }
    
    private float CastToEdge(Vector3 position, Vector3 direction, float maxDistance)
    {
        RaycastHit hit;
        // Raise position slightly to avoid hitting the track immediately
        Vector3 rayStart = position + Vector3.up * 0.1f;
        
        if (Physics.Raycast(rayStart, direction, out hit, maxDistance, trackLayer))
        {
            return hit.distance;
        }
        else
        {
            // If we didn't hit anything, return a default value
            return 2.5f;
        }
    }
    
    private void OnDrawGizmos()
    {
        if (!showDebugVisuals)
            return;
            
        // Visualizar el punto de inicio y su dirección
        if (startPosition != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(startPosition.position, gizmoSize * 1.5f);
            Gizmos.DrawRay(startPosition.position, startPosition.forward * 3f);
            
            // Visualizar el primer raycast
            Vector3 rayStart = startPosition.position + Vector3.up * raycastHeight;
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(rayStart, rayStart + Vector3.down * (raycastHeight * 2));
        }
            
        // Visualizar los puntos de la pista
        if (trackPoints.Count < 2)
            return;
            
        // Draw track points and connections
        for (int i = 0; i < trackPoints.Count; i++)
        {
            TrackPoint point = trackPoints[i];
            TrackPoint nextPoint = (i < trackPoints.Count - 1) ? trackPoints[i + 1] : (closedTrack ? trackPoints[0] : null);
            
            // Usar colores diferentes para curvas y rectas
            if (point.isCurve)
            {
                // Resaltar curvas en naranja o rojo según la curvatura
                Gizmos.color = Color.Lerp(Color.yellow, Color.red, point.curvature);
                Gizmos.DrawSphere(point.position, gizmoSize * 1.2f);
            }
            else
            {
                // Draw normal point
                Gizmos.color = Color.green;
                Gizmos.DrawSphere(point.position, gizmoSize);
            }
            
            // Draw direction
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(point.position, point.direction * 2f);
            
            // Draw connection to next point
            if (nextPoint != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(point.position, nextPoint.position);
            }
            
            // Draw track width
            Gizmos.color = Color.red;
            Vector3 rightDir = Quaternion.Euler(0, 90, 0) * point.direction;
            Gizmos.DrawLine(
                point.position - rightDir * (point.width / 2),
                point.position + rightDir * (point.width / 2)
            );
        }
    }

    // Public method to get a copy of the track points
    public List<TrackPoint> GetTrackPoints()
    {
        return new List<TrackPoint>(trackPoints);
    }
}

#if UNITY_EDITOR
// Editor personalizado para añadir un botón para limpiar los objetos de debug
[UnityEditor.CustomEditor(typeof(TrackAnalyzer))]
public class TrackAnalyzerEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        
        TrackAnalyzer analyzer = (TrackAnalyzer)target;
        
        UnityEditor.EditorGUILayout.Space();
        if (GUILayout.Button("Analizar Pista (Auto-Detectar Capa)", GUILayout.Height(30)))
        {
            analyzer.AnalyzeTrackWithLayerDetection();
        }
        
        if (GUILayout.Button("Analizar Pista (Configuración Actual)", GUILayout.Height(30)))
        {
            analyzer.AnalyzeTrack();
        }
        
        if (GUILayout.Button("Limpiar Objetos de Debug", GUILayout.Height(24)))
        {
            // Buscar y eliminar objetos RayVisualizer y HitMarker
            GameObject[] visualizers = GameObject.FindGameObjectsWithTag("EditorOnly");
            foreach (var visualizer in visualizers)
            {
                if (visualizer.name.Contains("RayVisualizer") || visualizer.name.Contains("HitMarker"))
                {
                    DestroyImmediate(visualizer);
                }
            }
            
            // Buscar por nombre
            var allObjects = FindObjectsOfType<GameObject>();
            foreach (var obj in allObjects)
            {
                if (obj.name == "RayVisualizer" || obj.name == "HitMarker")
                {
                    DestroyImmediate(obj);
                }
            }
        }
    }
}
#endif 