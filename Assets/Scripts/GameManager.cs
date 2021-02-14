using System;
using System.Collections.Generic;
using DarkRift;
using DarkRift.Client;
using MmoooPlugin.Shared;
using UnityEngine;
using Object = System.Object;
using Vector2 = System.Numerics.Vector2;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    private Dictionary<ushort, ClientPlayer> players = new Dictionary<ushort, ClientPlayer>();
    
    public Queue<NetworkingData.GameUpdateData> worldUpdateBuffer = new Queue<NetworkingData.GameUpdateData>();

    [Header("Prefabs")] public GameObject PlayerPrefab;

    public Sprite[] SpriteArray;

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
        SpriteArray = Resources.LoadAll<Sprite>("player_sprites");
        
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
        player.Initialize(data.Id, data.Name, data.SpriteRowIndex, data.Position.X, data.Position.Y);
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

    void Update()
    {
        processServerUpdates();
        
        interpolateEntities();

        ClientTick++;
    }

    private void processServerUpdates()
    {
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
                            //do reconciliation for this player
                            for (int x = 0; x < player.pendingInputs.Count; x++)
                            {
                                if (player.pendingInputs.Peek().InputSeq < playerState.LastProcessedInput)
                                {
                                    player.pendingInputs.Dequeue();
                                }
                                else
                                {
                                    NetworkingData.PlayerInputData inputData = player.pendingInputs.Dequeue();

                                    if (Vector2.Distance(player.transformPosition, playerState.Position) > 0.05f)
                                    {
                                        player.transformPosition = PlayerMovement.MovePlayer(inputData, playerState.Position, Time.deltaTime);
                                    }
                                }
                            }
                        }
                        else
                        {
                            player.rotateSprite(playerState.LookDirection);
                            player.transformPosition.X = playerState.Position.X;
                            player.transformPosition.Y = playerState.Position.Y;
                            
                            player.positionBuffer.Enqueue(new Object[] {DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond, playerState.Position});
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

    private void interpolateEntities()
    {
        //TODO refactor: this is really hard to follow because our buffer is holding alternating datatypes
        long renderTimestamp = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
        
        foreach (KeyValuePair<ushort, ClientPlayer> kv in players)
        {
            ClientPlayer p = kv.Value;
            //interpolate everyone but myself
            if(p.id != ConnectionManager.Instance.PlayerId)
            {
                Queue<Array> posBuffer = kv.Value.positionBuffer;

                int max = posBuffer.Count;

                for (int i = 0; i < max-2; i++)
                {
                    if ((long) posBuffer.Peek().GetValue(0) < renderTimestamp)
                    {
                        posBuffer.Dequeue();
                    }
                } 

                Array[] arr = posBuffer.ToArray();
                if (arr.Length >= 2 && (long)arr[0].GetValue(0) <= renderTimestamp && renderTimestamp <= (long)arr[1].GetValue(0))
                {
                    Array p1 = posBuffer.Dequeue();
                    Array p2 = posBuffer.Dequeue();

                    var ts1 = (long) p1.GetValue(0);
                    Vector2 pos1 = (Vector2) p1.GetValue(1);

                    var ts2 = (long) p2.GetValue(0);
                    Vector2 pos2 = (Vector2) p2.GetValue(1);

                    float x = pos1.X + (pos2.X - pos1.X) * (renderTimestamp - ts1) / (ts2 - ts1);
                    float y = pos1.Y + (pos2.Y - pos1.Y) * (renderTimestamp - ts1) / (ts2 - ts1);
                    
                    //TODO this interpolation code still doesn't work for shit...
                    //p.transformPosition = new Vector2(x, y);
                    //Debug.Log($"interp position: {p.transformPosition.X}, {p.transformPosition.Y}");
                }
            }
            
            //actually set the position for this player
            p.transform.localPosition = new Vector3(p.transformPosition.X, p.transformPosition.Y, 0);
        }
        
        //finally, move myself (why here? will we interpolate server snapbacks? probably?)
        ClientPlayer player;
        if (players.TryGetValue(ConnectionManager.Instance.PlayerId, out player))
        {
            player.transform.localPosition = new Vector3(player.transformPosition.X, player.transformPosition.Y, 0);
        }
    }

    public void OnDisable()
    {
        players.Clear();
        worldUpdateBuffer.Clear();
    }
}