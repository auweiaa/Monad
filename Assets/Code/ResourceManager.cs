using UnityEngine;
using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;

public class ResourceManager : MonoBehaviour
{
    [SerializeField] private int woodAmount = 0;
    [SerializeField] private int stoneAmount = 0;
    [SerializeField] private int ironAmount = 0;
    [SerializeField] private int goldAmount = 0;
    [SerializeField] private int score = 0;
    [SerializeField] private TextMeshProUGUI woodAmountText;
    [SerializeField] private TextMeshProUGUI stoneAmountText;
    [SerializeField] private TextMeshProUGUI ironAmountText;
    [SerializeField] private TextMeshProUGUI goldAmountText;
    [SerializeField] private TextMeshProUGUI scoreText;

    [Header("Pop animation")]
    [SerializeField] private float popScale = 1.35f;
    [SerializeField] private float popDuration = 0.2f;

    private Dictionary<TextMeshProUGUI, Coroutine> runningPopCoroutines = new Dictionary<TextMeshProUGUI, Coroutine>();
    private Dictionary<TextMeshProUGUI, Vector3> baseScales = new Dictionary<TextMeshProUGUI, Vector3>();

    public event Action ResourcesChanged;

    public int GetWoodAmount()
    {
        return woodAmount;
    }
    public int GetStoneAmount()
    {
        return stoneAmount;
    }
    public int GetIronAmount()
    {
        return ironAmount;
    }
    public int GetGoldAmount()
    {
        return goldAmount;
    }
    public void AddWoodAmount(int amount)
    {
        woodAmount += amount;
        UpdateUI();
        PlayPop(woodAmountText);
    }
    public void AddStoneAmount(int amount)
    {
        stoneAmount += amount;
        UpdateUI();
        PlayPop(stoneAmountText);
    }
    public void AddIronAmount(int amount)
    {
        ironAmount += amount;
        UpdateUI();
        PlayPop(ironAmountText);
    }
    public void AddGoldAmount(int amount)
    {
        goldAmount += amount;
        UpdateUI();
        PlayPop(goldAmountText);
    }

    public void AddScore(int amount)
    {
        score += amount;
        UpdateUI();
        PlayPop(scoreText);
    }

    public bool CanAfford(ResourceCost cost)
    {
        return woodAmount >= cost.wood
            && stoneAmount >= cost.stone
            && ironAmount >= cost.iron
            && goldAmount >= cost.gold;
    }

    public bool TrySpend(ResourceCost cost)
    {
        cost = cost.ClampNonNegative();
        if (!CanAfford(cost))
        {
            return false;
        }
        AddWoodAmount(cost.wood * -1);
        AddStoneAmount(cost.stone * -1);
        AddIronAmount(cost.iron * -1);
        AddGoldAmount(cost.gold * -1);
        return true;
    }

    private void SaveBaseScale(TextMeshProUGUI text)
    {
        if (text != null && !baseScales.ContainsKey(text))
        {
            baseScales[text] = text.rectTransform.localScale;
        }
    }

    void Start()
    {
        SaveBaseScale(woodAmountText);
        SaveBaseScale(stoneAmountText);
        SaveBaseScale(ironAmountText);
        SaveBaseScale(goldAmountText);
        SaveBaseScale(scoreText);
        
        UpdateUI();
    }

    void UpdateUI()
    {
        ResourcesChanged?.Invoke();
        if (woodAmountText != null) woodAmountText.text = woodAmount.ToString();
        if (stoneAmountText != null) stoneAmountText.text = stoneAmount.ToString();
        if (ironAmountText != null) ironAmountText.text = ironAmount.ToString();
        if (goldAmountText != null) goldAmountText.text = goldAmount.ToString();
        if (scoreText != null) scoreText.text = score.ToString();
    }

    private void PlayPop(TextMeshProUGUI text)
    {
        if (text == null || !baseScales.ContainsKey(text)) return;

        if (runningPopCoroutines.TryGetValue(text, out Coroutine running))
        {
            StopCoroutine(running);
        }

        text.rectTransform.localScale = baseScales[text];
        runningPopCoroutines[text] = StartCoroutine(PopText(text));
    }

    private IEnumerator PopText(TextMeshProUGUI text)
    {
        if (text == null || !baseScales.ContainsKey(text)) yield break;

        RectTransform rt = text.rectTransform;
        Vector3 originalScale = baseScales[text];
        Vector3 targetScale = originalScale * popScale;

        float elapsed = 0f;
        float halfDuration = popDuration * 0.5f;

        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            rt.localScale = Vector3.Lerp(originalScale, targetScale, elapsed / halfDuration);
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            rt.localScale = Vector3.Lerp(targetScale, originalScale, elapsed / halfDuration);
            yield return null;
        }

        rt.localScale = originalScale;
        runningPopCoroutines.Remove(text);
    }

    public void RefundHalf(ResourceCost cost)
    {
        cost = cost.ClampNonNegative();

        int woodRefund = Mathf.RoundToInt(cost.wood * 0.5f);
        int stoneRefund = Mathf.RoundToInt(cost.stone * 0.5f);
        int ironRefund = Mathf.RoundToInt(cost.iron * 0.5f);
        int goldRefund = Mathf.RoundToInt(cost.gold * 0.5f);

        // the Add-functions trigger a text pop up, so only call if a real change happened
        if (woodRefund > 0)
        {
            AddWoodAmount(woodRefund);
        }

        if (stoneRefund > 0)
        {
            AddStoneAmount(stoneRefund);
        }

        if (ironRefund > 0)
        {
            AddIronAmount(ironRefund);
        }

        if (goldRefund > 0)
        {
            AddGoldAmount(goldRefund);
        }
    }
}
