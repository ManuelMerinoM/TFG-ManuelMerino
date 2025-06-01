using UnityEngine;
using System.Collections.Generic;
using System;
using System.Linq; 
using TMPro; 
using System.Collections; 
using Unity.MLAgents; 
using Unity.MLAgents.Policies; 
using Unity.Barracuda; 

public class GameController : MonoBehaviour
{
    [SerializeField] private TrackCheckpoints trackCheckpoints; // Reference to the active track's TrackCheckpoints

    // UI Panels (assign in Inspector)
    [SerializeField] private GameObject titleScreenPanel;
    [SerializeField] private GameObject selectionScreenPanel;
    [SerializeField] private GameObject raceUIPanel; 
    [SerializeField] private GameObject resultsScreenPanel;

    [SerializeField] private GameObject creditsScreenPanel;

    // UI Text Elements (assign in Inspector - within raceUIPanel)
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private TextMeshProUGUI lapText;
    [SerializeField] private TextMeshProUGUI positionText;

    // UI Text Elements (assign in Inspector - within resultsScreenPanel)
    [SerializeField] private TextMeshProUGUI resultsText; // Text to display race results

    // Class to track player progress
    private class PlayerProgress
    {
        public int lap = 0;
        public int checkpointIndex = -1; // Start at -1 to indicate no checkpoint passed yet (before the first checkpoint)
        public bool isRaceComplete = false;
        public float finishTime = -1f; // New: Store finish time (-1 indicates not finished)
    }

    // Dictionary to store progress for each car (player/AI)
    private Dictionary<Transform, PlayerProgress> playerProgressMap;

    // Race Variables
    public int totalLaps = 3; 

    // Track Selection (using GameObjects in the current scene)
    [SerializeField] private List<GameObject> availableTrackGameObjects; // Assign track GameObjects here
    private GameObject selectedTrackGameObject; // The currently selected track GameObject

    // Start Positions (found dynamically in the active track GameObject)
    private Transform[] currentStartPositions;

    // Player's chosen start position index (0-5)
    private int playerSelectedStartIndex = 0; // Default to first position
    public int PlayerSelectedStartIndex { get => playerSelectedStartIndex; set => playerSelectedStartIndex = Mathf.Clamp(value, 0, 5); }


    // AI Difficulty Models (Assign NNModel assets in Inspector)
    [SerializeField] private NNModel easyAIModel; 
    [SerializeField] private NNModel mediumAIModel; 
    [SerializeField] private NNModel hardAIModel; 

    private enum DifficultyLevel { Easy, Medium, Hard }
    [SerializeField] private float checkAIDifficultyInterval = 2f; // How often to check and adjust AI difficulty
    private float timeSinceLastAIDifficultyCheck;

    // Race Timer
    private float raceStartTime;
    private float currentRaceTime;
    public bool isRaceRunning = false;

    // Countdown Timer
    [SerializeField] private float raceCountdownDuration = 5f; // 5 seconds countdown
    private float currentCountdownTime;
    private bool isCountingDown = false;
    [SerializeField] private TextMeshProUGUI countdownText; // UI Text for countdown

    // Public properties for UI access
    public float CurrentRaceTime => currentRaceTime;

    private Transform playerCarTransform;

    // Get the player's current lap
    public int PlayerCurrentLap 
    {
        get
        {
            if (playerCarTransform != null && playerProgressMap.TryGetValue(playerCarTransform, out PlayerProgress playerProgress))
            {
                return playerProgress.lap;
            }
            return 0;
        }
    }

    // Get the player's current position
    public int PlayerCurrentPosition
    {
         get
        {
            if (playerCarTransform != null)
            {
                return GetPlayerPosition(playerCarTransform);
            }
            return -1;
        }
    }

    [SerializeField] private List<GameObject> carGameObjects; // All the cars in the race (player first)

    void Awake()
    {
        playerProgressMap = new Dictionary<Transform, PlayerProgress>();
    }

    void Start()
    {
        // Initially show the title screen and hide others
        ShowTitleScreen();
        
        // Ensure all track GameObjects are initially inactive except potentially one for setup
        SetAllTracksInactive();

    }

    void Update()
    {
        // Update the timer if the race is running
        if (isRaceRunning)
        {   
            currentRaceTime = Time.time - raceStartTime;
            UpdateUI(); // Update UI elements every frame
            // Check and adjust AI difficulty periodically
            timeSinceLastAIDifficultyCheck += Time.deltaTime;
            if (timeSinceLastAIDifficultyCheck >= checkAIDifficultyInterval)
            {
                AdjustAIDifficulty();
                timeSinceLastAIDifficultyCheck = 0f;
            }
        }

        // Countdown update (handled by coroutine)
    }

    private void UpdateUI()
    {
        if (timerText != null) timerText.text = "Tiempo: " + currentRaceTime.ToString("F2");
        
        if (lapText != null)
        {
            // Use the public property to get player's current lap and display it
            lapText.text = "Vuelta: " + (PlayerCurrentLap + 1) + "/" + totalLaps; 
        }
        
        if (positionText != null)
        {
            // Use the public property to get player's current position and display it
             positionText.text = "Posición: " + PlayerCurrentPosition; 
        }
    }

    // --- UI Navigation Methods --- 

    public void ShowTitleScreen()
    {
        if (titleScreenPanel != null) titleScreenPanel.SetActive(true);
        if (selectionScreenPanel != null) selectionScreenPanel.SetActive(false);
        if (raceUIPanel != null) raceUIPanel.SetActive(false);
        if (resultsScreenPanel != null) resultsScreenPanel.SetActive(false);
        if (creditsScreenPanel != null) creditsScreenPanel.SetActive(false);
        Debug.Log("Showing Title Screen");

        
        SetAllTracksInactive(); // Deactivate all tracks when going to title screen
    }

    public void ShowSelectionScreen()
    {
        if (titleScreenPanel != null) titleScreenPanel.SetActive(false);
        if (selectionScreenPanel != null) selectionScreenPanel.SetActive(true);
        if (raceUIPanel != null) raceUIPanel.SetActive(false);
        if (resultsScreenPanel != null) resultsScreenPanel.SetActive(false); // Hide results screen
        if (creditsScreenPanel != null) creditsScreenPanel.SetActive(false);
        Debug.Log("Showing Selection Screen");


        // Ensure the selected track is active when showing the selection screen
        if (selectedTrackGameObject != null)
        {
             selectedTrackGameObject.SetActive(true);
        }
    }

    public void ShowResultsScreen()
    {
        if (titleScreenPanel != null) titleScreenPanel.SetActive(false);
        if (selectionScreenPanel != null) selectionScreenPanel.SetActive(false);
        if (raceUIPanel != null) raceUIPanel.SetActive(false);
        if (resultsScreenPanel != null) resultsScreenPanel.SetActive(true);
        if (creditsScreenPanel != null) creditsScreenPanel.SetActive(false);
        Debug.Log("Showing Results Screen");

        DisplayRaceResults(); // Populate the results text

        // Deactivate all tracks after race finishes and results are shown, before going back to menu
         SetAllTracksInactive();
    }

    public void ShowCreditsScreen(){

        if (titleScreenPanel != null) titleScreenPanel.SetActive(false);
        if (selectionScreenPanel != null) selectionScreenPanel.SetActive(false);
        if (raceUIPanel != null) raceUIPanel.SetActive(false);
        if (resultsScreenPanel != null) resultsScreenPanel.SetActive(false);
        if (creditsScreenPanel != null) creditsScreenPanel.SetActive(true);
        Debug.Log("Showing Credits Screen");
    }

    // Method called by the "Play" button on the title screen
    public void OnPressPlayButton()
    {
        ShowSelectionScreen();
    }

    // Method called by the "Start" button on the selection screen
    public void OnPressStartRaceButton()
    {
        // This method will trigger the race start process
        // It needs to use the selectedTrackGameObject and playerSelectedStartIndex
        StartRace(playerSelectedStartIndex); // Pass only start index, track is already selected
    }

    // Method called by the "Exit" button on the title screen
    public void OnPressExitButton()
    {
        Debug.Log("Exiting Game");
        Application.Quit(); // Standard way to quit a Unity application
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false; // Stop play mode in the editor
#endif
    }

    // Method called by the "Back to Menu" button on the results screen
    public void OnPressBackToMenuButton()
    {
        ShowTitleScreen();
    }

    // --- Race Control Methods ---

    // Method to start the race process (instantiation, countdown)
    public void StartRace(int playerStartIndex)
    {
        if (isRaceRunning || isCountingDown)
        {
            Debug.LogWarning("Race is already running or counting down.");
            return;
        }

        if (selectedTrackGameObject == null)
        {
            Debug.LogError("No track is selected.");
            ShowSelectionScreen();
            return;
        }

        // Ensure TrackCheckpoints is found on the selected track
        if (trackCheckpoints == null)
        {
            trackCheckpoints = selectedTrackGameObject.GetComponentInChildren<TrackCheckpoints>();
            if (trackCheckpoints == null)
            {
                Debug.LogError($"TrackCheckpoints script not found on the selected track GameObject ({selectedTrackGameObject.name}) or its children.");
                ShowSelectionScreen();
                return;
            }
        }

        // Store selected index
        PlayerSelectedStartIndex = playerStartIndex; // Use the public property setter for clamping

        // Hide UI panels and show race UI
        if (titleScreenPanel != null) titleScreenPanel.SetActive(false);
        if (selectionScreenPanel != null) selectionScreenPanel.SetActive(false);
        if (resultsScreenPanel != null) resultsScreenPanel.SetActive(false);
        if (creditsScreenPanel != null) creditsScreenPanel.SetActive(false);
        if (raceUIPanel != null) raceUIPanel.SetActive(true);
        if(countdownText != null) countdownText.gameObject.SetActive(true);

        StartCoroutine(PrepareRace(selectedTrackGameObject));
    }

    // Prepare the race by setting up the track, checkpoints, and cars
    private IEnumerator PrepareRace(GameObject trackGameObject)
    {
        if (trackGameObject != null) trackGameObject.SetActive(true);

        trackCheckpoints = trackGameObject.GetComponentInChildren<TrackCheckpoints>();
        if (trackCheckpoints == null)
        {
            Debug.LogError($"TrackCheckpoints not found on the track GameObject ({trackGameObject.name}) or its children during preparation.");
            ShowSelectionScreen();
            yield break;
        }

        // Subscribe to the checkpoint correct event
        trackCheckpoints.OnCarCorrectCheckpoint += TrackCheckpoints_OnCarCorrectCheckpoint;

        // Assign the new TrackCheckpoints to all the NNCheck agents
        foreach (var carGO in carGameObjects)
        {
            NNCheck nnAgent = carGO.GetComponent<NNCheck>();
            if (nnAgent != null)
            {
                nnAgent.SetTrackCheckpoints(trackCheckpoints);
            }
        }
        // Find the start positions in the track GameObject
        Transform startPositionsParent = trackGameObject.transform.Find("StartPositions");
        if (startPositionsParent != null)
        {
            currentStartPositions = new Transform[startPositionsParent.transform.childCount];
            for (int i = 0; i < startPositionsParent.transform.childCount; i++)
            {
                currentStartPositions[i] = startPositionsParent.transform.GetChild(i);
            }
            Array.Sort(currentStartPositions, (t1, t2) => String.Compare(t1.name, t2.name));
        } else
        {
            Debug.LogError($"StartPositions parent GameObject not found as a child of the track GameObject ({trackGameObject.name}).");
            ShowSelectionScreen();
            yield break;
        }
        // If there are no start positions, show an error and break the coroutine
        if (currentStartPositions.Length == 0)
        {
            Debug.LogError("No start positions found in the active track GameObject.");
            ShowSelectionScreen();
            yield break;
        }

        // If there are no cars assigned in carGameObjects, show an error and break the coroutine
        if (carGameObjects == null || carGameObjects.Count == 0)
        {
            Debug.LogError("No cars assigned in carGameObjects.");
            ShowSelectionScreen();
            yield break;
        }
        // If there are not enough start positions for all cars, show an error and break the coroutine
        if (currentStartPositions.Length < carGameObjects.Count)
        {
            Debug.LogError($"Not enough start positions ({currentStartPositions.Length}) for all cars ({carGameObjects.Count}).");
            ShowSelectionScreen();
            yield break;
        }

        playerProgressMap.Clear();

        // Assign positions: player is always the first in the list
        int playerIdx = PlayerSelectedStartIndex;
        GameObject playerCarGO = carGameObjects[0];
        playerCarTransform = playerCarGO.transform;
        playerCarTransform.position = currentStartPositions[playerIdx].position;
        playerCarTransform.rotation = currentStartPositions[playerIdx].rotation;
        playerProgressMap.Add(playerCarTransform, new PlayerProgress());

        // Assign AIs to the remaining positions
        int aiListIdx = 1; // Start at 1 because 0 is the player
        for (int i = 0; i < currentStartPositions.Length; i++)
        {
            if (i == playerIdx) continue;
            if (aiListIdx >= carGameObjects.Count) break;
            GameObject aiCarGO = carGameObjects[aiListIdx];
            aiCarGO.transform.position = currentStartPositions[i].position;
            aiCarGO.transform.rotation = currentStartPositions[i].rotation;
            playerProgressMap.Add(aiCarGO.transform, new PlayerProgress());
            aiListIdx++;
        }

        // Reset physics and control
        foreach (var carGO in carGameObjects)
        {
            Rigidbody rb = carGO.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
            }
            if (carGO == playerCarGO)
                SetPlayerInputEnabled(carGO.transform, false);
            else
                SetAIControlEnabled(carGO.transform, false);
        }

        // Assign initial AI model if applicable
        for (int i = 1; i < carGameObjects.Count; i++)
        {
            var aiCar = carGameObjects[i];
            BehaviorParameters bp = aiCar.GetComponent<BehaviorParameters>();
            if (bp != null && easyAIModel != null)
                bp.Model = easyAIModel;
        }

        StartCoroutine(RaceCountdown());
    }

    // Countdown to start the race
    private IEnumerator RaceCountdown()
    {
        isCountingDown = true;
        currentCountdownTime = raceCountdownDuration;

        while (currentCountdownTime > 0)
        {
            if (countdownText != null)
            {  
                 // Display countdown time (e.g., show whole seconds)
                countdownText.text = Mathf.Ceil(currentCountdownTime).ToString();
            }
            yield return null; // Wait for the next frame
            currentCountdownTime -= Time.deltaTime;
        }

        // Countdown finished
        if (countdownText != null) countdownText.text = "Empecemos!";
        Debug.Log("Countdown Finished. GO!");
        yield return new WaitForSeconds(1f); // Show "GO!" for a second

        if (countdownText != null) countdownText.gameObject.SetActive(false);
        isCountingDown = false;

        // Start the race officially
        ActuallyStartRace();
    }
    // Method to start the race after the countdown
    private void ActuallyStartRace()
    {
        raceStartTime = Time.time;
        currentRaceTime = 0f; // Reset race time at the official start
        isRaceRunning = true;
        Debug.Log("Race Officially Started!");

        // Enable player input
        if(playerCarTransform != null) SetPlayerInputEnabled(playerCarTransform, true);

        // Enable AI scripts
        foreach (Transform carTransform in playerProgressMap.Keys)
        {
             if(carTransform != playerCarTransform)
             {
                 SetAIControlEnabled(carTransform, true); // Enable AI control
             }
        }

        // Initial UI update now that race has started
        UpdateUI();

        // Perform initial AI difficulty adjustment
        AdjustAIDifficulty();
    }

    // Helper method to enable/disable player input
    private void SetPlayerInputEnabled(Transform playerCar, bool isEnabled)
    {

        CarController carController = playerCar.GetComponent<CarController>();
        if (carController != null)
        {

             carController.enabled = isEnabled; 
        }

        // Also disable any AI scripts on the player car
         NNCheck nnAgent = playerCar.GetComponent<NNCheck>();
         if(nnAgent != null) nnAgent.enabled = false; 

        
         Rigidbody rb = playerCar.GetComponent<Rigidbody>();
         if (rb != null)
         {
              rb.isKinematic = !isEnabled; // Make kinematic when disabled, not kinematic when enabled
              // Reset velocity when disabling
              if (!isEnabled)
              {
                  rb.velocity = Vector3.zero;
                  rb.angularVelocity = Vector3.zero;
              } else
              {
                  // Reset velocity/angular velocity and make non-kinematic when enabling
                   rb.velocity = Vector3.zero;
                   rb.angularVelocity = Vector3.zero;
                   rb.isKinematic = false;
              }
         }
    }

    // Helper method to enable/disable AI control
    private void SetAIControlEnabled(Transform aiCar, bool isEnabled)
    {

        NNCheck nnAgent = aiCar.GetComponent<NNCheck>();
        if (nnAgent != null)
        {
            nnAgent.enabled = isEnabled; // Enable/Disable the NNCheck script
        }
        // Disable the Rigidbody when disabling AI control
        Rigidbody rb = aiCar.GetComponent<Rigidbody>();
         if (rb != null)
         {
             rb.isKinematic = !isEnabled; // Make kinematic when disabled, not kinematic when enabled
              // Reset velocity when disabling
              if (!isEnabled)
              {
                  rb.velocity = Vector3.zero;
                  rb.angularVelocity = Vector3.zero;
              } else
              {
                  // Reset velocity/angular velocity and make non-kinematic when enabling
                   rb.velocity = Vector3.zero;
                   rb.angularVelocity = Vector3.zero;
                   rb.isKinematic = false;
              }
         }
    }

    // Method to adjust AI difficulty based on player position
    private void AdjustAIDifficulty()
    {
        // Only adjust if race is running, player exists, there are AI cars, and models are assigned.
        if (!isRaceRunning || playerCarTransform == null || playerProgressMap.Count <= 1 || 
            easyAIModel == null || mediumAIModel == null || hardAIModel == null) return;

        int playerPosition = GetPlayerPosition(playerCarTransform);

        NNModel targetModel = null;

        // Determine the target model based on player position
        int totalCars = playerProgressMap.Count;

        // Ensure playerPosition is valid before using it for difficulty calculation
        if (playerPosition < 1 || playerPosition > totalCars)
        {
            Debug.LogWarning($"Player position {playerPosition} is out of expected range (1 to {totalCars}). Cannot adjust AI difficulty.");
            return;
        }

        if (playerPosition == 1)
        {
            targetModel = hardAIModel;
        } else if (playerPosition > 1 && playerPosition <= Mathf.CeilToInt(totalCars / 2f)) // Top half (excluding 1st)
        {
            targetModel = mediumAIModel;
        } else // Bottom half (playerPosition > Mathf.CeilToInt(totalCars / 2f) && playerPosition <= totalCars)
        {
            targetModel = easyAIModel;
        }

        if (targetModel == null)
        {
            // This case should ideally not be reached if playerPosition is valid
            Debug.LogWarning("Could not determine target AI model based on player position.");
            return; // Cannot adjust difficulty without a target model
        }

        Debug.Log($"Player position: {playerPosition} (out of {totalCars} cars). Setting AI models to: {targetModel.name}");

        // Apply the determined model to all AI cars
        foreach (var entry in playerProgressMap)
        {
            Transform carTransform = entry.Key;
            if (carTransform != playerCarTransform) // Exclude the player car
            {
                BehaviorParameters bp = carTransform.GetComponent<BehaviorParameters>();
                if (bp != null)
                {
                    // Assign the selected NNModel to the Behavior Parameters component
                    bp.Model = targetModel;
                } else
                {
                    Debug.LogWarning($"AI car ({carTransform.name}) is missing BehaviorParameters component.");
                }
            }
        }
    }

    // Method to handle a player passing a checkpoint
    private void TrackCheckpoints_OnCarCorrectCheckpoint(object sender, TrackCheckpoints.CarCheckPointEventArgs e)
    {
        Transform carTransform = e.carTransform;
        CheckpointSingle passedCheckpoint = e.checkpointSingle;
        

        if (playerProgressMap.TryGetValue(carTransform, out PlayerProgress playerProgress))
        {
            if (playerProgress.isRaceComplete) // Ignore if already finished
            {
                return;
            }

            // Ensure trackCheckpoints is assigned before accessing its list
            if (trackCheckpoints == null)
            {
                 Debug.LogError("TrackCheckpoints is not assigned in TrackCheckpoints_OnCarCorrectCheckpoint.");
                 return;
            }

            int passedCheckpointIndex = trackCheckpoints.checkpointSingleList.IndexOf(passedCheckpoint);
            
            // Check if the passed checkpoint is part of the current track's list
            if (passedCheckpointIndex == -1)
            {
                 Debug.LogWarning($"Car {carTransform.name} passed a checkpoint that is not in the current track's checkpoint list.");
                 return; // Ignore checkpoints from other tracks or invalid checkpoints
            }
            Debug.Log($"Checkpoint passed by: {carTransform.name}. Index: {passedCheckpointIndex}, Lap: {playerProgress.lap}");

            playerProgress.checkpointIndex = passedCheckpointIndex;

            // Check if the last checkpoint of the track was passed
            if (passedCheckpointIndex == trackCheckpoints.checkpointSingleList.Count - 1)
            {
                // This signifies completing a lap
                playerProgress.lap++;
                Debug.Log($"{carTransform.name} completed lap {playerProgress.lap}");

                // Reset checkpoint index for the next lap (if not the final lap)
                if(playerProgress.lap < totalLaps)
                {
                     playerProgress.checkpointIndex = 0; // Ready for the first checkpoint of the next lap
                } else if (playerProgress.lap == totalLaps) {
                    // If completed the last lap, mark race complete and record time
                     playerProgress.isRaceComplete = true;
                     playerProgress.finishTime = currentRaceTime; // Record finish time
                     Debug.Log($"{carTransform.name} finished the race with time: {playerProgress.finishTime:F2}!");

                     // Keep checkpointIndex on the last checkpoint for sorting purposes after finishing
                     playerProgress.checkpointIndex = trackCheckpoints.checkpointSingleList.Count - 1; 

                    // If the player finished, show results screen (after a short delay perhaps?)
                    if (carTransform == playerCarTransform)
                    {
                        // Delay showing results to allow player to see finish line etc.
                        StartCoroutine(ShowResultsScreenAfterDelay(2f)); // Show results 2 seconds after player finishes
                         // Stop the main race timer when the player finishes
                         isRaceRunning = false; // Stop updating currentRaceTime
                         isCountingDown = false; // Ensure countdown doesn't interfere if somehow active
                    }
                }
            }
        }
        else
        {
            Debug.LogWarning($"Player progress not found for car: {carTransform.name}. This should not happen if cars are instantiated and tracked correctly.");
        }
    }
    // Method to show the results screen after a delay
    private IEnumerator ShowResultsScreenAfterDelay(float delay)
    {
         // Optional: Disable player input immediately when player finishes
        if (playerCarTransform != null) SetPlayerInputEnabled(playerCarTransform, false);

        // Disable AI for all cars immediately when player finishes
         foreach (Transform carTransform in playerProgressMap.Keys)
         {
             if(carTransform != playerCarTransform && carTransform != null)
             {
                 SetAIControlEnabled(carTransform, false); // Needs implementation
             }
         }

        yield return new WaitForSeconds(delay);
        ShowResultsScreen();
    }

    // Method to display race results on the results screen
    private void DisplayRaceResults()
    {
        if (resultsText == null) return;

        // Sort players by finish time (finished players first, then by lap and checkpoint for others)
        List<KeyValuePair<Transform, PlayerProgress>> sortedPlayers = playerProgressMap.ToList();

        sortedPlayers.Sort((pair1, pair2) =>
        {
            bool p1Finished = pair1.Value.isRaceComplete;
            bool p2Finished = pair2.Value.isRaceComplete;

            if (p1Finished && !p2Finished) return -1; // p1 comes before p2
            if (!p1Finished && p2Finished) return 1;  // p2 comes before p1

            if (p1Finished && p2Finished)
            {
                // Both finished, sort by finish time (ascending)
                return pair1.Value.finishTime.CompareTo(pair2.Value.finishTime);
            } else
            {
                // Neither finished, sort by lap (descending), then checkpoint (descending)
                int lapCompare = pair2.Value.lap.CompareTo(pair1.Value.lap);
                if (lapCompare != 0) return lapCompare;

                return pair2.Value.checkpointIndex.CompareTo(pair1.Value.checkpointIndex);

            }
        });

        // Build the results string
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("--- Resultados de la Carrera ---");
        sb.AppendLine("");
        // Display the results
        for (int i = 0; i < sortedPlayers.Count; i++)
        {
            var playerEntry = sortedPlayers[i];
            string carName = playerEntry.Key != null ? playerEntry.Key.name : "Unknown Car"; 
            PlayerProgress progress = playerEntry.Value;
            int rank = i + 1; // 1-based rank

            sb.Append($"{rank}. {carName}: ");

            if (progress.isRaceComplete)
            {
                sb.AppendLine($"Finalizado en {progress.finishTime:F2}");
            } else
            {
                 // Display current progress for those who didn't finish
                 // Show lap + 1 because lap is 0-indexed internally
                 sb.AppendLine($"Vuelta {progress.lap + 1}/{totalLaps}, Checkpoint {progress.checkpointIndex + 1}");
            }
        }

        resultsText.text = sb.ToString();
    }

    // Method to stop the race 
    public void StopRace()
    {
        if (!isRaceRunning && !isCountingDown) // Avoid stopping if already stopped or not started
        {
             Debug.LogWarning("Race is not running or counting down.");
             return;
        }

        isRaceRunning = false;
        isCountingDown = false; // Ensure countdown stops
        Debug.Log("Race Stopped.");

        // Disable input and AI for all cars
        foreach (Transform carTransform in playerProgressMap.Keys)
        {
            if(carTransform != null)
            {
                SetPlayerInputEnabled(carTransform, false); 
                SetAIControlEnabled(carTransform, false); 
               
                 Rigidbody rb = carTransform.GetComponent<Rigidbody>();
                 if (rb != null)
                 {
                     rb.velocity = Vector3.zero;
                     rb.angularVelocity = Vector3.zero;
                     rb.isKinematic = true; // Make kinematic to prevent physics movement
                 }
            }
        }

        // Show results screen if race was stopped manually and player finished
        // If player finished, the coroutine handles showing the results screen.
    }

    // Method to determine player position in the race
    public int GetPlayerPosition(Transform playerTransform)
    {
        // Ensure the playerProgressMap is not empty before sorting
        if (playerProgressMap.Count == 0 || playerTransform == null || !playerProgressMap.ContainsKey(playerTransform))
        {
            return -1; // No players to rank or player not found
        }

        // Create a list of players and their progress
        List<KeyValuePair<Transform, PlayerProgress>> sortedPlayers = playerProgressMap.ToList();

        // Sort players. Primary sort by finish time (ascending) for finished players,
        // then by lap (descending), then by checkpoint index (descending) for others.
        Debug.Log($"Calculating positions. Player {playerTransform.name} is at Lap: {playerProgressMap[playerTransform].lap}, Checkpoint: {playerProgressMap[playerTransform].checkpointIndex}");
        sortedPlayers.Sort((pair1, pair2) =>
        {
            bool p1Finished = pair1.Value.isRaceComplete;
            bool p2Finished = pair2.Value.isRaceComplete;

            if (p1Finished && !p2Finished) return -1; // p1 finished, p2 didn't, p1 is higher rank
            if (!p1Finished && p2Finished) return 1;  // p2 finished, p1 didn't, p2 is higher rank
            if (p1Finished && p2Finished)
            {
                // Both finished, sort by finish time (ascending)
                return pair1.Value.finishTime.CompareTo(pair2.Value.finishTime);
            } else
            {
                // Neither finished, sort by lap (descending), then checkpoint (descending)
                int lapCompare = pair2.Value.lap.CompareTo(pair1.Value.lap);
                if (lapCompare != 0) return lapCompare;

                return pair2.Value.checkpointIndex.CompareTo(pair1.Value.checkpointIndex);

            }
        });
        for (int i = 0; i < sortedPlayers.Count; i++)
{
    Debug.Log($"Rank {i + 1}: {sortedPlayers[i].Key.name}, Lap: {sortedPlayers[i].Value.lap}, Checkpoint: {sortedPlayers[i].Value.checkpointIndex}, Finished: {sortedPlayers[i].Value.isRaceComplete}");
}
        // Find the position of the requested player transform
        int position = sortedPlayers.FindIndex(pair => pair.Key == playerTransform);

        // FindIndex returns 0-based index, race position is 1-based
        return position != -1 ? position + 1 : -1; // Return 1-based position or -1 if not found
    }

    // Method to select a track
    public void SelectTrack(GameObject trackObject)
    {
        if (availableTrackGameObjects == null || !availableTrackGameObjects.Contains(trackObject))
        {
            Debug.LogWarning($"Selected GameObject {trackObject.name} is not in the list of available tracks.");
            return;
        }

        // Deactivate all other tracks and activate the selected one
        SetAllTracksInactive();
        if (trackObject != null) trackObject.SetActive(true);

        selectedTrackGameObject = trackObject;
        Debug.Log($"Selected track: {selectedTrackGameObject.name}");

        // Find the TrackCheckpoints for the selected track immediately
        trackCheckpoints = selectedTrackGameObject.GetComponentInChildren<TrackCheckpoints>();
         if (trackCheckpoints == null)
         {
             Debug.LogError($"TrackCheckpoints script not found on the selected track GameObject ({selectedTrackGameObject.name}) or its children.");
         }

    }

     // Method to select a start position 
    public void SelectStartPosition(int index)
    {
        PlayerSelectedStartIndex = index; // Use the public property setter for clamping
        Debug.Log($"Selected start position index: {PlayerSelectedStartIndex}");
    }

    // Helper method to deactivate all track GameObjects
    private void SetAllTracksInactive()
    {
        if (availableTrackGameObjects == null) return;
        foreach (GameObject track in availableTrackGameObjects)
        {
            if (track != null) track.SetActive(false);
        }
    }

} 