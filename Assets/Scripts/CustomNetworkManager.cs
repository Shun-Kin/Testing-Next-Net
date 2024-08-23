using UnityEngine;
using TMPro;
using Mirror;

public class CustomNetworkManager : NetworkManager
{
    private const string DefaultNickname = "Боба";

    private static string nickname = string.Empty;  // Хранит никнейм игрока, введённый в главном меню

    [SerializeField] private TMP_InputField nicknameInputField;


    public override void Awake()
    {
        nicknameInputField.text = nickname; // Вернуть никнейм в поле ввода при отключении от сервера
        base.Awake();
    }

    public void OnNicknameChanged(string value)
    {
        nickname = value;
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
            nickname = string.IsNullOrEmpty(nickname) ? DefaultNickname : nickname  // Если поле никнейма пустое, то используется никнейм по умолчанию
        };

        NetworkClient.Send(playerMessage);  // Клиент отправляет сообщение на сервер
    }

    private void OnCreatePlayer(NetworkConnectionToClient conn, CreatePlayerMessage message)
    {
        // Создание объекта игрока с установленным никнеймом и позицией
        GameObject gameobject = Instantiate(playerPrefab);
        CustomPlayerController player = gameobject.GetComponent<CustomPlayerController>();
        player.nickname = message.nickname;
        gameobject.transform.position = GameManager.GetPlayerSpawnPosition();

        NetworkServer.AddPlayerForConnection(conn, gameobject); // Добавление объекта игрока для соединения
    }
}


/// <summary>Структура сетевого сообщения для создания игрока</summary>
public struct CreatePlayerMessage : NetworkMessage
{
    public string nickname;
}
