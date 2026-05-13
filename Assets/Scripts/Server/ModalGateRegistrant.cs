using UnityEngine;

// 자기 GameObject 의 활성/비활성에 맞춰 ModalGate 에 자동 등록/해제.
// 차단해야 할 UI(RandomEventUI, LeaderSelectUI.leaderPanel 등)에 이 컴포넌트만 부착하면 됨.
public class ModalGateRegistrant : MonoBehaviour
{
    void OnEnable()  => ModalGate.I.Register(this);
    void OnDisable() => ModalGate.I.Unregister(this);
}
