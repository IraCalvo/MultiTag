using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

//Dont forget to add head bumper space thingy

public class PlayerController : MonoBehaviour
{
    [Header("Player States")]
    [SerializeField] float timerBetweenIt;
    [SerializeField] bool isIt = false;
    [SerializeField] bool canBeTagged = true;

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

    [Header("Dash")]
    [SerializeField] int airDashAmount;
    [SerializeField] float airDashForce;
    [SerializeField] float dashCoolDown;
    bool isDashing = false;
    [SerializeField] int currentAirDashAmount;

    [Header("Misc")]
    [SerializeField] int playerLayer;
    [SerializeField] Transform groundCheck;
    [SerializeField] LayerMask groundLayer;
    [SerializeField] float groundCheckDistance;
    [SerializeField] float jumpBufferDistance;
    [SerializeField] LayerMask platformLayer;
    [SerializeField] int platformLayerInt;
    Rigidbody2D rb;
    Vector2 playerMovement;
    int currentJumpAmount;
    float currentHorizontalSpeed;
    private int currentJumpBufferTimer;
    private int staticGravity = 3;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        currentJumpAmount = jumpAmount;
        currentAirDashAmount = airDashAmount;
    }

    private void FixedUpdate()
    {
        MovePlayer();
        PeakOfJump();
        PlayerFastFall();
        GroundChecker();
        PlatformChecker();
        FallSpeedClamper();
    }

    void OnMove(InputValue value) 
    {
        playerMovement = value.Get<Vector2>();
        if (playerMovement.y < 0 && PlatformChecker())
        {
            Debug.Log("Entered");
            StartCoroutine(FallThroughPlatform());
        }
    }

    void OnJump(InputValue value)
    {
        //AirJump
        if (currentJumpAmount > 0 && IsGrounded() == false && JumpBufferChecker() == false)
        {
            rb.velocity = new Vector2(rb.velocity.x, 0f);
            rb.AddForce(Vector2.up * airJumpForce, ForceMode2D.Impulse);
            currentJumpAmount--;
        }
        //normal jump
        else if (currentJumpAmount > 0 && IsGrounded())
        {
            currentJumpAmount--;
            rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
        }
        //jump buffer
        else if (IsFalling() && IsGrounded() == false && JumpBufferChecker())
        {
            StartCoroutine(JumpWhenGrounded());
            Debug.Log("Jump buffered correctly");
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

    void MovePlayer()
    {
        if (isDashing) { return; }

        if (playerMovement.x != 0)
        {
            currentHorizontalSpeed += playerMovement.x * moveSpeed * Time.deltaTime;
            currentHorizontalSpeed = Mathf.Clamp(currentHorizontalSpeed, -speedClamp, speedClamp);
        }
        else
        {
            currentHorizontalSpeed = Mathf.MoveTowards(currentHorizontalSpeed, 0, decceleration * Time.deltaTime);
        }

        rb.velocity = new Vector2(currentHorizontalSpeed, rb.velocity.y);

    }

    void PeakOfJump()
    {
        if (rb.velocity.y < 0)
        {
            rb.gravityScale = peakOfJumpGravityMultiplier;
        }
    }

    void PlayerFastFall()
    {
        if (playerMovement.y < 0 && IsGrounded() == false)
        {
            if (rb.velocity.y > 0)
            {
                rb.velocity = new Vector2(rb.velocity.x, -0.5f);
            }
            rb.gravityScale = fastFallMultiplier;
        }
    }

    void GroundChecker()
    {
        if ((PlatformChecker() ||IsGrounded()) && IsJumping() == false)
        {
            currentJumpAmount = jumpAmount;
            currentAirDashAmount = airDashAmount;
        }
    }

    void FallSpeedClamper()
    {
        if (rb.velocity.y < fallSpeedClamp)
        {
            rb.velocity = new Vector2(rb.velocity.x, fallSpeedClamp);
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
        return rb.velocity.y > 0;
    }

    public bool IsFalling()
    {
        return rb.velocity.y < 0;
    }

    public bool JumpBufferChecker()
    {
        RaycastHit2D hit = Physics2D.Raycast(transform.position, Vector2.down, jumpBufferDistance, groundLayer);
        return hit;
    }

    private void OnCollisionEnter2D(Collision2D otherCollision)
    {
        if (otherCollision.gameObject.tag == "Player" && canBeTagged)
        {
            canBeTagged = false;
            isIt = false;
            otherCollision.gameObject.GetComponent<PlayerController>().isIt = true;
            StartCoroutine(InvincibilityCountdown());
        }
    }

    IEnumerator FallThroughPlatform()
    {
        Physics.IgnoreLayerCollision(playerLayer, platformLayerInt, true);
        rb.simulated = true;

        yield return new WaitForSeconds(3);

        //rb.simulated = true;
        //Physics.IgnoreLayerCollision(playerLayer, platformLayerInt, false);
    }

    IEnumerator InvincibilityCountdown()
    {
        yield return new WaitForSeconds(timerBetweenIt);
        canBeTagged = true;
    }

    IEnumerator Dash(Vector2 direction)
    {
        currentAirDashAmount--;
        isDashing = true;
        rb.AddForce(direction * airDashForce, ForceMode2D.Impulse);
        rb.gravityScale = 0;
        yield return new WaitForSeconds(dashCoolDown);
        rb.gravityScale = staticGravity;
        isDashing = false;
    }

    IEnumerator JumpWhenGrounded()
    {
        while (IsGrounded() == false)
        {
            yield return null;
        }
        currentJumpAmount--;
        rb.gravityScale = staticGravity;
        rb.velocity = new Vector2(rb.velocity.x, 0f);
        rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
        Debug.Log("Launched");
    }
}
