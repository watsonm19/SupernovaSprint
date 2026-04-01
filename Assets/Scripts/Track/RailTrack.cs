// ═════════════════════════════════════════════════════════════════════════════
//  RailTrack.cs
//  Defines a grind rail as an ordered series of waypoints.
//  The player snaps onto it when airborne or slamming and automatically
//  travels at topSpeed until they jump off or reach the end.
//
//  SETUP:
//    1. Create an empty GameObject for the rail — add this script.
//    2. Add child empty GameObjects as waypoints; assign them to the Waypoints array
//       in order from start to finish.
//    3. Add a Trigger Collider (e.g. BoxCollider, isTrigger = true) that covers
//       the rail. Make it slightly larger than the visual so it's easy to land on.
//    4. Optionally add a visual mesh as another child (the script drives no visuals).
// ═════════════════════════════════════════════════════════════════════════════

using UnityEngine;

public class RailTrack : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    #region Inspector

    [Header("── Path ─────────────────────────────────────────────────────────")]
    [Tooltip("Ordered waypoints defining the rail path. Add child empty GameObjects and assign them here.")]
    public Transform[] waypoints;

    #endregion
    // ─────────────────────────────────────────────────────────────────────────
    #region Precomputed Data

    private float[] _segmentLengths;
    private float   _totalLength;

    public float TotalLength => _totalLength;

    private void Awake()
    {
        PrecomputeSegments();
    }

    private void PrecomputeSegments()
    {
        if (waypoints == null || waypoints.Length < 2)
        {
            Debug.LogWarning($"[RailTrack] '{name}' needs at least 2 waypoints.", this);
            return;
        }

        _segmentLengths = new float[waypoints.Length - 1];
        _totalLength    = 0f;

        for (int i = 0; i < _segmentLengths.Length; i++)
        {
            _segmentLengths[i] = Vector3.Distance(waypoints[i].position, waypoints[i + 1].position);
            _totalLength      += _segmentLengths[i];
        }
    }

    #endregion
    // ─────────────────────────────────────────────────────────────────────────
    #region Path Queries

    // Returns the world-space position and forward direction at a given distance
    // along the rail (clamped to [0, TotalLength]).
    public (Vector3 position, Vector3 direction) GetPointAtDistance(float distance)
    {
        if (_segmentLengths == null || _segmentLengths.Length == 0)
            return (transform.position, transform.forward);

        distance = Mathf.Clamp(distance, 0f, _totalLength);
        float remaining = distance;

        for (int i = 0; i < _segmentLengths.Length; i++)
        {
            bool lastSegment = i == _segmentLengths.Length - 1;
            if (remaining <= _segmentLengths[i] || lastSegment)
            {
                float   t   = _segmentLengths[i] > 0f ? Mathf.Clamp01(remaining / _segmentLengths[i]) : 0f;
                Vector3 pos = Vector3.Lerp(waypoints[i].position, waypoints[i + 1].position, t);
                Vector3 dir = (waypoints[i + 1].position - waypoints[i].position).normalized;
                return (pos, dir);
            }
            remaining -= _segmentLengths[i];
        }

        // Fallback: end of rail
        int last = waypoints.Length - 1;
        return (waypoints[last].position,
                (waypoints[last].position - waypoints[last - 1].position).normalized);
    }

    // Returns the distance along the rail closest to the given world position.
    // Used to snap the player to the nearest point on entry.
    public float GetClosestDistance(Vector3 worldPos)
    {
        if (_segmentLengths == null || _segmentLengths.Length == 0) return 0f;

        float bestSqrDist  = float.MaxValue;
        float bestDistance = 0f;
        float accumulated  = 0f;

        for (int i = 0; i < _segmentLengths.Length; i++)
        {
            Vector3 a  = waypoints[i].position;
            Vector3 b  = waypoints[i + 1].position;
            Vector3 ab = b - a;

            float t       = Mathf.Clamp01(Vector3.Dot(worldPos - a, ab) / ab.sqrMagnitude);
            Vector3 point = a + ab * t;
            float sqrDist = (worldPos - point).sqrMagnitude;

            if (sqrDist < bestSqrDist)
            {
                bestSqrDist  = sqrDist;
                bestDistance = accumulated + t * _segmentLengths[i];
            }

            accumulated += _segmentLengths[i];
        }

        return bestDistance;
    }

    #endregion
    // ─────────────────────────────────────────────────────────────────────────
    #region Trigger Detection

    private void OnTriggerEnter(Collider other)
    {
        // Support controller on root or on a parent of the collider
        var controller = other.GetComponent<SupernovaSprintController>()
                      ?? other.GetComponentInParent<SupernovaSprintController>();
        if (controller == null) return;

        // Only grind when the player is in disc form (gravity slam) — the slam board
        // is what physically locks onto the rail.
        if (!controller.isSlamming) return;

        controller.StartGrinding(this);
    }

    #endregion
    // ─────────────────────────────────────────────────────────────────────────
    #region Editor Gizmos

    private void OnDrawGizmos()
    {
        if (waypoints == null || waypoints.Length < 2) return;

        Gizmos.color = Color.yellow;
        for (int i = 0; i < waypoints.Length - 1; i++)
        {
            if (waypoints[i] != null && waypoints[i + 1] != null)
                Gizmos.DrawLine(waypoints[i].position, waypoints[i + 1].position);
        }

        Gizmos.color = Color.green;
        foreach (var wp in waypoints)
        {
            if (wp != null)
                Gizmos.DrawWireSphere(wp.position, 0.25f);
        }

        // Direction arrows every 2 m
        if (Application.isPlaying && _totalLength > 0f)
        {
            Gizmos.color = new Color(1f, 1f, 0f, 0.5f);
            float step = 2f;
            for (float d = step; d < _totalLength; d += step)
            {
                var (pos, dir) = GetPointAtDistance(d);
                Gizmos.DrawRay(pos, dir * 0.6f);
            }
        }
    }

    #endregion
}
