using UnityEngine;
using System.IO.Ports;
using TMPro;

/* JSON data structure received from Arduino via UART:
{
  "GAME_STATE": int,    // 0 = ACTIVE, 1 = ENDED
  "WIN_STATE": int,     // 0 = WON, 1 = LOST, 2 = NOT_CONCLUDED
  "TIME": int,          // Time in seconds
  "WRONG": bool,        // Show wrong visual
  "PROX_1": int,        // 0 = OFF, 1 = ON
  "PROX_2": int,        // 0 = OFF, 1 = ON
  "PROX_3": int,        // 0 = OFF, 1 = ON
  "A": int,             // 0 = OFF, 1 = ON [128]
  "B": int,             // 0 = OFF, 1 = ON [64]
  "C": int,             // 0 = OFF, 1 = ON [32]
  "D": int,             // 0 = OFF, 1 = ON [16]
  "E": int,             // 0 = OFF, 1 = ON [8]
  "F": int,             // 0 = OFF, 1 = ON [4]
  "G": int,             // 0 = OFF, 1 = ON [2]
  "H": int,             // 0 = OFF, 1 = ON [1]
}
*/

[System.Serializable]
public class ArduinoData
{
    public int type;
    public int GAME_STATE;
    public int WIN_STATE;
    public int TIME;
    public bool WRONG;
    public int PROX_1; // 0 = OFF, 1 = ON
    public int PROX_2; // 0 = OFF, 1 = ON
    public int PROX_3; // 0 = OFF, 1 = ON
    public int A; // 0 = OFF, 1 = ON [128]
    public int B; // 0 = OFF, 1 = ON [64]
    public int C; // 0 = OFF, 1 = ON [32]
    public int D; // 0 = OFF, 1 = ON [16]
    public int E; // 0 = OFF, 1 = ON [8]
    public int F; // 0 = OFF, 1 = ON [4]
    public int G; // 0 = OFF, 1 = ON [2]
    public int H; // 0 = OFF, 1 = ON [1]
    
    // Combines the three proximity sensor values into one array
    public int[] ProximityState
    {
        get
        {
            return new int[] { PROX_1, PROX_2, PROX_3 };
        }
    }
    // Combines the eight switch values into one array
    public int[] SwitchState
    {
        get
        {
            return new int[] { A, B, C, D, E, F, G, H };
        }
    }
}

public class GameHandler : MonoBehaviour
{
    SerialPort serialPort = new SerialPort("COM5", 115200); // Set the correct COM port and baud rate for your Arduino
    public GameObject[] bitLigts; // Array to hold the bit light GameObjects, 8 lights for 8 bits
    public GameObject[] bitLigtsApprox; // Array to hold the bit light GameObjects, 3 lights for equation approximation
    public GameObject[] bitLightState; // Array to hold the bit light GameObjects, 2 lights for state of the game; Win or Lose
    public TextMeshProUGUI timeDisplay; // Reference to the TextMeshPro component for displaying Time
    public int timeChange = -1; // variable to hold the previous time change value received from Arduino
    public TextMeshProUGUI gameStateDisplay; // Reference to the TextMeshPro component for displaying gamestate
    public GameObject Door;
    
    // public int gameState = 0; // variable to hold the previous game state value received from Arduino, no use for now redundant.
    // public int winState = 2; // variable to hold the previous win state value received from Arduino, 2 = NOT_CONCLUDED redundant.
    public int[] switchState = new int[8]{0, 0, 0, 0, 0, 0, 0, 0}; // variable to hold the previous switch state values received from Arduino, 8 switches for 8 bits
    public int[] proximityState = new int[3]{0, 0, 0}; // variable to hold the previous proximity state values received from Arduino, 3 proximity sensors for equation approximation
    
    void ResetLights()
    {
        // Reset all bit lights to off state
        foreach (GameObject light in bitLigts)
        {
            light.GetComponent<Light>().enabled = false;
        }
        foreach (GameObject light in bitLigtsApprox)
        {
            light.GetComponent<Light>().enabled = false;
        }
        foreach (GameObject light in bitLightState)
        {
            light.GetComponent<Light>().enabled = false;
        }
    }
    void Start()
    {
        serialPort.DtrEnable = true; // Required for Uno R4 Native USB
        serialPort.RtsEnable = true;
        serialPort.ReadTimeout = 20; // Prevents Unity from freezing to wait for data from Arduino
        serialPort.Open(); // Open the serial port to start communication with Arduino
        ResetLights();
    }

    void Update()
    {
        if (!serialPort.IsOpen) return;

        // Process all complete messages currently available in the buffer.
        while (serialPort.BytesToRead > 0)
        {
            try
            {
                string data = serialPort.ReadLine().Trim();
                Debug.Log("UART: " + data);

                ArduinoData arduinoData = JsonUtility.FromJson<ArduinoData>(data);

                switch (arduinoData.type)
                {
                    case 1:
                        if (arduinoData.WRONG)
                        {
                            gameStateDisplay.text = "WRONG!";
                            UpdateGameState(2, arduinoData.WIN_STATE);
                        }
                        else
                        {
                            UpdateGameState(arduinoData.GAME_STATE, arduinoData.WIN_STATE);
                        }
                        break;

                    case 2:
                        if (arduinoData.TIME != timeChange)
                        {
                            UpdateTimeDisplay(arduinoData.TIME);
                            timeChange = arduinoData.TIME;
                        }
                        break;

                    case 3:
                        int[] switchValues = arduinoData.SwitchState;
                        if (!AreStatesEqual(switchState, switchValues))
                        {
                            for (int i = 0; i < switchValues.Length; i++)
                            {
                                bitLigts[i].GetComponent<Light>().enabled = switchValues[i] == 1;
                            }
                            switchState = switchValues;
                        }
                        break;

                    case 4:
                        int[] proximityValues = arduinoData.ProximityState;
                        if (!AreStatesEqual(proximityState, proximityValues))
                        {
                            for (int i = 0; i < proximityValues.Length; i++)
                            {
                                bitLigtsApprox[i].GetComponent<Light>().enabled = proximityValues[i] == 1;
                            }
                            proximityState = proximityValues;
                        }
                        break;
                }
            }
            catch (System.TimeoutException)
            {
                break; // The message is not complete yet; try again next frame.
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning("UART Parsing Error: " + exception.Message);
            }
        }
    }

    bool AreStatesEqual(int[] currentState, int[] receivedState)
    {
        if (currentState == null || receivedState == null || currentState.Length != receivedState.Length)
        {
            return false;
        }

        for (int i = 0; i < currentState.Length; i++)
        {
            if (currentState[i] != receivedState[i])
            {
                return false;
            }
        }

        return true;
    }
    
    void UpdateGameState(int gameState, int winState)
    {
        if (winState != 2)
        {
            UpdateWinState(winState);
            return; // If the win state is not NOT_CONCLUDED, we don't need to update the game state further
        }
        if (gameState == 0)
        {
            gameStateDisplay.text = "Waiting for responds"; // Display the active game state text
        }
        else if (gameState == 1)
        {
            gameStateDisplay.text = "Game Ended"; // Display the ended game state text, this is in case that winstate fails to get updated.
        }
        else if (gameState == 2)
        {
            gameStateDisplay.text = "Wrong!"; // Display the wrong game state text
        }

    }
    
    void UpdateWinState(int winState)
    {
        // Update the win state actions depending on what can be implemeted with this data. For now, just display the win state in the console.
        if (winState == 0)
        {
            Debug.Log("Win State: WON");
            bitLightState[0].GetComponent<Light>().enabled = true; // Turn on the "WON" light
            bitLightState[1].GetComponent<Light>().enabled = false; // Turn off the "LOST" light
            gameStateDisplay.text = "YOU WIN!"; // Display the win message
            Door.SetActive(false); // Open the door when the player wins
        }
        else if (winState == 1)
        {
            Debug.Log("Win State: LOST");
            bitLightState[0].GetComponent<Light>().enabled = false; // Turn off the "WON" light
            bitLightState[1].GetComponent<Light>().enabled = true; // Turn on the "LOST" light
            gameStateDisplay.text = "YOU LOSE!"; // Display the lose message
            // The door always remains closed in active play state
            //Door.SetActive(true); // Close the door when the player loses
        }
        else if (winState == 2) // supposedly won't activate, as this is the default state, but just in case, we will turn off both lights.
        {
            Debug.Log("Win State: NOT_CONCLUDED");
            bitLightState[0].GetComponent<Light>().enabled = false; // Turn off the "WON" light
            bitLightState[1].GetComponent<Light>().enabled = false; // Turn off the "LOST" light
        }
    }
    
    
    void UpdateTimeDisplay(int time)
    {
        // Update the time display with the new time value
        timeDisplay.text = "Time:\n" + time + "s";
    }

    void OnApplicationQuit()
    {
        // Close the port when you stop playing so it doesn't get stuck
        serialPort.Close();
    }
}