using UnityEngine;

public class PlacedTower : MonoBehaviour
{
    [SerializeField] private TowerData towerData;
    [SerializeField] private Vector3Int originCell;
    [SerializeField] private Vector2Int footprint;

    public TowerData TowerData => towerData;
    public Vector3Int OriginCell => originCell;
    public Vector2Int Footprint => footprint;

    public void Initialize(TowerData data, Vector3Int origin, Vector2Int footprint)
    {
        towerData = data;
        originCell = origin;
        this.footprint = footprint;
    }
}




