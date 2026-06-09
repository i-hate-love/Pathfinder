using UnityEngine;

public class GridNode
{
    public bool walkable;
    public Vector3 worldPosition;
    public int gridX;
    public int gridY;

    public float clearanceRadius;

    public int gCost;
    public int hCost;
    public int movementPenalty;
    public GridNode parent;

    public int fCost => gCost + hCost + movementPenalty;

    public GridNode(bool walkable, Vector3 worldPosition, int gridX, int gridY, float clearanceRadius, int movementPenalty = 0)
    {
        this.walkable = walkable;
        this.worldPosition = worldPosition;
        this.gridX = gridX;
        this.gridY = gridY;
        this.clearanceRadius = clearanceRadius;
        this.movementPenalty = movementPenalty;
    }
}