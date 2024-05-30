using UnityEngine;
using TMPro;
using Mirror;


public class CustomNetworkManager : NetworkManager
{
    public static string Nickname = string.Empty;   // ������ ������� ������, �������� � ������� ����
    public TMP_InputField inputNickname;


    public override void Awake()
    {
        inputNickname.text = Nickname;  // ������� ������� � ���� ����� ��� ���������� �� �������
        base.Awake();
    }

    public void OnNicknameChanged(string value)
    {
        Nickname = value;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        NetworkServer.RegisterHandler<CreatePlayerMessage>(OnCreatePlayer); // ����������� ����������� ��������� �� �������
    }

    public override void OnClientConnect()
    {
        base.OnClientConnect();

        CreatePlayerMessage playerMessage = new CreatePlayerMessage
        {
            nickname = string.IsNullOrEmpty(Nickname) ? "�����" : Nickname  // ���� ���� �������� ������, �� ������������ ������� �� ���������
        };

        NetworkClient.Send(playerMessage);  // ������ ���������� ��������� �� ������
    }

    void OnCreatePlayer(NetworkConnectionToClient conn, CreatePlayerMessage message)
    {
        // �������� ������� ������ � ������������� ��������� � ��������
        GameObject gameobject = Instantiate(playerPrefab);

        CustomPlayerController player = gameobject.GetComponent<CustomPlayerController>();
        player.playerName = message.nickname;
        gameobject.transform.position = GameManager.GetPlayerSpawnPosition();

        NetworkServer.AddPlayerForConnection(conn, gameobject); // ���������� ������� ������ ��� ����������
    }
}


// ��������� �������� ���������
public struct CreatePlayerMessage : NetworkMessage
{
    public string nickname;
}
