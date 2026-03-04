using UnityEngine;
using TMPro;
using System;
using System.Collections;

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
    public void SetWoodAmount(int amount)
    {
        woodAmount += amount;
        updateUI();
        if (woodAmountText != null) StartCoroutine(PopText(woodAmountText));
    }
    public void SetStoneAmount(int amount)
    {
        stoneAmount += amount;
        updateUI();
        if (stoneAmountText != null) StartCoroutine(PopText(stoneAmountText));
    }
    public void SetIronAmount(int amount)
    {
        ironAmount += amount;
        updateUI();
        if (ironAmountText != null) StartCoroutine(PopText(ironAmountText));
    }
    public void SetGoldAmount(int amount)
    {
        goldAmount += amount;
        updateUI();
        if (goldAmountText != null) StartCoroutine(PopText(goldAmountText));
    }

    public void SetScore(int amount)
    {
        score += amount;
        updateUI();
        if (scoreText != null) StartCoroutine(PopText(scoreText));
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
        SetWoodAmount(cost.wood * -1);
        SetStoneAmount(cost.stone * -1);
        SetIronAmount(cost.iron * -1);
        SetGoldAmount(cost.gold * -1);
        return true;
    }
    void Start()
    {
        updateUI();
    }

    void updateUI()
    {
        ResourcesChanged?.Invoke();
        if (woodAmountText != null) woodAmountText.text = woodAmount.ToString();
        if (stoneAmountText != null) stoneAmountText.text = stoneAmount.ToString();
        if (ironAmountText != null) ironAmountText.text = ironAmount.ToString();
        if (goldAmountText != null) goldAmountText.text = goldAmount.ToString();
        if (scoreText != null) scoreText.text = score.ToString();
    }

    private IEnumerator PopText(TextMeshProUGUI text)
    {
        if (text == null) yield break;

        RectTransform rt = text.rectTransform;
        Vector3 originalScale = rt.localScale;
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
    }
}
