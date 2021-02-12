using System.Collections.Generic;
using System.Linq;
using DarkRift;
using MmoooPlugin.Shared;
using UnityEngine;

public class ClientPlayer : MonoBehaviour
{
    private string playerName;
    private bool isOwn;
    private ushort inputSeq = 0;
    public ushort id;
    public Queue<NetworkingData.PlayerInputData> pendingInputs = new Queue<NetworkingData.PlayerInputData>();
    public Queue<System.Array> positionBuffer = new Queue<System.Array>();
    public System.Numerics.Vector2 transformPosition = new System.Numerics.Vector2(0,0);

    private NetworkingData.PlayerStateData PlayerPosition;

    public GameObject Prefab;


    private SpriteRenderer renderer;
    private ushort DefaultLookDirection = 1;
    public ushort spriteRowIndex = 0;
    
    public void Initialize(ushort id, string playerName, byte spriteRow, float x, float y)
    {
        this.id = id;
        this.playerName = playerName;
        transform.localPosition = new Vector3(x, y, 0);
        
        if (ConnectionManager.Instance.PlayerId == id)
        {
            Debug.Log($"Initializing our player {playerName} with client id ({id})");
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
        
        renderer.sprite = GameManager.Instance.SpriteArray[spriteIndex + (animationFrames*lookDirection)];
    }

    public void FixedUpdate()
    {
        //LookDirection is 0: right, 1: down, 2: left, 3: up
        //animation frames is # frames for walk animation
        if (isOwn)
        {
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
            
            NetworkingData.PlayerInputData inputData = new NetworkingData.PlayerInputData(inputs, lookDirection, inputSeq);

            if(inputs.Contains(true)) {
                pendingInputs.Enqueue(inputData);

                transformPosition = PlayerMovement.MovePlayer(inputData, transformPosition, Time.deltaTime);
                rotateSprite(lookDirection);

                using (Message message = Message.Create((ushort) NetworkingData.Tags.PlayerInput, inputData))
                {
                    //NOTE uncomment if you want to see the last input sequence number sent by the client: Debug.Log($"last sent input sequence number: {inputData.InputSeq}");
                    ConnectionManager.Instance.Client.SendMessage(message, SendMode.Reliable);
                }
                
                inputSeq++;
            }
        }
    }
}