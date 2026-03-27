using UnityEngine;

public class OfficeCharacter : MonoBehaviour
{
    public string employeeId;
    public WorkStation assignedDesk;

    private CharacterController _controller;

    void Awake()
    {
        _controller = GetComponent<CharacterController>();
    }

    // 채용 시 초기화
    public void Init(string empId, WorkStation desk)
    {
        employeeId   = empId;
        assignedDesk = desk;
    }

    // 지정된 Desk로 이동
    public void GoToDesk()
    {
        if (assignedDesk == null) return;
        _controller.MoveTo(
            assignedDesk.GetWorkCell(),
            assignedDesk.GetWorkWorldPos()
        );
    }
}