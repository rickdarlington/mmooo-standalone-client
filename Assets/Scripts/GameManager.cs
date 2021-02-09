using System.Collections.Generic;
using DarkRift;
using DarkRift.Client;
using MmoooPlugin.Shared;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    private Dictionary<ushort, ClientPlayer> players = new Dictionary<ushort, ClientPlayer>();
    
    public Queue<NetworkingData.GameUpdateData> worldUpdateBuffer = new Queue<NetworkingData.GameUpdateData>();

    [Header("Prefabs")] public GameObject PlayerPrefab;

    public uint ClientTick { get; private set; }
    public uint LastReceivedServerTick { get; private set; }
    
    void OnDestroy()
    {
        Instance = null;
    }
    
    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(this);
    }

    void Start()
    {
        ConnectionManager.Instance.Client.MessageReceived += onMessage;
            
        using (Message message = Message.Create((ushort)NetworkingData.Tags.PlayerReady, new NetworkingData.PlayerReadyData(true)))
        {
            ConnectionManager.Instance.Client.SendMessage(message, SendMode.Reliable);
            Debug.Log("Ready message sent.");
        }
    }

    void onMessage(object sender, MessageReceivedEventArgs args)
    {
        using (Message message = args.GetMessage())
        {
            switch ((NetworkingData.Tags) message.Tag)
            {
                case NetworkingData.Tags.GameStartData:
                    Debug.Log("Got game start data.");
                    OnGameStart(message.Deserialize<NetworkingData.GameStartData>());
                    break;
                case NetworkingData.Tags.GameUpdate:
                    NetworkingData.GameUpdateData gameUpdateData = message.Deserialize<NetworkingData.GameUpdateData>();
                    worldUpdateBuffer.Enqueue(gameUpdateData);
                    break;
                case NetworkingData.Tags.PlayerSpawn:
                    SpawnPlayer(message.Deserialize<NetworkingData.PlayerSpawnData>());
                    break;
                case NetworkingData.Tags.PlayerDeSpawn:
                    DespawnPlayer(message.Deserialize<NetworkingData.PlayerDespawnData>().Id);
                    break;
                default:
                    Debug.Log($"Unhandled tag: {message.Tag}");
                    break;
            }
        }
    }

    void OnGameStart(NetworkingData.GameStartData data)
    {
        LastReceivedServerTick = data.OnJoinServerTick;
        ClientTick = data.OnJoinServerTick;
        foreach (NetworkingData.PlayerSpawnData playerSpawnData in data.Players)
        {
            SpawnPlayer(playerSpawnData);
        }
    }

    void SpawnPlayer(NetworkingData.PlayerSpawnData data)
    {
        GameObject go = Instantiate(PlayerPrefab);
        ClientPlayer player = go.GetComponent<ClientPlayer>();
        player.Prefab = go;
        player.Initialize(data.Id, data.Name, new Vector2(data.Position.X, data.Position.Y));
        players.Add(data.Id, player);
        Debug.Log($"Spawn player {data.Name} at [{data.Position.X}, {data.Position.Y}]");
    }

    void DespawnPlayer(ushort id)
    {
        ClientPlayer player;
        if (players.TryGetValue(id, out player))
        {
            Destroy(player.Prefab);
            players.Remove(id);
        }
    }

    void FixedUpdate()
    {
        ClientTick++;
        
        if (worldUpdateBuffer.Count > 0)
        {
            int numUpdates = worldUpdateBuffer.Count;
            for (int i = 0; i < numUpdates; i++)
            {
                NetworkingData.GameUpdateData nextUpdate = worldUpdateBuffer.Dequeue();
                
                for(int j = 0; j < nextUpdate.UpdateData.Length; j++)
                {
                    NetworkingData.PlayerStateData playerState = nextUpdate.UpdateData[j];

                    ClientPlayer player;
                    if(players.TryGetValue(playerState.Id, out player))
                    {
                        if (playerState.Id == ConnectionManager.Instance.PlayerId)
                        {
                            System.Numerics.Vector2 newPos = playerState.Position;
                            
                            //Debug.Log($"server says my positon is {playerState.Position.X}, {playerState.Position.Y}");
                            
                            for (int x = 0; x < player.pendingInputs.Count; x++)
                            {
                                if (player.pendingInputs.Peek().InputSeq < playerState.LastProcessedInput)
                                {
                                    player.pendingInputs.Dequeue();
                                }
                                else
                                {
                                    NetworkingData.PlayerInputData inputData = player.pendingInputs.Dequeue();
                                    newPos = PlayerMovement.MovePlayer(inputData, newPos, Time.deltaTime);
                                }
                            }
                            
                            player.transform.position = new Vector3(newPos.X, newPos.Y,0);
                        }
                        else
                        { 
                            player.transform.localPosition = new Vector3(playerState.Position.X, 
                            playerState.Position.Y, 0);
                        }
                    }
                    else
                    {
                        Debug.Log($"ERROR got update for player (id:{playerState.Id}, {playerState.Position.X}, {playerState.Position.Y}) that we don't know about!");
                    }
                }
            }
        }
    }
}