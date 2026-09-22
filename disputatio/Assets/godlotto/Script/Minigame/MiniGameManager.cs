using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;
using System;
using Random = UnityEngine.Random;
using Unity.Cinemachine;

public class MiniGameManager : SingletonMonoBehaviour<MiniGameManager>
{
    public CinemachineCamera vcam;

    [Header("Game Settings")]
    public float gameDuration = 60f;
    public int maxHealth = 5;
    public Vector2 mapSize = new Vector2(5000, 3000); 

    [Header("Cursor Settings")]
    public Texture2D cursorTexture; 
    
    [Header("Spawn Settings")]
    // [수정] 플레이어 스폰 위치를 0, 0, 0으로 설정
    public Vector3 playerSpawnPos = Vector3.zero; 
    // [추가] 적 소환 시 최소/최대 거리 설정 (캐릭터 크기 400x300 고려)
    public float minEnemyDistance = 700f; 
    public float maxEnemyDistance = 1200f;

    [Header("Prefabs (Assign in Inspector)")]
    public GameObject playerPrefab;
    public GameObject enemyPrefab;
    public GameObject projectilePrefab;
    public GameObject exitPrefab;

    private GameObject playerInstance; 
    private int currentHealth;
    private bool isGameOver = false;

    void Start()
    {
        if (cursorTexture != null)
        {
            Vector2 hotspot = new Vector2(cursorTexture.width / 2, cursorTexture.height / 2);
            Cursor.SetCursor(cursorTexture, hotspot, CursorMode.Auto);
        }

        currentHealth = maxHealth;
        
        SpawnPlayer();
        SpawnExit();
        
        StartCoroutine(EnemySpawner());
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0)) 
        {
            Cursor.lockState = CursorLockMode.Confined;
        }
    }

    void SpawnPlayer()
    {
        if (playerPrefab != null)
        {
            playerInstance = Instantiate(playerPrefab, playerSpawnPos, Quaternion.identity);
            GameLog.Log($"플레이어가 {playerSpawnPos} 위치에 소환되었습니다.");
        }
        else
        {
            Debug.LogError("Player Prefab이 할당되지 않았습니다!");
        }

        if (vcam != null && playerInstance != null)
        {
            vcam.Follow = playerInstance.transform;
        }
    }

    void SpawnExit()
    {
        // [참고] 플레이어가 (0,0,0)에 소환되므로 탈출구 위치도 조정이 필요할 수 있습니다.
        Vector3 exitPos = new Vector3(2500, 0, 0); 
        Instantiate(exitPrefab, exitPos, Quaternion.identity);
    }

    IEnumerator EnemySpawner()
    {
        while (!isGameOver)
        {
            yield return new WaitForSeconds(0.5f);
            SpawnEnemy();
        }
    }

    // [수정] 캐릭터를 중심으로 사방에서 적 소환
    void SpawnEnemy()
{
    if (playerInstance == null) return;

    // 사방 소환 위치 계산
    float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
    float distance = 800f; // 캐릭터 크기 400x300 고려
    Vector3 spawnOffset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * distance;
    Vector3 spawnPos = playerInstance.transform.position + spawnOffset;

    GameObject enemy = Instantiate(enemyPrefab, spawnPos, Quaternion.identity);

    // [수정] 소환된 적에게 플레이어 정보를 전달하여 추격 및 방향 전환 시작
    MiniGameEnemy enemyScript = enemy.GetComponent<MiniGameEnemy>();
    if (enemyScript != null)
    {
        enemyScript.SetTarget(playerInstance.transform); 
    }
}

    public void TakeDamage()
    {
        currentHealth--;
        if (currentHealth <= 0)
        {
            GameOver();
        }
    }

    void GameOver()
    {
        isGameOver = true;

        // 1. 유니티의 시간 흐름을 0으로 만들어 게임을 멈춤
        Time.timeScale = 0f;

        // 2. 갇혀있던 커서를 풀고 다시 보이게 설정
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        GameLog.Log("Game Over! 모든 동작이 정지되었습니다.");
        
        // 여기에 게임 오버 UI를 띄우는 코드를 추가하면 좋습니다.
        // gameOverUI.SetActive(true); 
    }

    public void Win()
    {
        isGameOver = true;
        GameLog.Log("Escaped! Transitioning to 1st Person...");
    }
}