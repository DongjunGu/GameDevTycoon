using UnityEngine;

public class DevSpeedTest : MonoBehaviour
{
    public void SetFastDev()
    {
        DevelopmentManager.Instance.developmentDuration = 10f;
        Debug.Log("[DevSpeedTest] 개발시간 10초로 설정");
    }
}
