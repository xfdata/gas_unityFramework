using System;
using System.Collections.Generic;
using Framework;
using UnityEngine;
using BattleFoundation;
using UnityEngine.AI;

namespace BattleCommon
{

public class CombatPathfindingSystem : Disposable, IBattleSystem
{
    private IBattleContext _context;
    private NavMeshPath _cachedPath = new NavMeshPath();
    private int _walkableAreaMask;

    private Queue<PathRequest> _requestQueue = new Queue<PathRequest>(32);
    private Framework.ObjectPool<PathRequest> _requestPool = new Framework.ObjectPool<PathRequest>();
    private int _processPerFrame = 2;
    private float _updateInterval = 1f / 20f;
    private float _updateTimer;

    public CombatPathfindingSystem()
    {
        _walkableAreaMask = 1 << NavMesh.GetAreaFromName("Walkable");
    }

    public void Initialize(IBattleContext context)
    {
        _context = context;
    }

    public void Start()
    {
    }

    public void Update(float deltaTime)
    {
        using (new AutoProfiler("BattleCommon.CombatPathfinding.Update"))
        {
            _updateTimer += deltaTime;
            while (_updateTimer >= _updateInterval)
            {
                _updateTimer -= _updateInterval;
                ProcessRequests();
            }
        }
    }

    private void ProcessRequests()
    {
        int processed = 0;
        while (processed < _processPerFrame && _requestQueue.Count > 0)
        {
            var request = _requestQueue.Dequeue();
            if (request.IsCancelled)
            {
                ReturnRequest(request, true);
                continue;
            }

            bool success = CalculatePath(request.Start, request.End, request.ResultPath);
            request.Callback?.Invoke(success, request.ResultPath);
            ReturnRequest(request, false);
            processed++;
        }
    }

    public void LateUpdate(float deltaTime)
    {
    }

    public bool CalculatePath(Vector3 start, Vector3 end, List<Vector3> resultPath)
    {
        resultPath.Clear();

        if (!IsPositionOnNavMesh(start))
        {
            if (!TrySamplePosition(start, out start))
            {
                return false;
            }
        }

        if (!IsPositionOnNavMesh(end))
        {
            if (!TrySamplePosition(end, out end))
            {
                return false;
            }
        }

        _cachedPath.ClearCorners();
        if (NavMesh.CalculatePath(start, end, _walkableAreaMask, _cachedPath))
        {
            if (_cachedPath.status == NavMeshPathStatus.PathComplete ||
                _cachedPath.status == NavMeshPathStatus.PathPartial)
            {
                for (int i = 0; i < _cachedPath.corners.Length; i++)
                {
                    resultPath.Add(_cachedPath.corners[i]);
                }
                return true;
            }
        }

        return false;
    }

    public void RequestPathAsync(Vector3 start, Vector3 end, Action<bool, List<Vector3>> callback)
    {
        var request = _requestPool.Get();
        request.Start = start;
        request.End = end;
        if (request.ResultPath == null)
            request.ResultPath = new List<Vector3>();
        else
            request.ResultPath.Clear();
        request.Callback = callback;
        request.IsCancelled = false;
        _requestQueue.Enqueue(request);
    }

    public static bool IsPositionOnNavMesh(Vector3 position)
    {
        return NavMesh.SamplePosition(position, out _, 0.1f, NavMesh.AllAreas);
    }

    public static bool TrySamplePosition(Vector3 position, out Vector3 result, float maxDistance = 10f)
    {
        if (NavMesh.SamplePosition(position, out var hit, maxDistance, NavMesh.AllAreas))
        {
            result = hit.position;
            return true;
        }
        result = position;
        return false;
    }

    public Vector3 GetRandomPointOnNavMesh(Vector3 center, float radius)
    {
        if (_context?.Random == null)
            return center;

        for (int i = 0; i < 30; i++)
        {
            Vector3 randomDirection = _context.Random.InsideUnitSphere() * radius;
            randomDirection += center;
            if (NavMesh.SamplePosition(randomDirection, out var hit, radius, NavMesh.AllAreas))
            {
                return hit.position;
            }
        }
        return center;
    }

    protected override void OnDispose()
    {
        _context = null;
        base.OnDispose();
        while (_requestQueue.Count > 0)
        {
            var req = _requestQueue.Dequeue();
            ReturnRequest(req, true);
        }
    }

    private void ReturnRequest(PathRequest request, bool clearResultPath)
    {
        if (request == null)
            return;

        request.Reset(clearResultPath);
        _requestPool.Return(request);
    }

    private class PathRequest
    {
        public Vector3 Start;
        public Vector3 End;
        public List<Vector3> ResultPath;
        public Action<bool, List<Vector3>> Callback;
        public bool IsCancelled;

        public void Reset(bool clearResultPath)
        {
            Start = Vector3.zero;
            End = Vector3.zero;
            Callback = null;
            IsCancelled = false;
            if (clearResultPath)
                ResultPath?.Clear();
        }
    }
}

public static class CombatNavMeshUtility
{
    public static bool FindClosestReachablePoint(Vector3 position, out Vector3 result, float maxDistance = 10f)
    {
        if (NavMesh.SamplePosition(position, out var hit, maxDistance, NavMesh.AllAreas))
        {
            result = hit.position;
            return true;
        }
        result = Vector3.zero;
        return false;
    }

    public static float CalculatePathLength(List<Vector3> path)
    {
        if (path == null || path.Count < 2) return 0f;

        float length = 0f;
        for (int i = 1; i < path.Count; i++)
        {
            length += Vector3.Distance(path[i - 1], path[i]);
        }
        return length;
    }

    public static Vector3 GetDirectionAlongPath(List<Vector3> path, int currentIndex)
    {
        if (path == null || path.Count < 2) return Vector3.forward;
        if (currentIndex >= path.Count - 1) return (path[path.Count - 1] - path[path.Count - 2]).normalized;
        return (path[currentIndex + 1] - path[currentIndex]).normalized;
    }
}

}
