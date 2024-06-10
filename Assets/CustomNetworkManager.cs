using UnityEngine;
using TMPro;
using Mirror;


public class CustomNetworkManager : NetworkManager
{
    public static string Nickname = string.Empty;   // Хранит никнейм игрока, введённый в главном меню
    public TMP_InputField inputNickname;


    public override void Awake()
    {
        inputNickname.text = Nickname;  // Вернуть никнейм в поле ввода при отключении от сервера
        base.Awake();
    }

    public void OnNicknameChanged(string value)
    {
        Nickname = value;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        NetworkServer.RegisterHandler<CreatePlayerMessage>(OnCreatePlayer); // Регистрация обработчика сообщения на сервере
    }

    public override void OnClientConnect()
    {
        base.OnClientConnect();

        CreatePlayerMessage playerMessage = new CreatePlayerMessage
        {
            nickname = string.IsNullOrEmpty(Nickname) ? "Бобба" : Nickname  // Если поле никнейма пустое, то используется никнейм по умолчанию
        };

        NetworkClient.Send(playerMessage);  // Клиент отправляет сообщение на сервер
    }

    void OnCreatePlayer(NetworkConnectionToClient conn, CreatePlayerMessage message)
    {
        // Создание объекта игрока с установленным никнеймом и позицией
        GameObject gameobject = Instantiate(playerPrefab);

        CustomPlayerController player = gameobject.GetComponent<CustomPlayerController>();
        player.playerName = message.nickname;
        gameobject.transform.position = GameManager.GetPlayerSpawnPosition();

        NetworkServer.AddPlayerForConnection(conn, gameobject); // Добавление объекта игрока для соединения
    }
}


// Структура сетевого сообщения
public struct CreatePlayerMessage : NetworkMessage
{
    public string nickname;
}
