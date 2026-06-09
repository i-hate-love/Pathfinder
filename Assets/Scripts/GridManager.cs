using System.Collections.Generic;
using UnityEngine;

public class GridManager : MonoBehaviour
{
    public static GridManager Instance;

    [Header("Grid")]
    public LayerMask unwalkableMask;
    public Vector2 gridWorldSize = new Vector2(50, 50);
    public float nodeRadius = 0.5f;
    public float obstacleCheckHeight = 2f;
    public bool preventCornerCutting = true;

    [Header("Clearance")]
    public float clearanceProbeStep = 0.1f;
    public float maxClearanceRadius = 3f;
    public float clearanceHeight = 1.2f;

    [Header("Debug")]
    public bool drawGridGizmos = true;
    public bool drawClearanceText = false;

    private GridNode[,] grid;
    private float nodeDiameter;
    private int gridSizeX;
    private int gridSizeY;
    private Vector3 worldBottomLeft;

    public int GridSizeX => gridSizeX;
    public int GridSizeY => gridSizeY;
    public float NodeDiameter => nodeDiameter;

    void Awake()
    {
        Instance = this;
        nodeDiameter = nodeRadius * 2f;
        gridSizeX = Mathf.Max(1, Mathf.RoundToInt(gridWorldSize.x / nodeDiameter));
        gridSizeY = Mathf.Max(1, Mathf.RoundToInt(gridWorldSize.y / nodeDiameter));
        CreateGrid();
    }

    public void CreateGrid()
    {
        grid = new GridNode[gridSizeX, gridSizeY];
        worldBottomLeft =
            transform.position
            - Vector3.right * gridWorldSize.x * 0.5f
            - Vector3.forward * gridWorldSize.y * 0.5f;

        for (int x = 0; x < gridSizeX; x++)
        {
            for (int y = 0; y < gridSizeY; y++)
            {
                BuildOrUpdateNode(x, y);
            }
        }
    }

    public void UpdateGrid()
    {
        CreateGrid();
    }

    public void UpdateGrid(Bounds bounds)
    {
        if (grid == null)
        {
            CreateGrid();
            return;
        }

        Vector3 min = bounds.min;
        Vector3 max = bounds.max;

        int minX = Mathf.Clamp(WorldToGridX(min.x - maxClearanceRadius), 0, gridSizeX - 1);
        int maxX = Mathf.Clamp(WorldToGridX(max.x + maxClearanceRadius), 0, gridSizeX - 1);
        int minY = Mathf.Clamp(WorldToGridY(min.z - maxClearanceRadius), 0, gridSizeY - 1);
        int maxY = Mathf.Clamp(WorldToGridY(max.z + maxClearanceRadius), 0, gridSizeY - 1);

        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                BuildOrUpdateNode(x, y);
            }
        }
    }

    void BuildOrUpdateNode(int x, int y)
    {
        Vector3 worldPoint = GetWorldPoint(x, y);

        bool pointWalkable = !Physics.CheckBox(
            worldPoint + Vector3.up * obstacleCheckHeight * 0.5f,
            new Vector3(nodeRadius * 0.9f, obstacleCheckHeight * 0.5f, nodeRadius * 0.9f),
            Quaternion.identity,
            unwalkableMask
        );

        float clearance = pointWalkable ? CalculateClearanceRadius(worldPoint) : 0f;

        if (grid[x, y] == null)
            grid[x, y] = new GridNode(pointWalkable, worldPoint, x, y, clearance);
        else
        {
            grid[x, y].walkable = pointWalkable;
            grid[x, y].worldPosition = worldPoint;
            grid[x, y].gridX = x;
            grid[x, y].gridY = y;
            grid[x, y].clearanceRadius = clearance;
            grid[x, y].parent = null;
            grid[x, y].gCost = 0;
            grid[x, y].hCost = 0;
        }
    }

    float CalculateClearanceRadius(Vector3 worldPoint)
    {
        float lastValid = 0f;
        Vector3 center = worldPoint + Vector3.up * clearanceHeight * 0.5f;

        for (float r = clearanceProbeStep; r <= maxClearanceRadius; r += clearanceProbeStep)
        {
            bool blocked = Physics.CheckSphere(center, r, unwalkableMask);
            if (blocked)
                break;

            lastValid = r;
        }

        return lastValid;
    }

    public Vector3 GetWorldPoint(int x, int y)
    {
        return worldBottomLeft
             + Vector3.right * (x * nodeDiameter + nodeRadius)
             + Vector3.forward * (y * nodeDiameter + nodeRadius);
    }

    int WorldToGridX(float worldX)
    {
        float percentX = (worldX - worldBottomLeft.x) / gridWorldSize.x;
        return Mathf.RoundToInt((gridSizeX - 1) * Mathf.Clamp01(percentX));
    }

    int WorldToGridY(float worldZ)
    {
        float percentY = (worldZ - worldBottomLeft.z) / gridWorldSize.y;
        return Mathf.RoundToInt((gridSizeY - 1) * Mathf.Clamp01(percentY));
    }

    public GridNode NodeFromWorldPoint(Vector3 worldPosition)
    {
        int x = WorldToGridX(worldPosition.x);
        int y = WorldToGridY(worldPosition.z);
        return grid[x, y];
    }

    public bool TryGetNode(int x, int y, out GridNode node)
    {
        if (x >= 0 && x < gridSizeX && y >= 0 && y < gridSizeY)
        {
            node = grid[x, y];
            return true;
        }

        node = null;
        return false;
    }

    public bool IsNodeTraversableForRadius(GridNode node, float agentRadius)
    {
        return node != null && node.walkable && node.clearanceRadius >= agentRadius;
    }

    public GridNode GetClosestTraversableNode(Vector3 worldPosition, float agentRadius, int maxSearchRadius = 8)
    {
        GridNode center = NodeFromWorldPoint(worldPosition);
        if (IsNodeTraversableForRadius(center, agentRadius))
            return center;

        GridNode bestNode = null;
        float bestSqrDistance = float.MaxValue;

        for (int radius = 1; radius <= maxSearchRadius; radius++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    int checkX = center.gridX + x;
                    int checkY = center.gridY + y;

                    if (!TryGetNode(checkX, checkY, out GridNode node))
                        continue;

                    if (!IsNodeTraversableForRadius(node, agentRadius))
                        continue;

                    float sqr = (node.worldPosition - worldPosition).sqrMagnitude;
                    if (sqr < bestSqrDistance)
                    {
                        bestSqrDistance = sqr;
                        bestNode = node;
                    }
                }
            }

            if (bestNode != null)
                return bestNode;
        }

        return null;
    }

    public List<GridNode> GetNeighbours(GridNode node, float agentRadius)
    {
        List<GridNode> neighbours = new List<GridNode>();

        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                if (x == 0 && y == 0)
                    continue;

                int checkX = node.gridX + x;
                int checkY = node.gridY + y;

                if (!TryGetNode(checkX, checkY, out GridNode candidate))
                    continue;

                if (!IsNodeTraversableForRadius(candidate, agentRadius))
                    continue;

                bool isDiagonal = x != 0 && y != 0;
                if (preventCornerCutting && isDiagonal)
                {
                    bool sideAOk = TryGetNode(node.gridX + x, node.gridY, out GridNode sideA) &&
                                   IsNodeTraversableForRadius(sideA, agentRadius);

                    bool sideBOk = TryGetNode(node.gridX, node.gridY + y, out GridNode sideB) &&
                                   IsNodeTraversableForRadius(sideB, agentRadius);

                    if (!sideAOk || !sideBOk)
                        continue;
                }

                neighbours.Add(candidate);
            }
        }

        return neighbours;
    }

    public List<Vector3> SimplifyPath(List<GridNode> path)
    {
        List<Vector3> waypoints = new List<Vector3>();
        if (path == null || path.Count == 0)
            return waypoints;

        waypoints.Add(path[0].worldPosition);
        Vector2 oldDirection = Vector2.zero;

        for (int i = 1; i < path.Count; i++)
        {
            Vector2 newDirection = new Vector2(
                path[i].gridX - path[i - 1].gridX,
                path[i].gridY - path[i - 1].gridY
            );

            if (newDirection != oldDirection)
                waypoints.Add(path[i].worldPosition);

            oldDirection = newDirection;
        }

        Vector3 last = path[path.Count - 1].worldPosition;
        if (waypoints.Count == 0 || Vector3.Distance(waypoints[waypoints.Count - 1], last) > 0.01f)
            waypoints.Add(last);

        return waypoints;
    }

    public void ResetNodesRuntimeData()
    {
        for (int x = 0; x < gridSizeX; x++)
        {
            for (int y = 0; y < gridSizeY; y++)
            {
                GridNode node = grid[x, y];
                node.gCost = int.MaxValue;
                node.hCost = 0;
                node.parent = null;
            }
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.white;
        Gizmos.DrawWireCube(transform.position, new Vector3(gridWorldSize.x, 1f, gridWorldSize.y));

        if (!drawGridGizmos || grid == null)
            return;

        foreach (GridNode node in grid)
        {
            if (!node.walkable)
                Gizmos.color = new Color(1f, 0f, 0f, 0.35f);
            else
            {
                float t = Mathf.InverseLerp(0f, maxClearanceRadius, node.clearanceRadius);
                Gizmos.color = Color.Lerp(new Color(1f, 1f, 0f, 0.15f), new Color(0f, 1f, 0f, 0.25f), t);
            }

            Gizmos.DrawCube(node.worldPosition, Vector3.one * (nodeDiameter - 0.05f));
        }
    }
}