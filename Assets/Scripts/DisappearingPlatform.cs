using UnityEngine;
using System.Collections;

public class DisappearingPlatform : MonoBehaviour
{
    [Header("Timing")]
    [SerializeField] float warningBlinkDuration = 1.5f;
    [SerializeField] float timeSpentInvisible = 3f;
    [SerializeField] float blinkSpeedInterval = 0.1f;

    [Header("References")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Collider2D platformCollider;

    private bool isTriggered = false;
    private Color originalColor;

    private void Awake()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (platformCollider == null) platformCollider = GetComponent<Collider2D>();
        originalColor = spriteRenderer.color;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (isTriggered) return;

        // Ensure it's the player landing on TOP of the surface
        foreach (ContactPoint2D contact in collision.contacts)
        {
            if (contact.normal.y < -0.8f)
            {
                StartCoroutine(BlinkDisappearAndReturnSequence());
                break;
            }
        }
    }

    private IEnumerator BlinkDisappearAndReturnSequence()
    {
        isTriggered = true;
        float elapsed = 0f;
        bool isVisible = true;

        // 1. WARNING PHASE: Fast blinking effect
        while (elapsed < warningBlinkDuration)
        {
            isVisible = !isVisible;
            spriteRenderer.color = isVisible ? originalColor : new Color(originalColor.r, originalColor.g, originalColor.b, 0.15f);
            yield return new WaitForSeconds(blinkSpeedInterval);
            elapsed += blinkSpeedInterval;
        }

        // 2. DISAPPEAR PHASE: Hide visuals and shut off physical collisions
        spriteRenderer.color = new Color(originalColor.r, originalColor.g, originalColor.b, 0f);
        platformCollider.enabled = false;

        // Wait out the invisible cooldown window
        yield return new WaitForSeconds(timeSpentInvisible);

        // 3. RETURN PHASE: Restore everything safely
        spriteRenderer.color = originalColor;
        platformCollider.enabled = true;
        isTriggered = false;
    }
}
