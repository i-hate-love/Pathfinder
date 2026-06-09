using System.Collections.Generic;
using UnityEngine;

public class AStarPathfinder : MonoBehaviour
{
    public static AStarPathfinder Instance;

    private GridManager grid;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        grid = GridManager.Instance;
    }

    public List<Vector3> FindPath(Vector3 startPos, Vector3 targetPos, float agentRadius)
    {
        if (grid == null)
            grid = GridManager.Instance;

        if (grid == null)
            return null;

        GridNode startNode = grid.GetClosestTraversableNode(startPos, agentRadius);
        GridNode targetNode = grid.GetClosestTraversableNode(targetPos, agentRadius);

        if (startNode == null || targetNode == null)
            return null;

        grid.ResetNodesRuntimeData();

        List<GridNode> openSet = new List<GridNode>();
        HashSet<GridNode> closedSet = new HashSet<GridNode>();

        startNode.gCost = 0;
        startNode.hCost = GetDistance(startNode, targetNode);
        openSet.Add(startNode);

        while (openSet.Count > 0)
        {
            GridNode currentNode = openSet[0];

            for (int i = 1; i < openSet.Count; i++)
            {
                if (openSet[i].fCost < currentNode.fCost ||
                    (openSet[i].fCost == currentNode.fCost && openSet[i].hCost < currentNode.hCost))
                {
                    currentNode = openSet[i];
                }
            }

            openSet.Remove(currentNode);
            closedSet.Add(currentNode);

            if (currentNode == targetNode)
                return RetracePath(startNode, targetNode);

            foreach (GridNode neighbour in grid.GetNeighbours(currentNode, agentRadius))
            {
                if (closedSet.Contains(neighbour))
                    continue;

                int newCost = currentNode.gCost + GetDistance(currentNode, neighbour) + neighbour.movementPenalty;

                if (newCost < neighbour.gCost || !openSet.Contains(neighbour))
                {
                    neighbour.gCost = newCost;
                    neighbour.hCost = GetDistance(neighbour, targetNode);
                    neighbour.parent = currentNode;

                    if (!openSet.Contains(neighbour))
                        openSet.Add(neighbour);
                }
            }
        }

        return null;
    }

    List<Vector3> RetracePath(GridNode startNode, GridNode endNode)
    {
        List<GridNode> path = new List<GridNode>();
        GridNode currentNode = endNode;

        while (currentNode != startNode)
        {
            path.Add(currentNode);
            currentNode = currentNode.parent;

            if (currentNode == null)
                return null;
        }

        path.Add(startNode);
        path.Reverse();
        return grid.SimplifyPath(path);
    }

    int GetDistance(GridNode a, GridNode b)
    {
        int dstX = Mathf.Abs(a.gridX - b.gridX);
        int dstY = Mathf.Abs(a.gridY - b.gridY);

        if (dstX > dstY)
            return 14 * dstY + 10 * (dstX - dstY);

        return 14 * dstX + 10 * (dstY - dstX);
    }
}