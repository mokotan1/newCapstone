using UnityEngine;
using System.Collections;

public class MiniGamePlayer : MonoBehaviour
{
    public float moveSpeed = 300f;
    public float knockbackForce = 15f;
    
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
        
        // [수정] 애니메이터가 자식 오브젝트에 있으므로 GetComponentInChildren을 사용합니다.
        if (animator == null) animator = GetComponentInChildren<Animator>();

        // Resources 폴더에서 컨트롤러 로드 (파일 이름: 측면으로 걷는 모션2_0)
        RuntimeAnimatorController controller = Resources.Load<RuntimeAnimatorController>("측면으로 걷는 모션2_0");

        if (controller != null)
        {
            animator.runtimeAnimatorController = controller;
            print("애니메이터 컨트롤러 로드 성공!");
        }
        else
        {
            Debug.LogError("Resources 폴더에 컨트롤러 파일이 없거나 이름이 틀립니다.");
        }
    }

    void Update()
    {
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
    // [수정] Input.GetKey 대신 이미 계산된 moveInput 값을 사용합니다.
    // 이렇게 하면 화살표 키(Arrow Keys)로 움직여도 애니메이션이 나옵니다.
    bool isMovingHorizontal = Mathf.Abs(moveInput.x) > 0;
    bool isMovingVertical = Mathf.Abs(moveInput.y) > 0;

    bool isMoving = isMovingHorizontal || isMovingVertical;

    // 1. 방향에 따라 이미지 반전 (moveInput.x 사용)
    if (moveInput.x > 0) 
    {
        spriteRenderer.flipX = false; // 오른쪽
    }
    else if (moveInput.x < 0) 
    {
        spriteRenderer.flipX = true;  // 왼쪽
    }

    // 2. 애니메이터 제어
    if (animator != null)
    {
        animator.SetBool("isMoving", isMoving);
        
        // 애니메이터 노드 이름이 "WalkRight" 인지 다시 확인하세요!
        if (isMoving && !animator.GetCurrentAnimatorStateInfo(0).IsName("WalkRight"))
        {
            animator.Play("WalkRight");
        }
    }
}



    void FixedUpdate()
    {
        // 유니티 6의 최신 물리 엔진 API 사용
        rb.linearVelocity = moveInput.normalized * moveSpeed * Time.fixedDeltaTime * 50f;
    }

    IEnumerator PerformMeleeAttack()
    {
        isAttacking = true;
        if (animator != null) animator.SetTrigger("Attack");

        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mousePos.z = 0;
        Vector2 attackDir = (mousePos - transform.position).normalized;
        float angle = Mathf.Atan2(attackDir.y, attackDir.x) * Mathf.Rad2Deg;

        if (meleeHitboxPrefab != null)
        {
            // y축의 절대값이 x축보다 크면 '위아래' 공격으로 판단합니다.
            float spawnDistance = (Mathf.Abs(attackDir.y) > Mathf.Abs(attackDir.x)) ? 1.7f : 1.3f;

            // 결정된 거리(spawnDistance)를 곱하여 생성 위치를 정합니다.
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
        if (collision.gameObject.CompareTag("Enemy"))
        {
            // MiniGameManager.Instance.TakeDamage()가 구현되어 있어야 합니다.
            if (MiniGameManager.Instance != null) MiniGameManager.Instance.TakeDamage();
            
            Vector2 knockDir = (transform.position - collision.transform.position).normalized;
            rb.AddForce(knockDir * knockbackForce, ForceMode2D.Impulse);
        }
    }
}