using UnityEngine;
using System.Collections;

public class MiniGamePlayer : MonoBehaviour
{
    public float moveSpeed = 300f;
    // PPU 1(400x300) 환경에서는 넉백 힘이 2000~5000 사이여야 체감이 됩니다.
    public float knockbackForce = 2500f; 
    
    [Header("Invincibility Settings")]
    public float invincibilityDuration = 0.5f;
    private bool isInvincible = false;
    private bool isKnockedBack = false; 
    private bool isDead = false; // [해결] missing variable 추가

    [Header("Melee Attack Settings")]
    public GameObject meleeHitboxPrefab;
    public float attackDuration = 0.2f;
    public float attackCooldown = 0.5f;

    [Header("Animation Settings")]
    public SpriteRenderer spriteRenderer; 
    public Animator animator; 

    private Vector2 moveInput;
    private Rigidbody2D rb;
    private bool isAttacking = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        
        // [회전 고정] 물리 연산으로 인해 캐릭터가 도는 것을 방지합니다.
        rb.freezeRotation = true;
        // 물리 보간 설정으로 넉백을 부드럽게 표현합니다.
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        if (animator == null) animator = GetComponentInChildren<Animator>();
        
        // 애니메이터 컨트롤러 자동 로드
        RuntimeAnimatorController controller = Resources.Load<RuntimeAnimatorController>("측면으로 걷는 모션2_0");
        if (controller != null) animator.runtimeAnimatorController = controller;
    }

    void Update()
    {
        if (isDead) return;

        moveInput.x = Input.GetAxisRaw("Horizontal");
        moveInput.y = Input.GetAxisRaw("Vertical");

        UpdateVisuals();

        if (Input.GetMouseButtonDown(0) && !isAttacking)
        {
            StartCoroutine(PerformMeleeAttack());
        }
    }

    void UpdateVisuals()
    {
        if (isDead) return; 

        bool isMoving = Mathf.Abs(moveInput.x) > 0 || Mathf.Abs(moveInput.y) > 0;

        // 공격 중에는 마우스 방향을 고수하고, 아닐 때만 이동 방향을 봅니다.
        if (!isAttacking)
        {
            if (moveInput.x > 0) spriteRenderer.flipX = false;
            else if (moveInput.x < 0) spriteRenderer.flipX = true;
        }

        if (animator != null)
        {
            animator.SetBool("isMoving", isMoving);
            if (isMoving && !animator.GetCurrentAnimatorStateInfo(0).IsName("WalkRight"))
            {
                animator.Play("WalkRight");
            }
        }
    }

    void FixedUpdate()
    {
        if (isDead) return;

        // 넉백(Knockback) 중에는 물리 힘에 의해 밀려나야 하므로 조작을 잠시 막습니다.
        if (!isKnockedBack)
        {
            rb.linearVelocity = moveInput.normalized * moveSpeed * Time.fixedDeltaTime * 50f;
        }
    }

    IEnumerator PerformMeleeAttack()
    {
        isAttacking = true;
        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mousePos.z = 0;
        Vector2 attackDir = (mousePos - transform.position).normalized;
        float angle = Mathf.Atan2(attackDir.y, attackDir.x) * Mathf.Rad2Deg;

        // 공격하는 순간 마우스 방향 바라보기
        if (attackDir.x > 0) spriteRenderer.flipX = false;
        else if (attackDir.x < 0) spriteRenderer.flipX = true;

        if (animator != null) animator.SetTrigger("Attack");

        if (meleeHitboxPrefab != null)
        {
            float spawnDistance = (Mathf.Abs(attackDir.y) > Mathf.Abs(attackDir.x)) ? 340f : 260f;
            GameObject hitbox = Instantiate(meleeHitboxPrefab, 
                transform.position + (Vector3)attackDir * spawnDistance, 
                Quaternion.Euler(0, 0, angle));
            hitbox.transform.SetParent(transform);
            Destroy(hitbox, attackDuration);
        }

        yield return new WaitForSeconds(attackCooldown);
        isAttacking = false;
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        // "Enemy" 태그를 가진 물체와 부딪혔고 무적이 아닐 때만 실행
        if (collision.gameObject.CompareTag("Enemy") && !isInvincible)
        {
            if (MiniGameManager.Instance != null) MiniGameManager.Instance.TakeDamage();
            
            // 넉백 방향 계산: 적의 위치로부터 나(주인공)를 밀어내는 방향
            Vector2 knockDir = (transform.position - collision.transform.position).normalized;
            
            StartCoroutine(KnockbackRoutine());
            StartCoroutine(InvincibilityRoutine());

            // [주인공 밀치기] AddForce를 사용하여 주인공을 뒤로 밀어냅니다.
            rb.AddForce(knockDir * knockbackForce, ForceMode2D.Impulse);
        }
    }

    IEnumerator KnockbackRoutine()
    {
        isKnockedBack = true;
        yield return new WaitForSeconds(0.2f); // 0.2초간 조작 불가 (밀려나는 시간)
        isKnockedBack = false;
    }

    IEnumerator InvincibilityRoutine()
    {
        isInvincible = true;
        float timer = 0;
        while (timer < invincibilityDuration)
        {
            spriteRenderer.enabled = !spriteRenderer.enabled;
            yield return new WaitForSeconds(0.1f);
            timer += 0.1f;
        }
        spriteRenderer.enabled = true;
        isInvincible = false;
    }
}