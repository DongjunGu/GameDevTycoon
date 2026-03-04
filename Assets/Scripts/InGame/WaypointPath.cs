using UnityEngine;

public class WaypointPath : MonoBehaviour
{
    // 인스펙터에서 순서대로 드래그 연결
    public Transform[] waypoints;

    public Vector3 GetWaypoint(int index)
    {
        return waypoints[index].position;
    }

    public int Count => waypoints.Length;

    // 씬에서 경로 시각화
    void OnDrawGizmos()
    {
        if (waypoints == null || waypoints.Length < 2) return;

        Gizmos.color = Color.yellow;
        for (int i = 0; i < waypoints.Length - 1; i++)
        {
            if (waypoints[i] != null && waypoints[i + 1] != null)
                Gizmos.DrawLine(waypoints[i].position, waypoints[i + 1].position);
        }

        // 마지막 → 처음 연결 (순환 경로)
        if (waypoints[^1] != null && waypoints[0] != null)
            Gizmos.DrawLine(waypoints[^1].position, waypoints[0].position);
    }
}