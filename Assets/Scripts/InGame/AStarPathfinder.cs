using System.Collections.Generic;
using UnityEngine;

public class AStarPathfinder : MonoBehaviour
{
    public static AStarPathfinder Instance { get; private set; }

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    public List<Vector3Int> FindPath(Vector3Int start, Vector3Int goal)
    {
        var openSet = new PriorityQueue<Vector3Int>();
        var cameFrom = new Dictionary<Vector3Int, Vector3Int>();
        var gScore = new Dictionary<Vector3Int, float>();
        var fScore = new Dictionary<Vector3Int, float>();

        gScore[start] = 0;
        fScore[start] = Heuristic(start, goal);
        openSet.Enqueue(start, fScore[start]);

        while (openSet.Count > 0)
        {
            var current = openSet.Dequeue();

            if (current == goal)
                return ReconstructPath(cameFrom, current);

            foreach (var neighbor in GridManager.Instance.GetNeighbors(current))
            {
                if (!GridManager.Instance.IsWalkable(neighbor)) continue;

                // 계단(층 이동) 비용을 일반 이동보다 약간 높게 (선택적)
                float moveCost = Mathf.Abs(neighbor.z - current.z) > 0 ? 1.5f : 1f;
                float tentativeG = gScore.GetValueOrDefault(current, float.MaxValue) + moveCost;

                if (tentativeG < gScore.GetValueOrDefault(neighbor, float.MaxValue))
                {
                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentativeG;
                    fScore[neighbor] = tentativeG + Heuristic(neighbor, goal);
                    openSet.Enqueue(neighbor, fScore[neighbor]);
                }
            }
        }

        return null;
    }

    float Heuristic(Vector3Int a, Vector3Int b)
    {
        // z(층) 차이도 휴리스틱에 반영
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y) + Mathf.Abs(a.z - b.z);
    }

    List<Vector3Int> ReconstructPath(Dictionary<Vector3Int, Vector3Int> cameFrom, Vector3Int current)
    {
        var path = new List<Vector3Int> { current };
        while (cameFrom.ContainsKey(current))
        {
            current = cameFrom[current];
            path.Insert(0, current);
        }
        return path;
    }
}

// ── 우선순위 큐 ──────────────────────────────────────
public class PriorityQueue<T>
{
    private List<(T item, float priority)> _elements = new();
    public int Count => _elements.Count;

    public void Enqueue(T item, float priority)
    {
        _elements.Add((item, priority));
    }

    public T Dequeue()
    {
        int bestIndex = 0;
        for (int i = 1; i < _elements.Count; i++)
            if (_elements[i].priority < _elements[bestIndex].priority)
                bestIndex = i;

        T best = _elements[bestIndex].item;
        _elements.RemoveAt(bestIndex);
        return best;
    }
}