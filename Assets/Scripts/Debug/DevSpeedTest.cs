using UnityEngine;

public class DevSpeedTest : MonoBehaviour
{
    public void SetFastDev()
    {
        DevelopmentManager.Instance.developmentDuration = 10f;
        Debug.Log("[DevSpeedTest] 개발시간 10초로 설정");
    }

    public void SetZeroBug()
    {
        DevelopmentPanelUI.Instance.SetBug(0f);
        Debug.Log("[DevSpeedTest] 버그 0으로 설정");
    }
}
