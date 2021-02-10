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
    public Queue<System.Object> positionBuffer = new Queue<System.Object>();
    public System.Numerics.Vector2 transformPosition = new System.Numerics.Vector2(0,0);

    private NetworkingData.PlayerStateData PlayerPosition;

    public GameObject Prefab;
    
    public void Initialize(ushort id, string playerName, float x, float y)
    {
        this.id = id;
        this.playerName = playerName;
        transform.localPosition = new Vector3(x, y, 0);
        
        if (ConnectionManager.Instance.PlayerId == id)
        {
            Debug.Log($"Initializing our player {playerName} with client id ({id})");
            isOwn = true;
        }

        if (playerName == "a")
        {
            Prefab.GetComponent<SpriteRenderer>().sprite = GameManager.Instance.SpriteArray[15];
        }

        Camera.main.transform.SetParent(transform);
    }

    public void FixedUpdate()
    {
        if (isOwn)
        {
            bool[] inputs = new bool[5];
            inputs[0] = Input.GetKey(KeyCode.W);
            inputs[1] = Input.GetKey(KeyCode.A);
            inputs[2] = Input.GetKey(KeyCode.S);
            inputs[3] = Input.GetKey(KeyCode.D);
            inputs[4] = Input.GetKey(KeyCode.Space);
            
            NetworkingData.PlayerInputData inputData = new NetworkingData.PlayerInputData(inputs, 0, inputSeq);

            if(inputs.Contains(true)) {
                pendingInputs.Enqueue(inputData);

                transformPosition = PlayerMovement.MovePlayer(inputData, transformPosition, Time.deltaTime);
                
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