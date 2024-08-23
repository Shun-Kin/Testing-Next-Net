using System.Collections.Generic;
using UnityEngine;
using Mirror;

public class GameManager : NetworkBehaviour
{
    private const int RaycastHitsCount = 3;
    private const int OverlapCollidersCount = 1;
    private const float RandomRotationYMax = 1f;
    private const float RaycastStartY = 11f;
    private const float RaycastDistance = 12f;
    private const float PlayerYOffset = -1.45f;
    private const float OverlapPositionYOffset = 0.05f;

    private static GameManager instance;
    private static readonly RaycastHit[] raycastHits = new RaycastHit[RaycastHitsCount];        // Буфер для метода RaycastNonAlloc
    private static readonly Collider[] overlapColliders = new Collider[OverlapCollidersCount];  // Буфер для метода OverlapSphereNonAlloc

    // Информация для спавна игрока
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private Transform playerSpawn;
    [SerializeField] private float playerSpawnRadius;   // Радиус от точки playerSpawn, в котором будет заспавнен игрок
    [SerializeField] private float playerOverlapRadius; // Радиус сферы спавна игрока, для определения свободного пространства

    // Информация для спавна аптечек
    [SerializeField] private GameObject medkitPrefab;
    [SerializeField] private Transform medkitSpawn;
    [SerializeField] private float medkitSpawnRadius;
    [SerializeField] private float medkitOverlapRadius;
    [SerializeField] private int medkitCount;


    private void Start()
    {
        instance = this;

        // Сервер спавнит аптечки
        if (isServer)
        {
            Vector3 randomPosition;
            Quaternion randomRotation = Quaternion.identity;

            for (int i = 0; i < medkitCount; i++)
            {
                randomPosition = GetRandomSpawnPosition(medkitSpawn.position, medkitSpawnRadius, medkitOverlapRadius);
                randomRotation.y = Random.Range(0f, RandomRotationYMax);
                NetworkServer.Spawn(Instantiate(medkitPrefab, randomPosition, randomRotation, medkitSpawn));    // Сервер спавнит аптечки на клиентах
            }
        }
    }

    /// <summary>Возвращает точку спавна для игрока</summary>
    public static Vector3 GetPlayerSpawnPosition()
    {
        return GetRandomSpawnPosition(instance.playerSpawn.position, instance.playerSpawnRadius, instance.playerOverlapRadius, PlayerYOffset);
    }

    /// <summary>Возвращает случайную точку со свободным пространством</summary>
    private static Vector3 GetRandomSpawnPosition(Vector3 spawnPoint, float spawnRadius, float overlapRadius, float positionYOffset = 0f)
    {
        Vector3 randomPosition = Vector3.zero;
        int hitCount;

        do
        {
            // Рейкаст сверху в случайную точку
            randomPosition.Set(spawnPoint.x + Random.Range(-spawnRadius, spawnRadius), RaycastStartY, spawnPoint.z + Random.Range(-spawnRadius, spawnRadius));
            hitCount = Physics.RaycastNonAlloc(randomPosition, Vector3.down, raycastHits, RaycastDistance);
            randomPosition.y = raycastHits[Random.Range(0, hitCount)].point.y + overlapRadius + OverlapPositionYOffset; // Рейкаст пробивает все объекты на пути, поэтому выбираем случайно объект, над которым будем спавнить объект

            hitCount = Physics.OverlapSphereNonAlloc(randomPosition, overlapRadius, overlapColliders);                  // Проверить свободное пространство сферой
            if (hitCount == 0)  // Если в сфере нет объектов, то точка спавна найдена, иначе повторить поиск
            {
                randomPosition.y += positionYOffset;
                return randomPosition;
            }
        } while (true);
    }

    /// <summary>Респавнит игрока с созданием нового объекта</summary>
    public static void ReplacePlayer(NetworkConnectionToClient conn)
    {
        GameObject oldPlayer = conn.identity.gameObject;
        GameObject newPlayer = Instantiate(instance.playerPrefab);

        CustomPlayerController player = newPlayer.GetComponent<CustomPlayerController>();
        player.nickname = oldPlayer.GetComponent<CustomPlayerController>().nickname;
        player.transform.position = GetPlayerSpawnPosition();

        NetworkServer.ReplacePlayerForConnection(conn, newPlayer, true);
        Destroy(oldPlayer, 0.1f);   // Удалить предыдущий объект игрока, который теперь заменён. Задержка необходима для завершения замены (особенность Mirror)
    }

    /// <summary>Установливает слой для объекта и всех дочерних объектов</summary>
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
    }
}
