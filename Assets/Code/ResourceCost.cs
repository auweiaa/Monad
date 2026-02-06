using System;
using UnityEngine;

[Serializable]
public struct ResourceCost
{
    [Min(0)] public int wood;
    [Min(0)] public int stone;
    [Min(0)] public int iron;
    [Min(0)] public int gold;

    public ResourceCost(int wood, int stone, int iron, int gold)
    {
        this.wood = Mathf.Max(0, wood);
        this.stone = Mathf.Max(0, stone);
        this.iron = Mathf.Max(0, iron);
        this.gold = Mathf.Max(0, gold);
    }

    public ResourceCost ClampNonNegative()
    {
        return new ResourceCost(
            Mathf.Max(0, wood),
            Mathf.Max(0, stone),
            Mathf.Max(0, iron),
            Mathf.Max(0, gold)
        );
    }

    public bool CanAfford(ResourceManager resourceManager)
    {
        if (resourceManager == null)
        {
            return false;
        }

        return resourceManager.GetWoodAmount() >= wood
            && resourceManager.GetStoneAmount() >= stone
            && resourceManager.GetIronAmount() >= iron
            && resourceManager.GetGoldAmount() >= gold;
    }
}


