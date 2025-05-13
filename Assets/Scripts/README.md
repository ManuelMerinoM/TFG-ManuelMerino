# Reinforcement Learning for Circuit Navigation

Este sistema implementa un agente de aprendizaje por refuerzo para coches que aprenden a navegar por un circuito siguiendo checkpoints.

## Características

- Utiliza sensores de percepción de rayos (Ray Perception Sensors) para detectar el entorno
- Recompensa a los agentes por pasar por los checkpoints en orden
- Mayor recompensa por pasar por el centro de los checkpoints
- Penalización por chocar contra las paredes del circuito
- Sistema de recompensas continuas basado en la distancia al siguiente checkpoint

## Configuración del Agente

1. Añade el componente `NNCursor` a tu coche
2. Añade el componente `RayPerceptionSensorComponent3D` (de ML-Agents) al mismo objeto
3. Añade el componente `Behavior Parameters` (se añade automáticamente con ML-Agents)
4. Asigna la referencia de `TrackCheckpoints` en el inspector
5. Ajusta los parámetros según sea necesario:
   - Velocidad de movimiento y giro
   - Valores de recompensas y penalizaciones
   - Configuración de los sensores de rayos

## Configuración de los Sensores de Rayos

Para configurar el componente `RayPerceptionSensorComponent3D`:

1. En el Inspector, haz clic en "Add Component" y busca "Ray Perception Sensor 3D"
2. Configura los siguientes parámetros:
   - **Sensor Name**: Nombre descriptivo (ej. "RaySensor")
   - **Detectable Tags**: Añade las etiquetas "ParedExt", "ParedInt", "Checkpoint", "Coche"
   - **Rays Per Direction**: Número de rayos a cada lado (ej. 4 = 8 rayos en total)
   - **Max Ray Degrees**: 180 para una cobertura semicircular
   - **Sphere Cast Radius**: 0.5 (ajustar según tamaño del coche)
   - **Ray Length**: 20 (ajustar según escala del circuito)
   - **Observation Stacks**: 1
   - **Start Vertical Offset**: 0 (ajustar según altura del coche)
   - **End Vertical Offset**: 0
   - **Ray Layer Mask**: Default (o la capa donde estén los objetos a detectar)

## Configuración de Behavior Parameters

El componente `Behavior Parameters` es esencial para el funcionamiento del agente. Configúralo así:

1. **Behavior Name**: "CircuitoAgent" (debe coincidir con el nombre en el archivo YAML)
2. **Vector Observation**:
   - **Space Size**: 6 (corresponde a las observaciones en CollectObservations)
   - **Stacked Vectors**: 1
3. **Actions**:
   - **Continuous Actions**: 0
   - **Discrete Branches**: 2
   - **Discrete Branch 0 Size**: 3 (0: no moverse, 1: adelante, 2: atrás)
   - **Discrete Branch 1 Size**: 3 (0: no girar, 1: izquierda, 2: derecha)
4. **Model**:
   - Durante el entrenamiento: selecciona "Behaviors Parameters > Inference Device > CPU"
   - Para usar un modelo entrenado: selecciona "Model" y arrastra el archivo .onnx generado
5. **Inference Device**: CPU (o GPU si está disponible)
6. **Behavior Type**:
   - Durante el entrenamiento: "Default"
   - Para pruebas manuales: "Heuristic Only"
   - Para usar un modelo entrenado: "Inference Only"

## Entrenamiento

1. Asegúrate de tener ML-Agents instalado
2. Usa el archivo de configuración `circuito_config.yaml` para el entrenamiento
3. Ejecuta el entrenamiento con el comando:
   ```
   mlagents-learn Assets/Resources/circuito_config.yaml --run-id=CircuitoTraining
   ```
4. Inicia el juego en modo de entrenamiento

## Estructura de Recompensas

- **Recompensa positiva**: Pasar por checkpoints correctos
- **Recompensa adicional**: Pasar por el centro de los checkpoints
- **Recompensa continua**: Acercarse al siguiente checkpoint
- **Penalización**: Chocar contra paredes
- **Penalización**: Pasar por checkpoints en orden incorrecto
- **Penalización pequeña**: Por cada paso de tiempo (para fomentar eficiencia)

## Consejos para el Entrenamiento

- Comienza con un circuito simple y luego incrementa la complejidad
- Ajusta los valores de recompensa según el comportamiento observado
- Utiliza múltiples agentes en paralelo para acelerar el entrenamiento
- Monitoriza el entrenamiento con TensorBoard para visualizar el progreso 