using UnityEngine;
using TMPro;

public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance { get; private set; }

    [Header("Level Settings")]
    public string levelName = "Level_1"; 
    public Transform playerTransform; 
    public float movementThreshold = 0.1f; 

    [Header("UI Components")]
    public TextMeshProUGUI timerText; 
    public TextMeshProUGUI recordText; 

    private float currentTime = 0f;
    private bool isTimerRunning = false;
    private bool hasStartedMoving = false;
    private Vector3 startingPosition;

    private void Awake()
    {
        // Set up the Singleton instance
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        if (playerTransform != null) startingPosition = playerTransform.position;
        
        float record = GetBestTime();
        recordText.text = record > 0f ? "PB: " + FormatTime(record) : "PB: --:--.--";
        timerText.text = FormatTime(0f);
    }

    void Update()
    {
        if (!hasStartedMoving && playerTransform != null)
        {
            if (Vector3.Distance(startingPosition, playerTransform.position) > movementThreshold)
            {
                isTimerRunning = true;
                hasStartedMoving = true;
            }
        }

        if (isTimerRunning)
        {
            currentTime += Time.deltaTime;
            timerText.text = FormatTime(currentTime);
        }
    }

    public void FinishLevel()
    {
        if (!isTimerRunning) return;
        isTimerRunning = false;

        bool isNewRecord = CheckAndSaveRecord(currentTime);
        if (isNewRecord)
        {
            timerText.color = Color.green;
            recordText.text = "New PB: " + FormatTime(currentTime);
        }
        
        // Open end-of-level UI menu here if you have one!
    }

    private bool CheckAndSaveRecord(float completionTime)
    {
        string recordKey = levelName + "_BestTime";
        float existingRecord = PlayerPrefs.GetFloat(recordKey, 0f);

        if (existingRecord == 0f || completionTime < existingRecord)
        {
            PlayerPrefs.SetFloat(recordKey, completionTime);
            PlayerPrefs.Save();
            return true;
        }
        return false;
    }

    public string FormatTime(float timeToFormat)
    {
        int minutes = Mathf.FloorToInt(timeToFormat / 60);
        int seconds = Mathf.FloorToInt(timeToFormat % 60);
        float fraction = (timeToFormat * 100) % 100;
        return string.Format("{0:00}:{1:00}.{2:00}", minutes, seconds, fraction);
    }
    
    public float GetBestTime() => PlayerPrefs.GetFloat(levelName + "_BestTime", 0f);
}
