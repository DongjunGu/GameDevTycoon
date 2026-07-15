using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// 클릭한 지점의 GridManager 셀 좌표를 화면/콘솔에 출력하는 디버그 툴.
// 엘리베이터로 이을 두 셀(아래층 진입 지점 / 위층 도착 지점) 좌표를 확인할 때 사용.
public class CellCoordinateInspector : MonoBehaviour
{
    [Tooltip("최근 클릭 기록 표시 개수")]
    public int maxHistory = 8;

    private readonly List<string> _history = new();

    void Update()
    {
        var mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
        if (GridManager.Instance == null || Camera.main == null) return;

        Vector3 screenPos = mouse.position.ReadValue();
        screenPos.z = -Camera.main.transform.position.z; // 카메라 ~ z=0 평면까지 거리
        Vector3 worldPos = Camera.main.ScreenToWorldPoint(screenPos);
        worldPos.z = 0f;

        Vector3Int cell = GridManager.Instance.WorldToCell(worldPos);
        bool walkable = GridManager.Instance.IsWalkable(cell);

        string line = $"cell={cell}  world=({worldPos.x:F2},{worldPos.y:F2})  walkable={walkable}";
        Debug.Log($"[CellCoordinateInspector] {line}");

        _history.Insert(0, line);
        if (_history.Count > maxHistory) _history.RemoveAt(_history.Count - 1);
    }

    void OnGUI()
    {
        if (_history.Count == 0) return;

        const int width = 420;
        int height = 24 * _history.Count + 12;
        GUI.Box(new Rect(10, 10, width, height), "");

        var style = new GUIStyle(GUI.skin.label) { fontSize = 16 };
        for (int i = 0; i < _history.Count; i++)
        {
            GUI.Label(new Rect(20, 14 + i * 24, width - 20, 24), _history[i], style);
        }
    }
}
