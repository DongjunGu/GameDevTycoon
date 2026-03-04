using System.Collections.Generic;
using UnityEngine;

public class CharacterManager : MonoBehaviour
{
    public static CharacterManager Instance { get; private set; }

    private CharacterController _selectedCharacter;
    public CharacterController SelectedCharacter => _selectedCharacter;

    private List<CharacterController> _allCharacters = new();

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    // 씬에 배치된 캐릭터 등록 (각 CharacterController의 Start에서 호출)
    public void Register(CharacterController character)
    {
        if (!_allCharacters.Contains(character))
            _allCharacters.Add(character);
    }

    // 캐릭터 선택 (지금은 GameController에서 마우스 클릭으로 호출)
    // 나중에 UI Manager 등에서도 호출 가능
    public void SelectCharacter(CharacterController character)
    {
        _selectedCharacter = character;
        Debug.Log($"선택된 캐릭터: {character.name}");
    }

    public void ClearSelection()
    {
        _selectedCharacter = null;
    }

    // 선택된 캐릭터를 목적지로 보냄
    public void AssignDestination(Vector3Int targetCell)
    {
        if (_selectedCharacter == null)
        {
            Debug.Log("선택된 캐릭터 없음");
            return;
        }

        if (_selectedCharacter.State == CharacterState.Moving)
        {
            Debug.Log($"{_selectedCharacter.name} 이동 중 - 명령 무시");
            return;
        }

        _selectedCharacter.SetState(CharacterState.Idle);
        _selectedCharacter.MoveTo(targetCell);
    }
}