using System;
using System.Collections;
using DarkRift;
using DarkRift.Client;
using DarkRift.Client.Unity;
using DefaultNamespace;
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
            ConsoleUtils.getInstance().printInGameConsole($"Connection to server failed.  Retrying in {retrySeconds} seconds.");
            Debug.LogError($"Connection to server failed.  Retrying in {retrySeconds} seconds.");
            StartCoroutine(ConnectCoroutine());
        }
        else
        {
            StopCoroutine(ConnectCoroutine());
            ConsoleUtils.getInstance().printInGameConsole("Can't connect after multiple retries.  Have you tried rebooting? ;)");
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
            ConsoleUtils.getInstance().printInGameConsole($"Connected to {Client.Address}");
            retrySeconds = 0;
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
        ConsoleUtils.getInstance().printInGameConsole("Server Disconnected");
        Debug.Log("Server disconnected.");
        TryConnect();
    }
    
    private void onMessage(object sender, MessageReceivedEventArgs args)
    {
        using (Message message = args.GetMessage())
        {
            switch ((NetworkingData.Tags) message.Tag)
            {
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
        ConsoleUtils.getInstance().printInGameConsole("Login declined. Check username.");
        Debug.LogError("Login declined.  Check username.");
    }

    private void OnLoginAccepted(NetworkingData.LoginInfoData data)
    {
        ConsoleUtils.getInstance().printInGameConsole("You are logged in. Welcome back!");
        Debug.Log($"Login success, clientId = {data.Id}");
        Client.MessageReceived -= onMessage;
        PlayerId = data.Id;
        SceneManager.LoadScene("Game", LoadSceneMode.Single);
    }
}
