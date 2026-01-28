using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Rhinotap.Toolkit;

public class GuiManager : Singleton<GuiManager>
{
    

    [SerializeField]
    private Image XpBar;

    [Header("Growth Icons")]
    [SerializeField]
    private Image[] growthIcons;
    [SerializeField]
    private Color completedColor = Color.white;
    [SerializeField]
    private Color currentColor = new Color(1f, 1f, 1f, 1f); // Full white
    [SerializeField]
    private Color lockedColor = new Color(0f, 0f, 0f, 0.5f); // Semi-transparent black


    [SerializeField]
    private GameObject pauseBtn;
    [SerializeField]
    private GameObject resumeBtn;
    [SerializeField]
    private GameObject pausedBg;

    [SerializeField]
    private GameObject ScoreScreen;
    [Header("Game Over Messages")]
    [SerializeField]
    [TextArea]
    private string victoryMessage = "GbGrsaTr Gñk)anrYcCIvitkñúgvKÁenH";
    [SerializeField]
    [TextArea]
    private string defeatMessage = "BüayammþgeTot";
    
    [SerializeField]
    private Font messageFont;

    [SerializeField]
    private Text ScoreText;
    private Text messageText;

    // Start is called before the first frame update
    void Start()
    {
        // Setup Message Text (Clone ScoreText)
        if (messageText == null && ScoreText != null)
        {
            GameObject msgObj = Instantiate(ScoreText.gameObject, ScoreText.transform.parent);
            msgObj.name = "MessageText";
            messageText = msgObj.GetComponent<Text>();
            
            if (messageFont != null)
            {
                messageText.font = messageFont;
            }

            messageText.text = "";
            // Optimize for long text
            messageText.resizeTextForBestFit = true;
            messageText.resizeTextMinSize = 10;
            messageText.resizeTextMaxSize = 60;
            messageText.alignment = TextAnchor.MiddleCenter;
            
            // Center the message text in the screen (Fill Parent)
            RectTransform rt = messageText.GetComponent<RectTransform>();
            if (rt != null)
            {
                 rt.anchorMin = Vector2.zero;
                 rt.anchorMax = Vector2.one;
                 rt.sizeDelta = Vector2.zero; 
                 rt.anchoredPosition = Vector2.zero;
            }

            msgObj.SetActive(false);
        }

        EventManager.StartListening("GameWin", () => {
             ShowGameMessage(victoryMessage);
        });

        EventManager.StartListening("GameLoss", () => {
             ShowGameMessage(defeatMessage);
        });

        EventManager.StartListening("GameStart", () => {
            SetXp(0, 1);
            HideScore();
            UpdateGrowthIcons(1); // Reset icons to level 1
        });
        

        EventManager.StartListening<bool>("gamePaused", (isPaused) => {
            TogglePauseBtn();
        });


        EventManager.StartListening<int>("GameOver", (score) => {
            ShowScore(score);
        });

        EventManager.StartListening<int>("onLevelUp", (level) => {
            UpdateGrowthIcons(level);
        });

        // Ensure layout is fixed at start
        FixPauseLayout();
        UpdateGrowthIcons(1); // Initial state
        
        // Ensure XP bar starts empty
        if (XpBar != null)
        {
             if (XpBar.type != Image.Type.Filled)
             {
                 XpBar.type = Image.Type.Filled;
                 XpBar.fillMethod = Image.FillMethod.Horizontal;
             }
             XpBar.fillAmount = 0f;
        }
    }

    private void FixPauseLayout()
    {
        // Fix Paused BG layout: Ensure it covers the entire screen and has no rounded corners
        if (pausedBg != null)
        {
            RectTransform rt = pausedBg.GetComponent<RectTransform>();
            if (rt != null)
            {
                // Stretch to fill parent (Full Screen)
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.pivot = new Vector2(0.5f, 0.5f);
                
                // Oversize it slightly to cover any potential edges/margins
                rt.offsetMin = new Vector2(-50f, -50f);
                rt.offsetMax = new Vector2(50f, 50f);
            }

            Image img = pausedBg.GetComponent<Image>();
            if (img != null)
            {
                // Remove sprite to eliminate border radius/rounded corners
                img.sprite = null; 
                // Ensure it's a black semi-transparent overlay
                img.color = new Color(0f, 0f, 0f, 0.4f);
            }
        }
    }

    public void SetXp(int currentXP, int maxXp, int currentLevel = 1, int maxLevels = 1)
    {
        if (XpBar == null) return;
        
        // Calculate progress within current level (0 to 1)
        float levelProgress = (float)currentXP / (float)maxXp;

        // Visual-Based Progress Logic
        // We want the bar to fill from the "Start Icon" of the current level to the "End Icon" of the current level.
        // Start Icon = Icon for currentLevel (or start of bar if level 1)
        // End Icon = Icon for currentLevel + 1

        // Assumptions:
        // growthIcons[0] corresponds to Level 1 Start? Or Level 1 End?
        // Usually in these games:
        // [Icon 1] ----------- [Icon 2]
        // Level 1 progress fills the gap between Icon 1 and Icon 2.
        
        // Let's assume growthIcons is ordered: Icon 0 (Lvl 1), Icon 1 (Lvl 2), etc.
        // If we are Level 1: We go from Icon 0 to Icon 1.
        // If we are Level 2: We go from Icon 1 to Icon 2.

        if (growthIcons != null && growthIcons.Length > 0)
        {
            // Determine Start Position
            float startPos = 0f;
            int startIdx = currentLevel - 1;
            
            // FIX: For Level 1, start at 0 (empty bar) instead of the first icon position
            if (currentLevel == 1)
            {
                startPos = 0f;
            }
            else if (startIdx >= 0 && startIdx < growthIcons.Length)
            {
                startPos = GetNormalizedPosition(growthIcons[startIdx].rectTransform);
            }
            else if (startIdx >= growthIcons.Length)
            {
                // If we are past the last icon, start from the last icon's position
                startPos = GetNormalizedPosition(growthIcons[growthIcons.Length - 1].rectTransform);
            }

            // Determine End Position
            float endPos = 1f; // Default to full bar if no next icon
            int endIdx = currentLevel;

            if (endIdx >= 0 && endIdx < growthIcons.Length)
            {
                endPos = GetNormalizedPosition(growthIcons[endIdx].rectTransform);
            }

            // REMOVED: Early return for max level so we can see progress through the final level
            // if (currentLevel >= maxLevels) ...

            // Interpolate
            float finalFill = Mathf.Lerp(startPos, endPos, levelProgress);
            XpBar.fillAmount = finalFill;
        }
        else
        {
            // Fallback to simple math if icons are missing
            float segmentSize = 1f / (float)maxLevels;
            float totalProgress = ((currentLevel - 1) * segmentSize) + (levelProgress * segmentSize);
            XpBar.fillAmount = totalProgress;
        }
    }

    private float GetNormalizedPosition(RectTransform target)
    {
        if (XpBar == null || target == null) return 0f;
        
        // We need the position relative to the XpBar's width.
        // Assuming XpBar is a Filled Image that spans the full width of the container area.
        // We can compare world X positions.
        
        RectTransform barRect = XpBar.rectTransform;
        
        // Get World Corners of the bar to find the start (left) and width
        Vector3[] corners = new Vector3[4];
        barRect.GetWorldCorners(corners);
        // corners[0] is bottom-left
        // corners[2] is top-right
        
        float startX = corners[0].x;
        float totalWidth = corners[2].x - corners[0].x;
        
        if (totalWidth <= 0) return 0f;

        float targetX = target.position.x;
        
        // Calculate normalized position (0 to 1)
        float normalized = (targetX - startX) / totalWidth;
        
        return Mathf.Clamp01(normalized);
    }

    private void UpdateGrowthIcons(int currentLevel)
    {
        if (growthIcons == null || growthIcons.Length == 0) return;

        for (int i = 0; i < growthIcons.Length; i++)
        {
            if (growthIcons[i] == null) continue;

            // Icons are 0-indexed, Levels are 1-indexed.
            // Icon 0 = Level 1, Icon 1 = Level 2, etc.
            int iconLevel = i + 1;

            if (iconLevel < currentLevel)
            {
                // Past levels (Completed) - Maybe dim them or keep them full color?
                // Let's keep them full color to show progress
                growthIcons[i].color = completedColor;
                // Optional: Make them smaller or transparent?
                // For now, let's keep them fully visible as "trophies"
            }
            else if (iconLevel == currentLevel)
            {
                // Current Level - Highlighted
                growthIcons[i].color = currentColor;
                // Optional: Scale up slightly?
                growthIcons[i].transform.localScale = Vector3.one * 1.2f; 
            }
            else
            {
                // Future Levels - Locked/Black
                growthIcons[i].color = lockedColor;
                growthIcons[i].transform.localScale = Vector3.one;
            }
            
            // Reset scale for non-current levels
            if(iconLevel != currentLevel)
            {
                 growthIcons[i].transform.localScale = Vector3.one;
            }
        }
    }

    private void TogglePauseBtn()
    {
        if( pauseBtn == null || resumeBtn == null)
        {
            Debug.Log("Missing pause/resume btns");
            return;
        }

        
        if( pauseBtn.activeSelf == true)
        {
            //game paused
            pauseBtn.SetActive(false);
            resumeBtn.SetActive(true);
            pausedBg.SetActive(true);
            
            // Re-apply layout fix to ensure it stays correct
            FixPauseLayout();

            // Ensure Resume button is on top of the background
            resumeBtn.transform.SetAsLastSibling();
        }else
        {
            //Resume
            pauseBtn.SetActive(true);
            resumeBtn.SetActive(false);
            pausedBg.SetActive(false);
        }
    }


    private void ShowScore(int score = 0)
    {
        if (ScoreScreen == null || ScoreText == null) return;
        ScoreScreen.SetActive(true);
        // Hide score text as per user request (use message instead)
        ScoreText.gameObject.SetActive(false);
        // ScoreText.text = score.ToString(); 
    }

    private void ShowGameMessage(string message)
    {
        if (ScoreScreen == null) return;
        ScoreScreen.SetActive(true);
        
        // Hide score text as per user request (Hide point text for now)
        if (ScoreText != null) ScoreText.gameObject.SetActive(false);
        
        if (messageText != null)
        {
            messageText.text = message;
            messageText.gameObject.SetActive(true);
        }
    }

    private void HideScore()
    {
        if (ScoreScreen == null || ScoreText == null) return;
        ScoreScreen.SetActive(false);
        ScoreText.text = "0";
        if (messageText != null) messageText.gameObject.SetActive(false);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
