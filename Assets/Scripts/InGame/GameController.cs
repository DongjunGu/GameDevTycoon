using UnityEngine;
using UnityEngine.InputSystem;

public class GameController : MonoBehaviour
{
    [Header("Layers")]
    public LayerMask characterLayer;
    public LayerMask workStationLayer;

    void Update()
    {
        if (Mouse.current.leftButton.wasPressedThisFrame)
            HandleClick();
    }

    void HandleClick()
    {
        Vector3 worldPos = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        worldPos.z = 0;

        // 1. 캐릭터 클릭 → 선택
        var characterHit = Physics2D.OverlapPoint(worldPos, characterLayer);
        if (characterHit != null)
        {
            var character = characterHit.GetComponent<CharacterController>();
            if (character != null)
            {
                CharacterManager.Instance.SelectCharacter(character);
                return;
            }
        }

        // 2. WorkStation 클릭 → 선택된 캐릭터를 WorkPoint로 이동
        var stationHit = Physics2D.OverlapPoint(worldPos, workStationLayer);
        if (stationHit != null)
        {
            var station = stationHit.GetComponent<WorkStation>();
            if (station != null)
            {
                CharacterManager.Instance.AssignDestination(station.GetWorkCell());
            }
        }
    }
}