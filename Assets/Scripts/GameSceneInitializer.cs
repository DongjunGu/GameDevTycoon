using UnityEngine;

public class GameSceneInitializer : MonoBehaviour
{
    void Start()
    {
        ProjectSaveManager.Instance.RestoreIfNeeded();
    }
}