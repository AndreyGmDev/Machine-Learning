using System;
using System.Collections.Generic;
using UnityEngine;

// Rota de referencia: grid navegavel no plano XZ + A*. Serve apenas de comparacao;
// ela nunca controla a nave evoluida.
public sealed class ReferenceRoute
{
    private const int MaxCells = 90000;
    private const int MaxSearchRadius = 8;
    private static readonly float Diagonal = Mathf.Sqrt(2f);

    private readonly List<Vector3> corners;

    public IReadOnlyList<Vector3> Corners => corners;
    public float Length { get; }

    private ReferenceRoute(List<Vector3> corners)
    {
        this.corners = corners;
        float total = 0f;
        for (int i = 1; i < corners.Count; i++)
            total += Vector3.Distance(corners[i - 1], corners[i]);
        Length = total;
    }

    // Retorna null (com aviso no Console) se nao houver caminho entre start e goal.
    public static ReferenceRoute Find(Vector3 start, Vector3 goal, LayerMask obstacleMask, float cellSize, float agentRadius, bool smooth)
    {
        NavGrid grid = BuildGrid(start, goal, obstacleMask, cellSize, agentRadius);

        if (!grid.TryGetWalkableCell(start, out int startCell) || !grid.TryGetWalkableCell(goal, out int goalCell))
        {
            Debug.LogWarning("ReferenceRoute: Spawn ou Goal esta bloqueado/fora do grid (sem celula navegavel por perto).");
            return null;
        }

        List<int> cells = AStar(grid, startCell, goalCell);
        if (cells == null)
        {
            Debug.LogWarning("ReferenceRoute: A* nao encontrou caminho entre o SpawnPoint e o Goal.");
            return null;
        }

        var points = new List<Vector3>(cells.Count + 2) { Flatten(start, start.y) };
        foreach (int cell in cells)
            points.Add(grid.CellCenter(cell));
        points.Add(Flatten(goal, start.y));

        return new ReferenceRoute(smooth ? Smooth(grid, points) : points);
    }

    private static Vector3 Flatten(Vector3 point, float y)
    {
        return new Vector3(point.x, y, point.z);
    }

    private static NavGrid BuildGrid(Vector3 start, Vector3 goal, LayerMask obstacleMask, float cellSize, float agentRadius)
    {
        // Limites: tudo que esta na layer de obstaculos + spawn + goal.
        var bounds = new Bounds(start, Vector3.zero);
        bounds.Encapsulate(goal);
        foreach (Collider collider in UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
        {
            bool isObstacle = ((1 << collider.gameObject.layer) & obstacleMask.value) != 0;
            if (isObstacle && collider.enabled && !collider.isTrigger && collider.gameObject.activeInHierarchy)
                bounds.Encapsulate(collider.bounds);
        }
        bounds.Expand(cellSize * 4f);

        // Se o grid ficar grande demais, aumenta a celula.
        float size = cellSize;
        int width = Mathf.CeilToInt(bounds.size.x / size);
        int depth = Mathf.CeilToInt(bounds.size.z / size);
        while (width * depth > MaxCells)
        {
            size *= 1.25f;
            width = Mathf.CeilToInt(bounds.size.x / size);
            depth = Mathf.CeilToInt(bounds.size.z / size);
        }

        var grid = new NavGrid(bounds.min.x, bounds.min.z, start.y, size, width, depth);
        Vector3 halfExtents = new Vector3(agentRadius, 0.2f, agentRadius);
        for (int z = 0; z < depth; z++)
        {
            for (int x = 0; x < width; x++)
            {
                Vector3 center = grid.CellCenter(x, z);
                grid.Walkable[z * width + x] = !Physics.CheckBox(center, halfExtents, Quaternion.identity, obstacleMask, QueryTriggerInteraction.Ignore);
            }
        }
        return grid;
    }

    private static List<int> AStar(NavGrid grid, int startCell, int goalCell)
    {
        int count = grid.Width * grid.Depth;
        float[] cost = new float[count];
        int[] parent = new int[count];
        bool[] closed = new bool[count];
        for (int i = 0; i < count; i++)
        {
            cost[i] = float.PositiveInfinity;
            parent[i] = -1;
        }

        var open = new MinHeap();
        cost[startCell] = 0f;
        open.Push(startCell, Heuristic(grid, startCell, goalCell));

        while (open.Count > 0)
        {
            int current = open.Pop();
            if (closed[current])
                continue;
            closed[current] = true;

            if (current == goalCell)
                return Rebuild(parent, current);

            int cx = current % grid.Width;
            int cz = current / grid.Width;
            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dz == 0)
                        continue;

                    int nx = cx + dx;
                    int nz = cz + dz;
                    if (!grid.IsWalkable(nx, nz))
                        continue;

                    bool diagonal = dx != 0 && dz != 0;
                    // Sem cortar quina: na diagonal, os dois vizinhos ortogonais precisam estar livres.
                    if (diagonal && (!grid.IsWalkable(cx + dx, cz) || !grid.IsWalkable(cx, cz + dz)))
                        continue;

                    int next = nz * grid.Width + nx;
                    if (closed[next])
                        continue;

                    float newCost = cost[current] + (diagonal ? Diagonal : 1f) * grid.CellSize;
                    if (newCost < cost[next])
                    {
                        cost[next] = newCost;
                        parent[next] = current;
                        open.Push(next, newCost + Heuristic(grid, next, goalCell));
                    }
                }
            }
        }

        return null;
    }

    // Distancia octil: consistente para movimento em 8 direcoes.
    private static float Heuristic(NavGrid grid, int from, int to)
    {
        int dx = Mathf.Abs(from % grid.Width - to % grid.Width);
        int dz = Mathf.Abs(from / grid.Width - to / grid.Width);
        int straight = Mathf.Abs(dx - dz);
        return (straight + Mathf.Min(dx, dz) * Diagonal) * grid.CellSize;
    }

    private static List<int> Rebuild(int[] parent, int last)
    {
        var path = new List<int>();
        for (int cell = last; cell != -1; cell = parent[cell])
            path.Add(cell);
        path.Reverse();
        return path;
    }

    // Remove pontos intermediarios com linha de visada livre; sobram so os "cantos" do caminho.
    private static List<Vector3> Smooth(NavGrid grid, List<Vector3> points)
    {
        var result = new List<Vector3> { points[0] };
        int index = 0;
        int last = points.Count - 1;
        while (index < last)
        {
            int next = last;
            while (next > index + 1 && !grid.HasLineOfSight(points[index], points[next]))
                next--;
            result.Add(points[next]);
            index = next;
        }
        return result;
    }

    private sealed class NavGrid
    {
        public readonly bool[] Walkable;
        public readonly int Width;
        public readonly int Depth;
        public readonly float CellSize;
        private readonly float minX;
        private readonly float minZ;
        private readonly float y;

        public NavGrid(float minX, float minZ, float y, float cellSize, int width, int depth)
        {
            this.minX = minX;
            this.minZ = minZ;
            this.y = y;
            CellSize = cellSize;
            Width = width;
            Depth = depth;
            Walkable = new bool[width * depth];
        }

        public Vector3 CellCenter(int x, int z)
        {
            return new Vector3(minX + (x + 0.5f) * CellSize, y, minZ + (z + 0.5f) * CellSize);
        }

        public Vector3 CellCenter(int cell)
        {
            return CellCenter(cell % Width, cell / Width);
        }

        public bool IsWalkable(int x, int z)
        {
            return x >= 0 && z >= 0 && x < Width && z < Depth && Walkable[z * Width + x];
        }

        public bool IsWalkable(Vector3 point)
        {
            int x = Mathf.FloorToInt((point.x - minX) / CellSize);
            int z = Mathf.FloorToInt((point.z - minZ) / CellSize);
            return IsWalkable(x, z);
        }

        // Celula do ponto; se estiver bloqueada, usa a celula livre mais proxima.
        public bool TryGetWalkableCell(Vector3 point, out int cell)
        {
            int px = Mathf.Clamp(Mathf.FloorToInt((point.x - minX) / CellSize), 0, Width - 1);
            int pz = Mathf.Clamp(Mathf.FloorToInt((point.z - minZ) / CellSize), 0, Depth - 1);

            cell = -1;
            float bestDistance = float.PositiveInfinity;
            for (int radius = 0; radius <= MaxSearchRadius && cell < 0; radius++)
            {
                for (int dz = -radius; dz <= radius; dz++)
                {
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) != radius || !IsWalkable(px + dx, pz + dz))
                            continue;

                        float distance = dx * dx + dz * dz;
                        if (distance < bestDistance)
                        {
                            bestDistance = distance;
                            cell = (pz + dz) * Width + px + dx;
                        }
                    }
                }
            }
            return cell >= 0;
        }

        public bool HasLineOfSight(Vector3 from, Vector3 to)
        {
            float distance = Vector3.Distance(new Vector3(from.x, 0f, from.z), new Vector3(to.x, 0f, to.z));
            int steps = Mathf.CeilToInt(distance / (CellSize * 0.5f));
            for (int i = 1; i < steps; i++)
            {
                if (!IsWalkable(Vector3.Lerp(from, to, i / (float)steps)))
                    return false;
            }
            return true;
        }
    }

    private sealed class MinHeap
    {
        private int[] items = new int[256];
        private float[] keys = new float[256];

        public int Count { get; private set; }

        public void Push(int item, float key)
        {
            if (Count == items.Length)
            {
                Array.Resize(ref items, Count * 2);
                Array.Resize(ref keys, Count * 2);
            }

            int i = Count++;
            while (i > 0)
            {
                int parent = (i - 1) / 2;
                if (keys[parent] <= key)
                    break;
                items[i] = items[parent];
                keys[i] = keys[parent];
                i = parent;
            }
            items[i] = item;
            keys[i] = key;
        }

        public int Pop()
        {
            int top = items[0];
            Count--;
            if (Count > 0)
            {
                int item = items[Count];
                float key = keys[Count];
                int i = 0;
                while (true)
                {
                    int child = i * 2 + 1;
                    if (child >= Count)
                        break;
                    if (child + 1 < Count && keys[child + 1] < keys[child])
                        child++;
                    if (keys[child] >= key)
                        break;
                    items[i] = items[child];
                    keys[i] = keys[child];
                    i = child;
                }
                items[i] = item;
                keys[i] = key;
            }
            return top;
        }
    }
}
