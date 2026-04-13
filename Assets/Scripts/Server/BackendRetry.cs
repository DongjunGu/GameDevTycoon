using System;
using System.Collections;
using BackEnd;
using UnityEngine;

public class BackendRetry : MonoBehaviour
{
    public static BackendRetry Instance { get; private set; }

    const int MaxRetries = 3;
    const float RetryDelay = 2f;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // Backend.GameData.GetMyData 재시도 래퍼
    public void GetMyData(string tableName, Action<BackendReturnObject> onDone)
    {
        StartCoroutine(Attempt(tableName, onDone, MaxRetries));
    }

    IEnumerator Attempt(string tableName, Action<BackendReturnObject> onDone, int retriesLeft)
    {
        BackendReturnObject result = null;
        Backend.GameData.GetMyData(tableName, new Where(), bro => result = bro);
        yield return new WaitUntil(() => result != null);

        if (result.IsSuccess() || retriesLeft <= 0)
        {
            if (!result.IsSuccess())
                Debug.LogError($"[BackendRetry] {tableName} 최종 실패: {result}");
            onDone(result);
        }
        else
        {
            Debug.LogWarning($"[BackendRetry] {tableName} 실패, {RetryDelay}초 후 재시도 (남은: {retriesLeft - 1}): {result}");
            yield return new WaitForSeconds(RetryDelay);
            yield return Attempt(tableName, onDone, retriesLeft - 1);
        }
    }
}
