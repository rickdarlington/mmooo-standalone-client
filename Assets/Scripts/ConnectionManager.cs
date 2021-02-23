using System;
using System.Collections;
using DarkRift;
using DarkRift.Client;
using DarkRift.Client.Unity;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(UnityClient))]
public class ConnectionManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private LoginManager loginManager;
    
    public ushort PlayerId { get; set; }
    public static ConnectionManager Instance => _instance;
    private static ConnectionManager _instance;

    public UnityClient Client { get; private set; }
    public delegate void OnConnectedDelegate();
    public event OnConnectedDelegate OnConnected;
    
    private int retrySeconds = 0;
    private static int backoffSeconds = 5;
    
    void Awake()
    {
        if (_instance != null)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(this);
        
        Client = GetComponent<UnityClient>();
        Client.Disconnected += DisconnectCallback;
        Client.ConnectInBackground(Client.Address, Client.Port, true, ConnectCallback);
    }

    private void TryConnect()
    {
        if (retrySeconds < 60)
        {
            Debug.LogError($"Connection to server failed.  Retrying in {retrySeconds} seconds.");
            StartCoroutine(ConnectCoroutine());
        }
        else
        {
            StopCoroutine(ConnectCoroutine());
            Debug.LogError("Can't connect after multiple retries.");
        }
    }

    private IEnumerator ConnectCoroutine()
    {
        yield return new WaitForSeconds(retrySeconds);
        Client.ConnectInBackground(Client.Address, Client.Port, true, ConnectCallback);
    }

    private void ConnectCallback(Exception exception)
    {
        if (Client.ConnectionState == ConnectionState.Connected)
        {
            backoffSeconds = 0;
            StopCoroutine(ConnectCoroutine());
            loginManager.ShowLogin();
            Client.MessageReceived += onMessage;
        }
        else
        {
            if (loginManager != null)
            {
                loginManager.HideLogin();
            }

            Client.MessageReceived -= onMessage;
            retrySeconds += backoffSeconds;
            TryConnect();
        }
    }
    
    private void DisconnectCallback(object o, DisconnectedEventArgs args)
    {
        Debug.Log("Server disconnected.");
        loginManager.LoadLogin();
        TryConnect();
    }
    
    private void onMessage(object sender, MessageReceivedEventArgs args)
    {
        using (Message message = args.GetMessage())
        {
            switch ((NetworkingData.Tags) message.Tag)
            {
                case NetworkingData.Tags.GameStartData:
                    Debug.Log("Got game start data.");
                    //OnGameStart(message.Deserialize<NetworkingData.GameStartData>());
                    break;
                case NetworkingData.Tags.GameUpdate:
                    //worldUpdateBuffer.Enqueue(message.Deserialize<NetworkingData.GameUpdateData>());
                    break;
                case NetworkingData.Tags.PlayerSpawn:
                    Debug.Log("Got player spawn data.");
                    //SpawnPlayer(message.Deserialize<NetworkingData.PlayerSpawnData>());
                    break;
                case NetworkingData.Tags.PlayerDeSpawn:
                    Debug.Log("Got player despawn data.");
                    //DespawnPlayer(message.Deserialize<NetworkingData.PlayerDespawnData>().Id);
                    break;
                case NetworkingData.Tags.LoginRequestDenied:
                    OnLoginDeclined();
                    break;
                case NetworkingData.Tags.LoginRequestAccepted:
                    OnLoginAccepted(message.Deserialize<NetworkingData.LoginInfoData>());
                    break;
                default:
                    Debug.Log($"Unhandled tag: {message.Tag}");
                    break;
            }
        }
    }

    private void OnLoginDeclined()
    {
        //TODO put this message in the UI
        Debug.LogError("Login declined.  Check username.");
    }

    private void OnLoginAccepted(NetworkingData.LoginInfoData data)
    {
        Debug.Log($"Login success, clientId = {data.Id}");
        
        PlayerId = data.Id;
        SceneManager.LoadScene("Game", LoadSceneMode.Single);
    }
}
