using UnityEngine;
using System.IO.Ports;
using TMPro;

[System.Serializable]
public class ArduinoData
{
    public int TIME;
}

public class GameHandler : MonoBehaviour
{
    SerialPort serialPort = new SerialPort("COM3", 9600);
    public GameObject[] bitLigts; // Array to hold the bit light GameObjects, 8 lights for 8 bits
    public GameObject[] bitLigtsApprox; // Array to hold the bit light GameObjects, 3 lights for equation approximation
    public GameObject[] bitLightState; // Array to hold the bit light GameObjects, 2 lights for state of the game; Win or Lose
    public TextMeshProUGUI timeDisplay; // Reference to the TextMeshPro component for displaying Time
        public int timeChange; // variable to hold the time change value received from Arduino
    public TextMeshProUGUI gameStateDisplay; // Reference to the TextMeshPro component for displaying gamestate
    public GameObject Door;
    
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
                if (arduinoData.TIME != timeChange)
                {
                    UpdateTimeDisplay(arduinoData.TIME);
                    timeChange = arduinoData.TIME; // Update the timeChange variable with the new value
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