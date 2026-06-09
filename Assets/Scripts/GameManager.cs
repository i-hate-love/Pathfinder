using UnityEngine;

public class GameManager : MonoBehaviour
{
    [Header("References")]
    public Transform player;
    public GameObject[] enemyPrefabs;

    [Header("Spawn Area")]
    public float minSpawnRadius = 4f;
    public float maxSpawnRadius = 8f;
    public float spawnHeight = 2f;
    public LayerMask groundMask;

    [Header("Checks")]
    public float groundCheckDistance = 10f;
    public float overlapCheckRadius = 0.6f;
    public LayerMask obstacleMask;

    public void SpawnRandomEnemy()
    {
        if (player == null || enemyPrefabs == null || enemyPrefabs.Length == 0)
            return;

        for (int attempt = 0; attempt < 12; attempt++)
        {
            Vector2 circle = Random.insideUnitCircle.normalized * Random.Range(minSpawnRadius, maxSpawnRadius);

            Vector3 candidate = new Vector3(
                player.position.x + circle.x,
                player.position.y + groundCheckDistance,
                player.position.z + circle.y
            );

            if (Physics.Raycast(candidate, Vector3.down, out RaycastHit hit, groundCheckDistance * 2f, groundMask))
            {
                Vector3 spawnPos = hit.point + Vector3.up * spawnHeight;

                bool blocked = Physics.CheckSphere(spawnPos, overlapCheckRadius, obstacleMask);
                if (blocked) continue;

                GameObject prefab = enemyPrefabs[Random.Range(0, enemyPrefabs.Length)];
                Instantiate(prefab, spawnPos, Quaternion.identity);
                return;
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        if (player == null) return;

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(player.position, minSpawnRadius);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(player.position, maxSpawnRadius);
    }
}
