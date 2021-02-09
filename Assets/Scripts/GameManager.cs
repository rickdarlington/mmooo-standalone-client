﻿using System;
using System.Collections.Generic;
using System.Linq;
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
        player.Initialize(data.Id, data.Name, data.Position.X, data.Position.Y);
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
        processServerUpdates();

        processInputs();

        //TODO this is hard to follow because ClientPlayer.positionBuffer alternates between timestamp and position. but making a bunch of objects is super wasteful...
        
        interpolateEntities();

        ClientTick++;
    }

    private void processInputs()
    {
        //TODO refactor?  this is "processInputs"
        ClientPlayer thisPlayer;
        if(players.TryGetValue(ConnectionManager.Instance.PlayerId, out thisPlayer))
        {
            thisPlayer.processInputs();
        }
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
                            //Debug.Log($"server position {playerState.Position.X}, {playerState.Position.Y} local position: {player.transform.localPosition.x}, {player.transform.localPosition.y}");
                            
                            for (int x = 0; x < player.pendingInputs.Count; x++)
                            {
                                if (player.pendingInputs.Peek().InputSeq < playerState.LastProcessedInput)
                                {
                                    player.pendingInputs.Dequeue();
                                }
                                else
                                {
                                    NetworkingData.PlayerInputData inputData = player.pendingInputs.Dequeue();

                                    player.transformPosition = PlayerMovement.MovePlayer(inputData, playerState.Position, Time.deltaTime);
                                }
                            }

                            //TODO snap player back if position isn't too far diverged from server
                            //if (Vector2.Distance(newPos, new Vector2(player.transform.localPosition.x, player.transform.localPosition.y)) > 0.05f)
                            //{
                            //}
                        }
                        else
                        {
                            player.transformPosition.X = playerState.Position.X;
                            player.transformPosition.Y =  playerState.Position.Y; 
                            
                            //interpolation?
                            player.positionBuffer.Enqueue(DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond);
                            player.positionBuffer.Enqueue(playerState.Position);
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
                Queue<System.Object> posBuffer = kv.Value.positionBuffer;
                int max = posBuffer.Count;
                for (int i = 0; i < max; i = i + 2)
                {
                    if (max >= 4 && (long)posBuffer.Peek() < renderTimestamp)
                    {
                        posBuffer.Dequeue();
                        posBuffer.Dequeue();
                    }
                }

                if (posBuffer.Count > 4 && (long)posBuffer.Peek() <= renderTimestamp)
                {
                    var ts1 = (long) posBuffer.Dequeue();
                    Vector2 pos1 = (Vector2) posBuffer.Dequeue();
                    
                    if (renderTimestamp <= (long) posBuffer.Peek())
                    {
                        var ts2 = (long) posBuffer.Dequeue();
                        Vector2 pos2 = (Vector2) posBuffer.Dequeue();

                        if (!pos1.Equals(pos2))
                        {
                            long t = ts2 - ts1;
                            p.transformPosition = Vector2.Lerp(pos1, pos2, t);
                        }
                    }
                }
            }
            
            //actually set the position for this player
            p.transform.localPosition = new Vector3(p.transformPosition.X, p.transformPosition.Y);
        }
        
        //finally, move myself (why here? will we interpolate server snapbacks? probably?)
        ClientPlayer player;
        if (players.TryGetValue(ConnectionManager.Instance.PlayerId, out player))
        {
            player.transform.localPosition = new Vector3(player.transformPosition.X, player.transformPosition.Y, 0);
        }
    }
}