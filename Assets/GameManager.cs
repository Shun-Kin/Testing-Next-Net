using UnityEngine;
using System.Collections.Generic;
using Mirror;


public class GameManager : NetworkBehaviour
{
    private static GameManager instance;
    private static readonly RaycastHit[] hits = new RaycastHit[3];  // Буфер для RaycastNonAlloc
    private static readonly Collider[] colliders = new Collider[1]; // Буфер для OverlapSphereNonAlloc

    // Информация для спавна игрока
    public GameObject playerPrefab;
    public Transform playerSpawn;
    public float playerSpawnRadius;     // Радиус от точки playerSpawn, в котором будет заспавнен игрок
    public float playerOverlapRadius;   // Радиус сферы спавна игрока, для определения свободного пространства

    // Информация для спавна аптечек
    public GameObject medkitPrefab;
    public int medkitCount;
    public Transform medkitSpawn;
    public float medkitSpawnRadius;
    public float medkitOverlapRadius;


    void Start()
    {
        instance = this;

        // Сервер спавнит аптечки
        if (isServer)
        {
            Vector3 randPosition;
            Quaternion randRotation = Quaternion.identity;

            for (int i = 0; i < medkitCount; i++)
            {
                randPosition = GetRandomSpawnPosition(medkitSpawn.position, medkitSpawnRadius, medkitOverlapRadius, 0);
                randRotation.y = Random.Range(0f, 1f);
                NetworkServer.Spawn(Instantiate(medkitPrefab, randPosition, randRotation, medkitSpawn));    // Сервер спавнит аптечки на клиентах
            }
        }
    }

    // Возвращает точку спавна для игрока
    public static Vector3 GetPlayerSpawnPosition()
    {
        return GetRandomSpawnPosition(instance.playerSpawn.position, instance.playerSpawnRadius, instance.playerOverlapRadius, -1.45f);
    }

    // Возвращает случайную точку со свободным пространством (positionYOffset - смещение по оси Y для корректировки высоты спавна)
    public static Vector3 GetRandomSpawnPosition(Vector3 spawnPoint, float spawnRadius, float overlapRadius, float positionYOffset)
    {
        Vector3 randPosition = Vector3.zero;
        int hitCount;

        do
        {
            // Рейкаст сверху в случайную точку
            randPosition.Set(spawnPoint.x + Random.Range(-spawnRadius, spawnRadius), 11f, spawnPoint.z + Random.Range(-spawnRadius, spawnRadius));
            hitCount = Physics.RaycastNonAlloc(randPosition, Vector3.down, hits, 12f);
            randPosition.y = hits[Random.Range(0, hitCount)].point.y + overlapRadius + 0.05f;   // Рейкаст пробивает все объекты на пути, поэтому выбираем случайно объект, над которым будем спавнить объект

            hitCount = Physics.OverlapSphereNonAlloc(randPosition, overlapRadius, colliders);   // Проверить свободное пространство сферой
            if (hitCount == 0)                                                                  // Если в сфере нет объектов, то точка спавна найдена, иначе повторить поиск
            {
                randPosition.y += positionYOffset;
                return randPosition;
            }
        } while (true);
    }

    // Замена объекта игрока для соединения
    public void _ReplacePlayer(NetworkConnectionToClient conn)
    {
        GameObject oldPlayer = conn.identity.gameObject;

        NetworkServer.ReplacePlayerForConnection(conn, Instantiate(playerPrefab), true);
        Destroy(oldPlayer, 0.1f);
    }

    // Респавн игрока с созданием нового объекта
    public static void ReplacePlayer(NetworkConnectionToClient conn)
    {
        GameObject oldPlayer = conn.identity.gameObject;
        GameObject newPlayer = Instantiate(instance.playerPrefab);

        CustomPlayerController player = newPlayer.GetComponent<CustomPlayerController>();
        player.playerName = oldPlayer.GetComponent<CustomPlayerController>().playerName;
        player.transform.position = GetPlayerSpawnPosition();

        NetworkServer.ReplacePlayerForConnection(conn, newPlayer, true);
        Destroy(oldPlayer, 0.1f);   // Удалить предыдущий объект игрока, который теперь заменён. Задержка необходима для завершения замены
    }

    // Установить слой для объекта и всех дочерних объектов
    public static void SetLayerWithChildren(Transform root, int layer)
    {
        Queue<Transform> queue = new Queue<Transform>();
        queue.Enqueue(root);    // Поместить трансформацию основного объектав очередь

        while (queue.Count > 0)
        {
            // Извлечь трансформацию из очереди и установить слой
            Transform current = queue.Dequeue();
            current.gameObject.layer = layer;

            // Поместить в очередь трансформации дочерних объектов
            foreach (Transform child in current)
            {
                queue.Enqueue(child);
            }
        }
    }

    public static void StopGame()
    {
        if (NetworkServer.active && NetworkClient.isConnected)  // Остановка хоста
        {
            NetworkManager.singleton.StopHost();
        }
        else if (NetworkClient.isConnected)                     // Остановка клиента
        {
            NetworkManager.singleton.StopClient();
        }
        else if (NetworkServer.active)                          // Остановка сервера
        {
            NetworkManager.singleton.StopServer();
        }
    }
}
