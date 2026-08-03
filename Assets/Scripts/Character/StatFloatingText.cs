using System.Collections;
using TMPro;
using UnityEngine;

public class StatFloatingText : MonoBehaviour
{
    public TextMeshPro label;
    public float floatSpeed = 1.0f;
    public float duration = 1.2f;

    public void Show(string text, Color color)
    {
        label.text = text;
        label.color = color;
        StartCoroutine(Animate());
    }

    IEnumerator Animate()
    {
        float elapsed = 0f;
        Vector3 startPos = transform.position;
        Color startColor = label.color;

        while (elapsed < duration)
        {
            if (GameTimeManager.Instance == null || GameTimeManager.Instance.IsRunningForMovement)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                transform.position = startPos + Vector3.up * floatSpeed * t;
                label.color = new Color(startColor.r, startColor.g, startColor.b, 1f - t);
            }
            yield return null;
        }

        StatFloatingTextPool.Instance?.Return(this);
    }
}
