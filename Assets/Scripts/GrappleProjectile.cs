using UnityEngine;

public class GrappleProjectile : MonoBehaviour
{
    private enum HookState { FlyingOut, Anchored, Retracting }
    private HookState currentState = HookState.FlyingOut;

    private Vector2 direction;
    private float speed;
    private LayerMask groundLayer;
    private PlayerController player;
    private float lifetime;

    public void Initialize(PlayerController playerRef, Vector2 dir, float launchSpeed, float hookLifetime, LayerMask layer)
    {
        player = playerRef;
        direction = dir;
        speed = launchSpeed;
        lifetime = hookLifetime;
        groundLayer = layer;

        currentState = HookState.FlyingOut;

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
    }

    void Update()
    {
        if (currentState == HookState.Anchored) return;

        if (currentState == HookState.FlyingOut)
        {
            transform.Translate(Vector2.right * speed * Time.deltaTime);

            lifetime -= Time.deltaTime;
            if (lifetime <= 0f)
            {
                StartRetracting();
            }
        }
        else if (currentState == HookState.Retracting)
        {
            Vector2 playerPos = player.GetShotSpawnPosition();

            transform.position = Vector2.MoveTowards(transform.position, playerPos, speed * 1.5f * Time.deltaTime);

            Vector2 returnDir = (playerPos - (Vector2)transform.position).normalized;
            float angle = Mathf.Atan2(returnDir.y, returnDir.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);

            if (Vector2.Distance(transform.position, playerPos) < 0.5f)
            {
                player.ResetGrappleState();
            }
        }
    }

    private void StartRetracting()
    {
        currentState = HookState.Retracting;
        transform.SetParent(null);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (currentState != HookState.FlyingOut) return;

        if (((1 << collision.gameObject.layer) & groundLayer) != 0)
        {
            currentState = HookState.Anchored;
            speed = 0;
            transform.SetParent(collision.transform);
            player.NotifyHookLanded(transform.position, gameObject);
        }
    }
}
