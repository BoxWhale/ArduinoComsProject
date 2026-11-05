using UnityEngine;
using System.IO.Ports;
using TMPro;

/* JSON data structure received from Arduino via UART:
{
  "GAME_STATE": int,    // 0 = ACTIVE, 1 = ENDED
  "WIN_STATE": int,     // 0 = WON, 1 = LOST, 2 = NOT_CONCLUDED
  "TIME": int,          // Time in seconds
  "WRONG": bool,        // Show wrong visual
  "HOT": float,         // How close the player is to the correct answer, 0.0 = far, 1.0 = close, supposedly suggested for now
                        // The equation is supposedly Mathf.Exp(-Mathf.Pow(x - 42f, 2) / 400); where the x is the player's answer
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
    public int GAME_STATE;
    public int WIN_STATE;
    public int TIME;
    public bool WRONG;
    //public float HOT; // Ignored for now, not used in the current implementation of the arduino
    public int A; // 0 = OFF, 1 = ON [128]
    public int B; // 0 = OFF, 1 = ON [64]
    public int C; // 0 = OFF, 1 = ON [32]
    public int D; // 0 = OFF, 1 = ON [16]
    public int E; // 0 = OFF, 1 = ON [8]
    public int F; // 0 = OFF, 1 = ON [4]
    public int G; // 0 = OFF, 1 = ON [2]
    public int H; // 0 = OFF, 1 = ON [1]

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
    SerialPort serialPort = new SerialPort("COM3", 9600);
    public GameObject[] bitLigts; // Array to hold the bit light GameObjects, 8 lights for 8 bits
    public GameObject[] bitLigtsApprox; // Array to hold the bit light GameObjects, 3 lights for equation approximation
    public GameObject[] bitLightState; // Array to hold the bit light GameObjects, 2 lights for state of the game; Win or Lose
    public TextMeshProUGUI timeDisplay; // Reference to the TextMeshPro component for displaying Time
        public int timeChange; // variable to hold the previous time change value received from Arduino
    public TextMeshProUGUI gameStateDisplay; // Reference to the TextMeshPro component for displaying gamestate
    public GameObject Door;
    
    public int gameState = 0; // variable to hold the previous game state value received from Arduino, no use for now
    public int winState = 2; // variable to hold the previous win state value received from Arduino, 2 = NOT_CONCLUDED
    public int[] switchState = new int[8]{0, 0, 0, 0, 0, 0, 0, 0}; // variable to hold the previous switch state values received from Arduino, 8 switches for 8 bits
    
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
        serialPort.ReadTimeout = 10; // Prevents Unity from freezing to wait for data from Arduino
        serialPort.Open(); // Open the serial port to start communication with Arduino
        ResetLights();
    }

    void Update()
    {
        if (serialPort.IsOpen)
        {
            try
            {
                // Read the data coming from Arduino
                string data = serialPort.ReadLine();
                // Display the data in the Unity console for debugging purposes
                Debug.Log(data);
                // Parse the JSON data received from Arduino into the ArduinoData class
                ArduinoData arduinoData = JsonUtility.FromJson<ArduinoData>(data);
                
                // Redundant checks to see if the values have changed before updating the display, to avoid unnecessary updates
                if (arduinoData.TIME != timeChange)
                {
                    UpdateTimeDisplay(arduinoData.TIME);
                    timeChange = arduinoData.TIME; // Update the timeChange variable with the new value
                }
                if(arduinoData.GAME_STATE != gameState)
                {
                    UpdateGameState(arduinoData.GAME_STATE);
                    gameState = arduinoData.GAME_STATE; // Update the gameState variable with the new value
                }
                if(arduinoData.WIN_STATE != winState)
                {
                    UpdateWinState(arduinoData.WIN_STATE);
                    winState = arduinoData.WIN_STATE; // Update the winState variable with the new value
                }
                if(arduinoData.WRONG)
                {
                    // If the WRONG value is true, display the wrong visual text
                    gameStateDisplay.text = "WRONG!";
                }
                else
                {
                    // If the WRONG value is false, display the game state text
                    if (arduinoData.GAME_STATE == 0)
                    {
                        gameStateDisplay.text = "Playing";
                    }
                    else if (arduinoData.GAME_STATE == 1)
                    {
                        gameStateDisplay.text = "Session ended";
                    }
                }
                
                if(switchState != arduinoData.SwitchState)
                {
                    // Update the bit lights based on the switch state values received from Arduino
                    for (int i = 0; i < 8; i++)
                    {
                        // Update the bit light based on the switch state value, if the value is 1, turn on the light, if 0, turn off the light
                        bitLigts[i].GetComponent<Light>().enabled = arduinoData.SwitchState[i] == 1;
                    }
                    switchState = arduinoData.SwitchState; // Update the switchState variable with the new values
                }
            }
            catch (System.TimeoutException)
            {
                // No new data this frame, just ignore
            }
            catch (System.ArgumentException exception)
            {
                Debug.LogWarning("Could not parse UART JSON: " + exception.Message);
            }
        }
    }
    
    void UpdateGameState(int gameState)
    {
        // Update the game state actions depending on what can be implemeted with this data. For now, just display the game state in the console.
        if (gameState == 0)
        {
            Debug.Log("Game State: ACTIVE");
        }
        else if (gameState == 1)
        {
            Debug.Log("Game State: ENDED");
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
            Door.SetActive(false); // Open the door when the player wins
        }
        else if (winState == 1)
        {
            Debug.Log("Win State: LOST");
            bitLightState[0].GetComponent<Light>().enabled = false; // Turn off the "WON" light
            bitLightState[1].GetComponent<Light>().enabled = true; // Turn on the "LOST" light
            Door.SetActive(true); // Close the door when the player loses
        }
        else if (winState == 2)
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