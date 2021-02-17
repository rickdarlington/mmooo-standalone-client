using System.Collections.Generic;
using DarkRift;
using MmoooPlugin.Shared;
using UnityEngine;
using MessageReceivedEventArgs = DarkRift.Client.MessageReceivedEventArgs;
using Vector2 = System.Numerics.Vector2;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public Dictionary<ushort, ClientPlayer> players = new Dictionary<ushort, ClientPlayer>();
    
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
                    worldUpdateBuffer.Enqueue(message.Deserialize<NetworkingData.GameUpdateData>());
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
        spawnFixedNPC();
        
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
        player.Initialize(data.Id, data.Name, data.SpriteRowIndex, data.Position.X, data.Position.Y, go);
        players.Add(data.Id, player);
        Debug.Log($"Spawn player {data.Name} at [{data.Position.X}, {data.Position.Y}]");
    }

    //TODO remove me
    void spawnFixedNPC()
    {
        GameObject go = Instantiate(PlayerPrefab);
        
        int spriteIndex = (12 * 14);
        int animationFrames = 3;
        
        go.GetComponent<SpriteRenderer>().sprite = Instance.SpriteArray[spriteIndex + (animationFrames*1)];
    
        transform.localPosition = new Vector3(15, 15, 0);
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
        processServerUpdates();
    }

    void Update()
    { 
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
                        if (player.isOwn)
                        {
                            doPlayerReconciliation(player, playerState);
                        }
                        else
                        {
                            //server position for others is authoritative (smooth with interpolation later)
                            player.previousPosition = player.currentPosition;
                            player.currentPosition = playerState.Position;
                            player.rotateSprite(playerState.LookDirection);
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

    private void doPlayerReconciliation(ClientPlayer player, NetworkingData.PlayerStateData playerState)
    {
        //server position for us is used for reconciliation
        var reconciledPosition = playerState.Position;
                            
        var max = player.reconciliationInputs.Count;

        if (player.reconciliationInputs.Count > 100)
        {
            Debug.LogError($"We seem to be leaking reconciliation inputs (count: {player.reconciliationInputs.Count}");
        }
        
        for (int x = 0; x < max; x++)
        {
            var nextInput = player.reconciliationInputs.Dequeue();

            if (nextInput.InputSeq >= playerState.LastProcessedInput)
            {
                reconciledPosition = PlayerMovement.MovePlayer(nextInput, reconciledPosition, nextInput.DeltaTime);
            }

            var renderPosition = player.transform.localPosition;
            if (Vector2.Distance(new Vector2(renderPosition.x, renderPosition.y), reconciledPosition) > 0.05f) 
            {
                //TODO may want to really, really re-verify this reconciliation logic isn't causing issues/popping/choppy rendering
                //TODO remove me Debug.Log($"reconciling position from rendered position: {renderPosition.x}, {renderPosition.y} to server position: {reconciledPosition.X}, {reconciledPosition.Y}");
                player.transform.localPosition = new Vector3(reconciledPosition.X, reconciledPosition.Y, 0);
            }
        }
    }
    
    private void interpolateEntities()
    {
        foreach (KeyValuePair<ushort, ClientPlayer> kv in players)
        {
            ClientPlayer player = kv.Value;
            if (!player.isOwn)
            {
                player.transform.position = new Vector3(player.currentPosition.X, player.currentPosition.Y, 0);
            }
        }
    }

    public void OnDisable()
    {
        players.Clear();
        worldUpdateBuffer.Clear();
    }
}