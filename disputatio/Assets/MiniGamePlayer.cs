using UnityEngine;
using System.Collections;

public class MiniGamePlayer : MonoBehaviour
{
    public float moveSpeed = 300f;
    public float knockbackForce = 15f;
    
    [Header("Melee Attack Settings")]
    public GameObject meleeHitboxPrefab; // 휘두르는 공격용 히트박스 프리팹
    public float attackDuration = 0.2f;  // 공격 지속 시간
    public float attackCooldown = 0.5f;  // 공격 쿨타임

    private Vector2 moveInput;
    private Rigidbody2D rb;
    private bool isAttacking = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        // Simple WASD/Arrow movement
        moveInput.x = Input.GetAxisRaw("Horizontal");
        moveInput.y = Input.GetAxisRaw("Vertical");

        if (Input.GetMouseButtonDown(0) && !isAttacking)
        {
            StartCoroutine(PerformMeleeAttack());
        }
    }

    void FixedUpdate()
    {
        // 최신 Unity 버전에 맞춰 linearVelocity 사용
        rb.linearVelocity = moveInput.normalized * moveSpeed * Time.fixedDeltaTime * 50f;
    }

    IEnumerator PerformMeleeAttack()
    {
        isAttacking = true;

        // 마우스 방향 계산
        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mousePos.z = 0;
        Vector2 attackDir = (mousePos - transform.position).normalized;
        float angle = Mathf.Atan2(attackDir.y, attackDir.x) * Mathf.Rad2Deg;

        // 공격 히트박스 생성 및 회전 (휘두르는 연출)
        if (meleeHitboxPrefab != null)
        {
            // 플레이어 앞쪽에 히트박스 생성
            GameObject hitbox = Instantiate(meleeHitboxPrefab, transform.position + (Vector3)attackDir * 50f, Quaternion.Euler(0, 0, angle));
            hitbox.transform.SetParent(transform); // 플레이어와 함께 이동
            Destroy(hitbox, attackDuration);
        }

        yield return new WaitForSeconds(attackCooldown);
        isAttacking = false;
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Enemy"))
        {
            MiniGameManager.Instance.TakeDamage();
            // Knockback
            Vector2 knockDir = (transform.position - collision.transform.position).normalized;
            transform.position += (Vector3)knockDir * knockbackForce;
        }
    }
}
