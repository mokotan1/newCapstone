using UnityEngine;

public class MiniGameEnemy : MonoBehaviour
{
    public float moveSpeed = 150f;
    private Transform player;
    
    // 가루 효과 컴포넌트 및 상태 변수
    private SpriteDissolver dissolver;
    private SpriteRenderer sr; 
    private bool isDead = false;

    void Start()
    {
        sr = GetComponent<SpriteRenderer>();

        // 플레이어 태그로 찾기
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) player = playerObj.transform;

        dissolver = GetComponent<SpriteDissolver>();

        // [추가] 적이 물리적인 힘에 밀려나지 않도록 설정
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            // Kinematic으로 설정하면 AddForce 등의 물리 힘에 영향을 받지 않습니다.
            rb.bodyType = RigidbodyType2D.Kinematic;
            // 회전 방지
            rb.freezeRotation = true;
        }
    }

    void Update()
    {
        if (player != null && !isDead)
        {
            // 1. 추격 로직
            Vector2 direction = (player.position - transform.position).normalized;
            transform.position += (Vector3)direction * moveSpeed * Time.deltaTime;

            // 2. 방향 전환(Flip) 로직
            // 플레이어가 적보다 왼쪽에 있으면(x값이 작으면) 이미지를 뒤집습니다(true).
            if (sr != null)
            {
                sr.flipX = player.position.x > transform.position.x;
            }
        }
    }

    public void SetTarget(Transform target)
    {
        player = target;
    }

    // [수정] 적의 죽음 감지 로직 (플레이어 공격 태그 확인)
    void OnTriggerEnter2D(Collider2D other)
    {
        if (isDead) return;

        // 플레이어의 공격(히트박스)에 닿았을 때만 가루가 되어 사라짐
        if (other.CompareTag("PlayerAttack") || other.CompareTag("Melee") || other.CompareTag("Projectile"))
        {
            if (other.CompareTag("Projectile")) Destroy(other.gameObject);
            StartDeathSequence();
        }
    }

    void StartDeathSequence()
    {
        isDead = true;

        // 충돌체 비활성화
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        // 가루 효과 실행
        if (dissolver != null)
        {
            dissolver.StartDissolve();
            GameLog.Log("실행됨");
        }
        else
        {
            Destroy(gameObject);
        }
    }
}