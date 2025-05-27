using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class StockMarketController : MonoBehaviour
{
    [Header("Market Settings")]
    public float initialPrice = 50f;
    public int actionsPerDay = 24;
    public int simulationDays = 365; // 1 year
    public float timeSpeedMultiplier = 1f; // Speed up simulation
    
    [Header("Market Display")]
    public LineRenderer priceLineRenderer;
    public Text currentPriceText;
    public Text dayCountText;
    public Text marketStatusText;
    
    [Header("Market Mechanics")]
    public float baseVolatility = 0.02f; // 2% base random change
    public float demandSensitivity = 0.1f; // How much demand affects price
    public AnimationCurve marketNoiseCurve; // For realistic price movements
    
    // Private variables
    private float currentPrice;
    private int currentDay = 0;
    private int currentAction = 0;
    private bool isMarketActive = false;
    private bool isInitialMonth = true;
    
    // Price history for calculations
    private Queue<float> priceHistory = new Queue<float>();
    private Queue<float> last30DayPrices = new Queue<float>();
    private Queue<float> last50DayPrices = new Queue<float>();
    
    // Market data
    private List<Vector3> pricePoints = new List<Vector3>();
    private float intrinsicValue; // True worth of the stock (50-day average)
    
    // Player tracking
    private int totalPlayers = 100; // Default number of players
    private int momentumPlayers = 50; // x in the formula
    private int valuePlayers = 50; // total - momentum
    
    // Demand tracking
    private float currentDemand = 0f; // Positive = buying pressure, Negative = selling pressure
    
    void Start()
    {
        InitializeMarket();
        StartCoroutine(RunMarketSimulation());
    }
    
    void InitializeMarket()
    {
        currentPrice = initialPrice;
        intrinsicValue = initialPrice;
        
        // Initialize price history with starting price
        for (int i = 0; i < 50; i++)
        {
            priceHistory.Enqueue(currentPrice);
            last50DayPrices.Enqueue(currentPrice);
            if (i < 30)
            {
                last30DayPrices.Enqueue(currentPrice);
            }
        }
        
        UpdateUI();
        SetupPriceVisualization();
    }
    
    void SetupPriceVisualization()
    {
        if (priceLineRenderer != null)
        {
            priceLineRenderer.positionCount = 0;
            priceLineRenderer.startWidth = 0.1f;
            priceLineRenderer.endWidth = 0.1f;
            priceLineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            priceLineRenderer.color = Color.blue;
        }
    }
    
    IEnumerator RunMarketSimulation()
    {
        isMarketActive = true;
        
        while (currentDay < simulationDays)
        {
            // Run 24 actions per day, but only on weekly basis (every 7 days)
            if (currentDay % 7 == 0 || isInitialMonth)
            {
                for (int action = 0; action < actionsPerDay; action++)
                {
                    ProcessMarketAction();
                    currentAction++;
                    
                    // Wait between actions for visualization
                    yield return new WaitForSeconds(0.1f / timeSpeedMultiplier);
                }
            }
            
            currentDay++;
            
            // First month (30 days) has random initialization
            if (currentDay > 30)
            {
                isInitialMonth = false;
            }
            
            UpdatePriceHistory();
            UpdateUI();
            
            yield return new WaitForSeconds(0.5f / timeSpeedMultiplier);
        }
        
        isMarketActive = false;
        Debug.Log("Market simulation completed!");
    }
    
    void ProcessMarketAction()
    {
        float priceChange = 0f;
        
        if (isInitialMonth)
        {
            // Random price movements for first month
            priceChange = Random.Range(-baseVolatility, baseVolatility) * currentPrice;
            priceChange += Mathf.Sin(Time.time * 0.5f) * currentPrice * 0.01f; // Small trend component
        }
        else
        {
            // Calculate demand based on player strategies
            CalculateMarketDemand();
            
            // Apply law of demand: higher demand = higher price
            float demandEffect = currentDemand * demandSensitivity * currentPrice;
            
            // Add some randomness (market noise)
            float randomEffect = (Random.value - 0.5f) * 2f * baseVolatility * currentPrice;
            
            // Combine effects
            priceChange = demandEffect + randomEffect;
            
            // Apply market efficiency (prices tend toward intrinsic value over time)
            float efficiencyPull = (intrinsicValue - currentPrice) * 0.001f;
            priceChange += efficiencyPull;
        }
        
        // Apply price change
        currentPrice += priceChange;
        
        // Prevent negative prices
        currentPrice = Mathf.Max(currentPrice, 0.01f);
        
        // Update visualization
        UpdatePriceVisualization();
    }
    
    void CalculateMarketDemand()
    {
        currentDemand = 0f;
        
        // Calculate momentum strategy demand
        float momentumDemand = CalculateMomentumDemand();
        
        // Calculate value strategy demand
        float valueDemand = CalculateValueDemand();
        
        // Weight by number of players using each strategy
        float totalDemand = (momentumDemand * momentumPlayers + valueDemand * valuePlayers) / totalPlayers;
        
        currentDemand = totalDemand;
        
        // Apply strategy effectiveness reduction (if too many use same strategy)
        ApplyStrategyEffectivenessReduction();
    }
    
    float CalculateMomentumDemand()
    {
        if (priceHistory.Count < 10) return 0f;
        
        // Check for 10% increase trend
        float[] recentPrices = new float[10];
        priceHistory.ToArray().CopyTo(recentPrices, priceHistory.Count - 10);
        
        float startPrice = recentPrices[0];
        float currentChangePercent = (currentPrice - startPrice) / startPrice * 100f;
        
        // Check if no drop > 5% occurred in between
        bool validTrend = true;
        float maxPrice = startPrice;
        for (int i = 1; i < recentPrices.Length; i++)
        {
            if (recentPrices[i] > maxPrice) maxPrice = recentPrices[i];
            float dropFromMax = (maxPrice - recentPrices[i]) / maxPrice * 100f;
            if (dropFromMax > 5f)
            {
                validTrend = false;
                break;
            }
        }
        
        // Buy signal: 10% increase with valid trend
        if (currentChangePercent >= 10f && validTrend)
        {
            return 1f; // Strong buy signal
        }
        
        return 0f; // No action
    }
    
    float CalculateValueDemand()
    {
        if (last30DayPrices.Count < 30) return 0f;
        
        // Calculate 30-day moving average (buy threshold)
        float sum30 = 0f;
        foreach (float price in last30DayPrices)
        {
            sum30 += price;
        }
        float movingAverage30 = sum30 / last30DayPrices.Count;
        
        // Buy signal: current price below 30-day average
        if (currentPrice < movingAverage30)
        {
            float undervaluedPercent = (movingAverage30 - currentPrice) / movingAverage30;
            return undervaluedPercent * 2f; // Stronger signal for more undervalued stocks
        }
        
        return 0f; // No action
    }
    
    void ApplyStrategyEffectivenessReduction()
    {
        // fm(x) = mx + c for momentum (m negative)
        // fv(x) = mx + c for value (m positive when more momentum players)
        
        float momentumRatio = (float)momentumPlayers / totalPlayers;
        
        // Reduce momentum effectiveness as more players use it
        if (momentumRatio > 0.5f)
        {
            float reductionFactor = 1f - (momentumRatio - 0.5f);
            currentDemand *= reductionFactor;
        }
        
        // Increase value effectiveness when more players use momentum
        if (momentumRatio > 0.3f)
        {
            float valueBonusFactor = 1f + (momentumRatio - 0.3f) * 0.5f;
            if (currentDemand < 0) // Value buying
            {
                currentDemand *= valueBonusFactor;
            }
        }
    }
    
    void UpdatePriceHistory()
    {
        priceHistory.Enqueue(currentPrice);
        if (priceHistory.Count > 365) // Keep 1 year of data
        {
            priceHistory.Dequeue();
        }
        
        // Update moving averages
        if (currentDay > 0)
        {
            last30DayPrices.Enqueue(currentPrice);
            if (last30DayPrices.Count > 30)
            {
                last30DayPrices.Dequeue();
            }
            
            last50DayPrices.Enqueue(currentPrice);
            if (last50DayPrices.Count > 50)
            {
                last50DayPrices.Dequeue();
            }
            
            // Update intrinsic value (50-day average)
            if (last50DayPrices.Count == 50)
            {
                float sum = 0f;
                foreach (float price in last50DayPrices)
                {
                    sum += price;
                }
                intrinsicValue = sum / 50f;
            }
        }
    }
    
    void UpdatePriceVisualization()
    {
        if (priceLineRenderer == null) return;
        
        // Add new point to visualization
        Vector3 newPoint = new Vector3(currentDay * 0.1f, currentPrice * 0.1f, 0f);
        pricePoints.Add(newPoint);
        
        // Update line renderer
        priceLineRenderer.positionCount = pricePoints.Count;
        priceLineRenderer.SetPositions(pricePoints.ToArray());
    }
    
    void UpdateUI()
    {
        if (currentPriceText != null)
        {
            currentPriceText.text = $"Price: ${currentPrice:F2}";
        }
        
        if (dayCountText != null)
        {
            dayCountText.text = $"Day: {currentDay}/{simulationDays}";
        }
        
        if (marketStatusText != null)
        {
            string status = isInitialMonth ? "Initial Period" : "Active Trading";
            marketStatusText.text = $"Status: {status}\nDemand: {currentDemand:F3}\nIntrinsic: ${intrinsicValue:F2}";
        }
    }
    
    // Public methods for external control
    public void SetPlayerRatio(int momentum, int value)
    {
        momentumPlayers = momentum;
        valuePlayers = value;
        totalPlayers = momentum + value;
    }
    
    public float GetCurrentPrice()
    {
        return currentPrice;
    }
    
    public float GetIntrinsicValue()
    {
        return intrinsicValue;
    }
    
    public int GetCurrentDay()
    {
        return currentDay;
    }
    
    public bool IsMarketActive()
    {
        return isMarketActive;
    }
}
