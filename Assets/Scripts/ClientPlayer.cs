﻿using System.Collections.Generic;
using System.Linq;
using DarkRift;
using DefaultNamespace;
using MmoooPlugin.Shared;
using UnityEngine;
using Vector3 = UnityEngine.Vector3;

public class ClientPlayer : MonoBehaviour
{
    public string name;
    public bool isOwn;
    public ushort ID;

    //for batching inputs at 30hz instead of the 60hz fixedupdate rate.
    //for smooth movement that is in sync with rendering make sure to set unity edit > Project Settings > Time > Fixed Timestep = 0.01666667
    public Queue<NetworkingData.PlayerInputData> pendingInputs = new Queue<NetworkingData.PlayerInputData>();
    private ushort inputSeq = 0;
    
    //saved inputs for reconciliation (connected player only)
    //it would probably be better/more efficient if we only had one of pendingInputs OR reconciliationInputs, but this is easier to manage
    public Queue<NetworkingData.PlayerInputData> reconciliationInputs = new Queue<NetworkingData.PlayerInputData>();

    //for interpolation (all entities EXCEPT connected player
    public Queue<NetworkingData.PlayerStateData> positionBuffer = new Queue<NetworkingData.PlayerStateData>();

    public ushort spriteRowIndex = 0;
    public GameObject Prefab;
    private SpriteRenderer renderer;
    private ushort DefaultLookDirection = 1;

    public Sprite[] SpriteArray;

    private static float MAX_CAMERA = 12;
    private static float MIN_CAMERA = 4;
    
    public void Initialize(ushort id, string name, byte spriteRow, float x, float y, GameObject prefab)
    {
        ID = id;
        this.name = name;
        Prefab = prefab;
        transform.localPosition = new Vector3(x, y, 0);
        
        
        if (ConnectionManager.Instance.PlayerId == id)
        {
            Debug.Log($"Initializing our player {this.name} with client id ({id})");
            isOwn = true;
            Camera.main.transform.SetParent(Prefab.transform);
        }

        spriteRowIndex = spriteRow;
        
        renderer = Prefab.GetComponent<SpriteRenderer>();
        rotateSprite(DefaultLookDirection);
    }

    public void rotateSprite(ushort lookDirection)
    {
        int spriteIndex = (spriteRowIndex * 14);
        int animationFrames = 3;
        
        renderer.sprite = SpriteArray[spriteIndex + (animationFrames*lookDirection)];
    }

    public void Update()
    {
        
        if (isOwn)
        {
            //stuff starting here doesn't need to get synced to the server
            if (Input.GetKeyDown(KeyCode.Tab))
            {
               ConsoleUtils.getInstance().toggle();
            }
        
            float scrollAmt = Input.mouseScrollDelta.y;
            if (scrollAmt != 0)
            {
                float newSize = Camera.main.orthographicSize - scrollAmt*2;
                if (MIN_CAMERA <= newSize && newSize <= MAX_CAMERA)
                {
                    Camera.main.orthographicSize = newSize;
                }
            }

            //everything below this point needs to get synced to the server
            //LookDirection is 0: right, 1: down, 2: left, 3: up
            //animation frames is # frames for walk animation
            bool[] inputs = new bool[5];
            inputs[0] = Input.GetKey(KeyCode.W);
            inputs[1] = Input.GetKey(KeyCode.A);
            inputs[2] = Input.GetKey(KeyCode.S);
            inputs[3] = Input.GetKey(KeyCode.D);
            inputs[4] = Input.GetKey(KeyCode.Space);
            
            byte lookDirection = 2;
            if (inputs[0]) lookDirection = 3;
            if (inputs[2]) lookDirection = 1;
            if (inputs[1]) lookDirection = 2;
            if (inputs[3]) lookDirection = 0;
            
            NetworkingData.PlayerInputData inputData = new NetworkingData.PlayerInputData(inputs, lookDirection, inputSeq, Time.deltaTime);

            if(inputs.Contains(true)) {
                //TODO can we do this differently to simplify to one list/queue
                pendingInputs.Enqueue(inputData);
                reconciliationInputs.Enqueue(inputData);
                var pos = PlayerMovement.MovePlayer(inputData, new System.Numerics.Vector2(transform.localPosition.x, transform.localPosition.y), Time.deltaTime);
                transform.localPosition = new Vector3(pos.X, pos.Y);
                rotateSprite(lookDirection);
                inputSeq++;
                
                //TODO remove me Debug.Log($"position updated to: {pos.X}, {pos.Y}");
            }
        }
    }

    public void FixedUpdate()
    {
        var c = pendingInputs.Count;
        NetworkingData.PlayerInputData[] datas = new NetworkingData.PlayerInputData[c];

        for (int i = 0; i < c; i++)
        {
            datas[i] = pendingInputs.Dequeue();
        }

        using (Message message = Message.Create((ushort) NetworkingData.Tags.PlayerInputs, new NetworkingData.PlayerInputDatas(datas)))
        {
            //NOTE uncomment if you want to see the last input sequence number sent by the client: Debug.Log($"last sent input sequence number: {inputData.InputSeq}");
            ConnectionManager.Instance.Client.SendMessage(message, SendMode.Reliable);
        }
    }
}