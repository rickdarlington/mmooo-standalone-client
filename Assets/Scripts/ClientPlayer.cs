using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DarkRift;
using MmoooPlugin.Shared;
using UnityEngine;

public class ClientPlayer : MonoBehaviour
{
    private ushort id;
    private string playerName;
    private bool isOwn;
    private ushort inputSeq = 0;
    public Queue<NetworkingData.PlayerInputData> pendingInputs = new Queue<NetworkingData.PlayerInputData>();

    private NetworkingData.PlayerStateData PlayerPosition;

    public GameObject Prefab;
    
    public void Initialize(ushort id, string playerName, Vector2 position)
    {
        this.id = id;
        this.playerName = playerName;
        
        if (ConnectionManager.Instance.PlayerId == id)
        {
            Debug.Log($"Initializing our player {playerName} with client id ({id})");
            isOwn = true;
        }
    }

    void FixedUpdate()
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

            System.Numerics.Vector2 newPos = PlayerMovement.MovePlayer(inputData, new System.Numerics.Vector2(transform.position.x, transform.position.y), Time.deltaTime);

            if (newPos.X != transform.position.x || newPos.Y != transform.position.y)
            {
                transform.position = new Vector3(newPos.X, newPos.Y, 0);
                //Debug.Log($"position: {transform.position.x}, {transform.position.y}");
            }

            if(inputs.Contains(true)) {
                pendingInputs.Enqueue(inputData);
                
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