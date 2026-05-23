using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

//Dont forget to add head bumper space thingy

public class PlayerController : MonoBehaviour
{
    #region Serialized Fields & Inspector Variables

    [Header("Player Stats")]
    [SerializeField] float moveSpeed;
    [SerializeField] float speedClamp;
    [SerializeField] float decceleration;
    [SerializeField] float fallSpeedClamp;

    [Header("Jumping")]
    [SerializeField] float jumpForce;
    [SerializeField] float airJumpForce;
    [SerializeField] int jumpAmount;
    [SerializeField] float peakOfJumpGravityMultiplier;
    [SerializeField] float fastFallMultiplier;
    [SerializeField] float jumpBufferForce;
    [SerializeField][Range(0f, 1f)] float airControlMultiplier = 0.4f;
    [SerializeField] float upwardGravityMultiplier = 5f;
    [SerializeField] float coyoteTimeDuration = 0.15f;

    [Header("Wall Jumping")]
    [SerializeField] Transform wallCheck;
    [SerializeField] float wallCheckDistance = 0.4f;
    [SerializeField] float wallSlideSpeed = 2f;
    [SerializeField] Vector2 wallJumpForce = new Vector2(10f, 15f);
    [SerializeField] float wallJumpLockoutDuration = 0.15f;
    private float wallJumpLockoutTimer;

    [Header("Dash")]
    [SerializeField] int airDashAmount;
    [SerializeField] float airDashForce;
    [SerializeField] float dashCoolDown;
    [SerializeField] int currentAirDashAmount;
    [SerializeField] float dashDuration;
    [SerializeField] float dashEndVelocityPreservation = 0.5f;

    [Header("Grappling Hook")]
    [SerializeField] GameObject hookPrefab;
    [SerializeField] Transform shotSpawnPoint;
    [SerializeField] float grappleLength;
    [SerializeField] float grappleLifetime;
    [SerializeField] float hookSpeed;
    [SerializeField] float hookProjectileSpeed;
    [SerializeField] LineRenderer lineRenderer;

    [Header("Misc")]
    [SerializeField] private int staticGravity = 3;
    [SerializeField] int playerLayer;
    [SerializeField] Transform groundCheck;
    [SerializeField] LayerMask groundLayer;
    [SerializeField] float groundCheckDistance;
    [SerializeField] float jumpBufferDistance;
    [SerializeField] LayerMask platformLayer;
    [SerializeField] int platformLayerInt;

    #endregion

    #region Private Fields & State Tracking

    Rigidbody2D rb;
    private Collider2D playerCollider;
    Vector2 playerMovement;
    private Vector2 mousePos;

    int currentJumpAmount;
    float currentHorizontalSpeed;
    private float coyoteTimeCounter;

    bool isDashing = false;
    private GameObject activeHookInstance;
    private Vector2 grapplePoint = Vector2.zero;
    private bool isHookFlying;
    bool isGrappling;
    bool hitIsPlatform;

    bool isWallSliding;
    bool isTouchingWall;
    int wallDirection;

    #endregion

    #region Unity Lifecycle Methods

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerCollider = GetComponent<Collider2D>();
        currentJumpAmount = jumpAmount;
        currentAirDashAmount = airDashAmount;
    }

    private void FixedUpdate()
    {
        if (isDashing) return;

        if (wallJumpLockoutTimer > 0)
        {
            wallJumpLockoutTimer -= Time.fixedDeltaTime;
        }

        Platform();
        PlatformPhasingHandler();

        WallCheck();
        HandleWallSliding();

        MovePlayer();
        HandleGravity();
        GroundChecker();
        PlayerFastFall();
        FallSpeedClamper();

        if (isGrappling)
        {
            MovePlayerToHook();
        }
    }

    private void LateUpdate()
    {
        if ((isGrappling || isHookFlying) && activeHookInstance != null)
        {
            lineRenderer.enabled = true;
            lineRenderer.SetPosition(0, shotSpawnPoint.position);
            lineRenderer.SetPosition(1, activeHookInstance.transform.position);
        }
        else
        {
            lineRenderer.enabled = false;
        }
    }

    #endregion

    #region Input System Callbacks (OnMove, OnJump, OnDash, etc.)

    void OnMove(InputValue value)
    {
        playerMovement = value.Get<Vector2>();
    }

    void OnJump(InputValue value)
    {
        if (!value.isPressed) return;

        if (isGrappling)
        {
            MaintainGrappleMomentum();
            rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
            ResetGrappleState();
            return;
        }

        // 1. WALL JUMP EXECUTION (Completely Free Action)
        if (wallJumpLockoutTimer <= 0 && (isWallSliding || (isTouchingWall && !IsGrounded())))
        {
            rb.linearVelocity = Vector2.zero;
            currentHorizontalSpeed = 0f;

            Vector2 jumpDir = new Vector2(-wallDirection * wallJumpForce.x, wallJumpForce.y);
            rb.AddForce(jumpDir, ForceMode2D.Impulse);

            currentHorizontalSpeed = -wallDirection * wallJumpForce.x;
            wallJumpLockoutTimer = wallJumpLockoutDuration;
            currentAirDashAmount = airDashAmount;

            // Ensure the player has at least 1 jump left for a mid-air double jump
            currentJumpAmount = Mathf.Max(currentJumpAmount, jumpAmount - 1);

            return; 
        }

        // 3. STANDARD JUMP POOL SYSTEM (Fallback for normal ground/double jumps)
        // Normal Grounded Jump
        if (IsGrounded())
        {
            currentJumpAmount--;
            rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
        }
        // Standard Air Jump / Double Jump
        else if (currentJumpAmount > 0 && JumpBufferChecker() == false)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f); 
            rb.AddForce(Vector2.up * airJumpForce, ForceMode2D.Impulse);
            currentJumpAmount--;
        }
        // Jump Buffer
        else if (IsFalling() && JumpBufferChecker())
        {
            StartCoroutine(JumpWhenGrounded());
        }
    }

    void OnDash(InputValue value)
    {
        if (isDashing == false && IsGrounded() == false && currentAirDashAmount > 0)
        {
            Vector2 dashDirection = new Vector2(playerMovement.x, playerMovement.y);
            StartCoroutine(Dash(dashDirection.normalized));
        }
    }

    void OnFire(InputValue value)
    {
        if (value.isPressed)
        {
            ShootGrapplingHook();
        }
    }

    void OnCancel(InputValue value)
    {
        if (value.isPressed)
        {
            if (isGrappling)
            {
                MaintainGrappleMomentum();
                ResetGrappleState();
            }
        }
    }

    void OnRestart(InputValue value)
    {
        if (value.isPressed)
        {
            Scene currentScene = SceneManager.GetActiveScene();

            if (currentScene.name == "MainMenu" || currentScene.name == "StartScreen")
            {
                return;
            }
            
            SceneManager.LoadScene(currentScene.buildIndex);
        }
    }

    #endregion

    #region Core Movement

    void MovePlayer()
    {
        if (isDashing || isWallSliding) { return; }
        if (wallJumpLockoutTimer > 0) { return; }

        float currentModifier = IsGrounded() || PlatformChecker() ? 1f : airControlMultiplier;

        if (playerMovement.x != 0)
        {
            currentHorizontalSpeed += playerMovement.x * moveSpeed * currentModifier * Time.deltaTime;
            currentHorizontalSpeed = Mathf.Clamp(currentHorizontalSpeed, -speedClamp, speedClamp);
        }
        else
        {
            float currentDecel = IsGrounded() || PlatformChecker() ? decceleration : decceleration * 1.5f;
            currentHorizontalSpeed = Mathf.MoveTowards(currentHorizontalSpeed, 0, currentDecel * Time.deltaTime);
        }

        rb.linearVelocity = new Vector2(currentHorizontalSpeed, rb.linearVelocity.y);
    }

    IEnumerator Dash(Vector2 direction)
    {
        currentAirDashAmount--;
        isDashing = true;

        if (direction == Vector2.zero)
        {
            direction = new Vector2(currentHorizontalSpeed >= 0 ? 1f : -1f, 0f).normalized;
        }

        float startTime = Time.time;
        rb.gravityScale = 0;

        while (Time.time < startTime + dashDuration)
        {
            rb.linearVelocity = direction * airDashForce;
            yield return null;
        }

        float exitSpeed = airDashForce * dashEndVelocityPreservation;

        if (playerMovement.x != 0)
        {
            currentHorizontalSpeed = playerMovement.x * exitSpeed;
        }
        else
        {
            currentHorizontalSpeed = direction.x * exitSpeed;
        }

        currentHorizontalSpeed = Mathf.Clamp(currentHorizontalSpeed, -speedClamp, speedClamp);

        rb.linearVelocity = new Vector2(currentHorizontalSpeed, direction.y * exitSpeed);

        rb.gravityScale = staticGravity;
        isDashing = false;
    }

    #endregion

    #region Gravity & Physics Modifiers

    void HandleGravity()
    {
        if (isDashing || isGrappling || isWallSliding) return;

        if (playerMovement.y < 0 && !IsGrounded())
        {
            if (rb.linearVelocity.y > 0)
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, -0.5f);
            }
            rb.gravityScale = fastFallMultiplier;
        }
        else if (rb.linearVelocity.y < 0)
        {
            rb.gravityScale = peakOfJumpGravityMultiplier;
        }
        else if (rb.linearVelocity.y > 0)
        {
            rb.gravityScale = upwardGravityMultiplier;
        }
        else if (IsGrounded() || PlatformChecker())
        {
            rb.gravityScale = staticGravity;
        }
    }

    void PlayerFastFall()
    {
        if (isWallSliding) return;

        if (playerMovement.y < 0 && IsGrounded() == false)
        {
            if (rb.linearVelocity.y > 0)
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, -0.5f);
            }
            rb.gravityScale = fastFallMultiplier;
        }
    }

    void FallSpeedClamper()
    {
        if (isWallSliding) return;

        if (rb.linearVelocity.y < fallSpeedClamp)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, fallSpeedClamp);
        }
    }

    IEnumerator JumpWhenGrounded()
    {
        while (IsGrounded() == false)
        {
            yield return null;
        }
        currentJumpAmount--;
        rb.gravityScale = staticGravity;
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
        rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
    }

    #endregion

    #region Wall Interaction Logic

    void WallCheck()
    {
        RaycastHit2D rightWallHit = Physics2D.Raycast(wallCheck.position, Vector2.right, wallCheckDistance, groundLayer);
        RaycastHit2D leftWallHit = Physics2D.Raycast(wallCheck.position, Vector2.left, wallCheckDistance, groundLayer);

        Debug.DrawRay(wallCheck.position, Vector2.right * wallCheckDistance, Color.blue);
        Debug.DrawRay(wallCheck.position, Vector2.left * wallCheckDistance, Color.blue);

        if (rightWallHit)
        {
            isTouchingWall = true;
            wallDirection = 1;
        }
        else if (leftWallHit)
        {
            isTouchingWall = true;
            wallDirection = -1;
        }
        else
        {
            isTouchingWall = false;
        }

        if (isTouchingWall && !IsGrounded())
        {
            currentJumpAmount = Mathf.Max(currentJumpAmount, jumpAmount - 1);
        }
    }

    void HandleWallSliding()
    {
        if (wallJumpLockoutTimer > 0)
        {
            isWallSliding = false;
            return;
        }

        if (isTouchingWall && !IsGrounded() && IsFalling())
        {
            if ((wallDirection == 1 && playerMovement.x > 0.01f) || (wallDirection == -1 && playerMovement.x < -0.01f))
            {
                isWallSliding = true;
            }
            else
            {
                isWallSliding = false;
            }
        }
        else
        {
            isWallSliding = false;
        }

        if (isWallSliding)
        {
            rb.gravityScale = 0;
            rb.linearVelocity = new Vector2(0f, Mathf.Clamp(rb.linearVelocity.y, -wallSlideSpeed, float.MaxValue));
        }
    }

    #endregion

    #region Platform & Phasing Logic

    void Platform()
    {
        if (playerMovement.y < 0 && PlatformChecker())
        {
            StartCoroutine(FallThroughPlatform());
        }
    }

    void PlatformPhasingHandler()
    {
        if (playerMovement.y < 0 && (IsFalling() || PlatformChecker()))
        {
            Physics2D.IgnoreLayerCollision(playerLayer, platformLayerInt, true);
        }
        else if (!isGrappling)
        {
            Physics2D.IgnoreLayerCollision(playerLayer, platformLayerInt, false);
        }
    }

    IEnumerator FallThroughPlatform()
    {
        Physics2D.IgnoreLayerCollision(playerLayer, platformLayerInt, true);

        yield return new WaitForSeconds(0.25f);

        Physics2D.IgnoreLayerCollision(playerLayer, platformLayerInt, false);
    }

    #endregion

    #region Grappling Hook Mechanics

    void ShootGrapplingHook()
    {
        if (isGrappling || isHookFlying) return;

        Vector3 screenPos = Mouse.current.position.ReadValue();
        screenPos.z = Mathf.Abs(Camera.main.transform.position.z - transform.position.z);
        Vector3 worldPos = Camera.main.ScreenToWorldPoint(screenPos);
        Vector2 mousePos = new Vector2(worldPos.x, worldPos.y);
        Vector2 grappleDirection = (mousePos - (Vector2)transform.position).normalized;

        isHookFlying = true;

        activeHookInstance = Instantiate(hookPrefab, shotSpawnPoint.position, Quaternion.identity);

        LayerMask attachableLayers = groundLayer | platformLayer;
        activeHookInstance.GetComponent<GrappleProjectile>().Initialize(this, grappleDirection, hookProjectileSpeed, grappleLifetime, attachableLayers);
    }

    void MovePlayerToHook()
    {
        rb.linearVelocity = Vector2.zero;
        rb.gravityScale = 0;

        bool pullingUpward = grapplePoint.y > transform.position.y;
        bool pullingDownward = grapplePoint.y < transform.position.y;
        bool holdingUp = playerMovement.y > 0;
        bool holdingDown = playerMovement.y < 0;

        bool isSeamlessDownwardPhase = hitIsPlatform && pullingDownward && holdingDown;

        if ((hitIsPlatform || PlatformChecker()) && ((pullingUpward && holdingUp) || (pullingDownward && holdingDown)))
        {
            Physics2D.IgnoreLayerCollision(playerLayer, platformLayerInt, true);
        }
        else
        {
            Physics2D.IgnoreLayerCollision(playerLayer, platformLayerInt, false);
        }

        float step = hookSpeed * Time.fixedDeltaTime;
        transform.position = Vector2.MoveTowards(transform.position, grapplePoint, step);

        float arrivalThreshold = isSeamlessDownwardPhase ? 0.85f : 0.4f;

        if (Vector2.Distance(transform.position, grapplePoint) < arrivalThreshold)
        {
            if (isSeamlessDownwardPhase)
            {
                Physics2D.IgnoreLayerCollision(playerLayer, platformLayerInt, true);
            }
            else
            {
                Physics2D.IgnoreLayerCollision(playerLayer, platformLayerInt, false);
            }

            hitIsPlatform = false;

            ResetGrappleState();

            if (isSeamlessDownwardPhase)
            {
                rb.linearVelocity = Vector2.down * hookSpeed;
            }
            else
            {
                MaintainGrappleMomentum();
            }
        }
    }

    void MaintainGrappleMomentum()
    {
        Vector2 launchDirection = (grapplePoint - (Vector2)transform.position).normalized;

        if (playerMovement.magnitude > 0)
        {
            launchDirection = (launchDirection + playerMovement).normalized;
        }

        currentHorizontalSpeed = launchDirection.x * hookSpeed;

        currentHorizontalSpeed = Mathf.Clamp(currentHorizontalSpeed, -speedClamp * 2f, speedClamp * 2f);

        rb.linearVelocity = Vector2.zero;
        rb.AddForce(launchDirection * hookSpeed, ForceMode2D.Impulse);
    }

    public void NotifyHookLanded(Vector2 hitPoint, GameObject hookInstance)
    {
        isHookFlying = false;
        grapplePoint = hitPoint;
        isGrappling = true;
        activeHookInstance = hookInstance;

        if (hookInstance.transform.parent != null)
        {
            int hitLayer = hookInstance.transform.parent.gameObject.layer;

            if (((1 << hitLayer) & platformLayer) != 0)
            {
                hitIsPlatform = true;
            }
            else
            {
                hitIsPlatform = false;
            }
        }
    }

    public void ResetGrappleState()
    {
        isGrappling = false;
        isHookFlying = false;
        rb.gravityScale = staticGravity;

        if (activeHookInstance != null)
        {
            activeHookInstance.transform.SetParent(null);
            Destroy(activeHookInstance);
            activeHookInstance = null;
        }
    }

    public Vector2 GetShotSpawnPosition()
    {
        return shotSpawnPoint.position;
    }

    #endregion

    #region Physics Status Checkers (Raycasts & Booleans)

    void GroundChecker()
    {
        if ((PlatformChecker() || IsGrounded()) && IsJumping() == false)
        {
            coyoteTimeCounter = coyoteTimeDuration;
            currentJumpAmount = jumpAmount;
            currentAirDashAmount = airDashAmount;
        }
        else if (isWallSliding)
        {
            coyoteTimeCounter = coyoteTimeDuration;
            currentAirDashAmount = airDashAmount;
        }
        else
        {
            coyoteTimeCounter -= Time.fixedDeltaTime;
        }
    }

    public bool IsGrounded()
    {
        RaycastHit2D hit = Physics2D.Raycast(transform.position, Vector2.down, groundCheckDistance, groundLayer);
        Debug.DrawRay(transform.position, Vector2.down * groundCheckDistance, Color.red);
        if (hit)
        {
            rb.gravityScale = staticGravity;
        }
        return hit;
    }

    public bool PlatformChecker()
    {
        RaycastHit2D hit = Physics2D.Raycast(transform.position, Vector2.down, groundCheckDistance, platformLayer);
        return hit;
    }

    public bool IsJumping()
    {
        return rb.linearVelocity.y > 0;
    }

    public bool IsFalling()
    {
        return rb.linearVelocity.y < 0.01f;
    }

    public bool JumpBufferChecker()
    {
        RaycastHit2D hit = Physics2D.Raycast(transform.position, Vector2.down, jumpBufferDistance, groundLayer);
        return hit;
    }

    #endregion
}
