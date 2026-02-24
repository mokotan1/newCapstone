using UnityEngine;

public class MiniGameEnemy : MonoBehaviour
{
    public float moveSpeed = 150f;
    private Transform player;

    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player").transform;
    }

    void Update()
    {
        if (player != null)
        {
            Vector2 direction = (player.position - transform.position).normalized;
            transform.position += (Vector3)direction * moveSpeed * Time.deltaTime;
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // Projectile(발사체) 또는 Melee(휘두르기) 히트박스에 닿으면 소멸
        if (other.CompareTag("Projectile") || other.CompareTag("Melee"))
        {
            if (other.CompareTag("Projectile")) Destroy(other.gameObject);
            Destroy(gameObject);
        }
    }
}
