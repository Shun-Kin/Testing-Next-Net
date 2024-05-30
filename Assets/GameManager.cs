using UnityEngine;
using System.Collections.Generic;
using Mirror;


public class GameManager : NetworkBehaviour
{
    private static GameManager instance;
    private static readonly RaycastHit[] hits = new RaycastHit[3];  // ����� ��� RaycastNonAlloc
    private static readonly Collider[] colliders = new Collider[1]; // ����� ��� OverlapSphereNonAlloc

    // ���������� ��� ������ ������
    public GameObject playerPrefab;
    public Transform playerSpawn;
    public float playerSpawnRadius;     // ������ �� ����� playerSpawn, � ������� ����� ��������� �����
    public float playerOverlapRadius;   // ������ ����� ������ ������, ��� ����������� ���������� ������������

    // ���������� ��� ������ �������
    public GameObject medkitPrefab;
    public int medkitCount;
    public Transform medkitSpawn;
    public float medkitSpawnRadius;
    public float medkitOverlapRadius;


    void Start()
    {
        instance = this;

        // ������ ������� �������
        if (isServer)
        {
            Vector3 randPosition;
            Quaternion randRotation = Quaternion.identity;

            for (int i = 0; i < medkitCount; i++)
            {
                randPosition = GetRandomSpawnPosition(medkitSpawn.position, medkitSpawnRadius, medkitOverlapRadius, 0);
                randRotation.y = Random.Range(0f, 1f);
                NetworkServer.Spawn(Instantiate(medkitPrefab, randPosition, randRotation, medkitSpawn));    // ������ ������� ������� �� ��������
            }
        }
    }

    // ���������� ����� ������ ��� ������
    public static Vector3 GetPlayerSpawnPosition()
    {
        return GetRandomSpawnPosition(instance.playerSpawn.position, instance.playerSpawnRadius, instance.playerOverlapRadius, -1.45f);
    }

    // ���������� ��������� ����� �� ��������� ������������� (positionYOffset - �������� �� ��� Y ��� ������������� ������ ������)
    public static Vector3 GetRandomSpawnPosition(Vector3 spawnPoint, float spawnRadius, float overlapRadius, float positionYOffset)
    {
        Vector3 randPosition = Vector3.zero;
        int hitCount;

        do
        {
            // ������� ������ � ��������� �����
            randPosition.Set(spawnPoint.x + Random.Range(-spawnRadius, spawnRadius), 11f, spawnPoint.z + Random.Range(-spawnRadius, spawnRadius));
            hitCount = Physics.RaycastNonAlloc(randPosition, Vector3.down, hits, 12f);
            randPosition.y = hits[Random.Range(0, hitCount)].point.y + overlapRadius + 0.05f;   // ������� ��������� ��� ������� �� ����, ������� �������� �������� ������, ��� ������� ������������� ������

            hitCount = Physics.OverlapSphereNonAlloc(randPosition, overlapRadius, colliders);   // ��������� ��������� ������������ ������
            if (hitCount == 0)                                                                  // ���� � ����� ��� ��������, �� ����� ������ �������, ����� ��������� �����
            {
                randPosition.y += positionYOffset;
                return randPosition;
            }
        } while (true);
    }

    // ������ ������� ������ ��� ����������
    public void _ReplacePlayer(NetworkConnectionToClient conn)
    {
        GameObject oldPlayer = conn.identity.gameObject;

        NetworkServer.ReplacePlayerForConnection(conn, Instantiate(playerPrefab), true);
        Destroy(oldPlayer, 0.1f);
    }

    // ������� ������ � ��������� ������ �������
    public static void ReplacePlayer(NetworkConnectionToClient conn)
    {
        GameObject oldPlayer = conn.identity.gameObject;
        GameObject newPlayer = Instantiate(instance.playerPrefab);

        CustomPlayerController player = newPlayer.GetComponent<CustomPlayerController>();
        player.playerName = oldPlayer.GetComponent<CustomPlayerController>().playerName;
        player.transform.position = GetPlayerSpawnPosition();

        NetworkServer.ReplacePlayerForConnection(conn, newPlayer, true);
        Destroy(oldPlayer, 0.1f);   // ������� ���������� ������ ������, ������� ������ ������. �������� ���������� ��� ���������� ������
    }

    // ���������� ���� ��� ������� � ���� �������� ��������
    public static void SetLayerWithChildren(Transform root, int layer)
    {
        Queue<Transform> queue = new Queue<Transform>();
        queue.Enqueue(root);    // ��������� ������������� ��������� �������� �������

        while (queue.Count > 0)
        {
            // ������� ������������� �� ������� � ���������� ����
            Transform current = queue.Dequeue();
            current.gameObject.layer = layer;

            // ��������� � ������� ������������� �������� ��������
            foreach (Transform child in current)
            {
                queue.Enqueue(child);
            }
        }
    }

    public void StopGame()
    {
        if (NetworkServer.active && NetworkClient.isConnected)  // ��������� �����
        {
            NetworkManager.singleton.StopHost();
        }
        else if (NetworkClient.isConnected)                     // ��������� �������
        {
            NetworkManager.singleton.StopClient();
        }
        else if (NetworkServer.active)                          // ��������� �������
        {
            NetworkManager.singleton.StopServer();
        }
    }
}
