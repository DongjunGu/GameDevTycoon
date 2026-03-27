using UnityEngine;

public class CharacterAnimator : MonoBehaviour
{
    [Header("Components")]
    public Animator animator;
    public SpriteRenderer spriteRenderer;

    // Animator Parameter 이름
    private static readonly int IsFront = Animator.StringToHash("isFront");
    private static readonly int IsWalking = Animator.StringToHash("isWalking");
    
    public bool GetCurrentIsFront()
    => animator.GetBool(IsFront);

    public void SetMoving(bool isMoving)
    {
        animator.speed = isMoving ? 1f : 0f;
    }


    public void UpdateDirection(Vector3 from, Vector3 to)
    {
        Vector3 delta = to - from;
        if (delta.sqrMagnitude < 0.001f) return;

        animator.speed = 1f;
        bool isFront = delta.y <= 0f;
        bool flipX = delta.x < 0f;

        animator.SetBool(IsFront, isFront);
        spriteRenderer.flipX = flipX;
    }

    public void SetIdle(bool isFront)
    {
        animator.speed = 0f;
        animator.SetBool(IsFront, isFront);
    }
}