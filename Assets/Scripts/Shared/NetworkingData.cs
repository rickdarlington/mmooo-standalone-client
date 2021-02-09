using System;
using System.Linq;
using DarkRift;
using Vector2 = System.Numerics.Vector2;

public class NetworkingData
{
    public enum Tags
    {
        LoginRequest = 0,
        LoginRequestAccepted = 1,
        LoginRequestDenied = 2,
        PlayerReady = 3,
        GameStartData = 100,
        GameUpdate = 200,
        PlayerInput = 203,
        PlayerSpawn = 300,
        PlayerDeSpawn = 301
    }

    
    public struct LoginRequestData : IDarkRiftSerializable
    {
        public string Name;

        public LoginRequestData(string name)
        {
            Name = name;
        }

        public void Deserialize(DeserializeEvent e)
        {
            Name = e.Reader.ReadString();
        }

        public void Serialize(SerializeEvent e)
        {
            e.Writer.Write(Name);
        }
    }
    
    public struct LoginInfoData : IDarkRiftSerializable
    {
        public ushort Id;

        public LoginInfoData(ushort id)
        {
            Id = id;
        }

        public void Deserialize(DeserializeEvent e)
        {
            Id = e.Reader.ReadUInt16();
        }

        public void Serialize(SerializeEvent e)
        {
            e.Writer.Write(Id);
        }
    }

    public struct PlayerReadyData : IDarkRiftSerializable
    {
        public bool Ready;

        public PlayerReadyData(bool ready)
        {
            Ready = ready;
        }
        
        public void Deserialize(DeserializeEvent e)
        {
            Ready = e.Reader.ReadBoolean();
        }
 
        public void Serialize(SerializeEvent e)
        {
 
            e.Writer.Write(Ready);
        }
    }
    
    public struct PlayerInputData : IDarkRiftSerializable
    {
        public bool[] Keyinputs;
        public byte LookDirection;
        public uint InputSeq;
 
        public PlayerInputData(bool[] keyInputs, byte lookDirection, uint inputSeq)
        {
            Keyinputs = keyInputs;
            LookDirection = lookDirection;
            InputSeq = inputSeq;
        }
 
        public void Deserialize(DeserializeEvent e)
        {
            Keyinputs = e.Reader.ReadBooleans();
            LookDirection = e.Reader.ReadByte();
            InputSeq = e.Reader.ReadUInt32();
        }
 
        public void Serialize(SerializeEvent e)
        {
 
            e.Writer.Write(Keyinputs);
            e.Writer.Write(LookDirection);
            e.Writer.Write(InputSeq);
        }
    }
    
    public struct PlayerStateData : IDarkRiftSerializable
    {
        public ushort Id;
        public Vector2 Position;
        public byte LookDirection;
        public uint LastProcessedInput;

        public PlayerStateData(ushort id, Vector2 position, byte lookDirection, uint lastProcessedInput)
        {
            Id = id;
            Position = position;
            LookDirection = lookDirection;
            LastProcessedInput = lastProcessedInput;
        }

        public void Deserialize(DeserializeEvent e)
        {
            Id = e.Reader.ReadUInt16();
            Position = new Vector2(e.Reader.ReadSingle(), e.Reader.ReadSingle());
            LookDirection = e.Reader.ReadByte();
            LastProcessedInput = e.Reader.ReadUInt32();
        }

        public void Serialize(SerializeEvent e)
        {
            e.Writer.Write(Id);
            e.Writer.Write(Position.X);
            e.Writer.Write(Position.Y);
            e.Writer.Write(LookDirection);
            e.Writer.Write(LastProcessedInput);
        }
    }
    
    public struct PlayerSpawnData : IDarkRiftSerializable
    {
        public ushort Id;
        public string Name;
        public Vector2 Position;

        public PlayerSpawnData(ushort id, string name, Vector2 position)
        {
            Id = id;
            Name = name;
            Position = position;
        }

        public void Deserialize(DeserializeEvent e)
        {
            Id = e.Reader.ReadUInt16();
            Name = e.Reader.ReadString();
            Position = new Vector2(e.Reader.ReadSingle(), e.Reader.ReadSingle());
        }

        public void Serialize(SerializeEvent e)
        {
            e.Writer.Write(Id);
            e.Writer.Write(Name);
            e.Writer.Write(Position.X);
            e.Writer.Write(Position.Y);
        }

        public String toString()
        {
            return $"";
        }
    }

    public struct PlayerDespawnData : IDarkRiftSerializable
    {
        public ushort Id;

        public PlayerDespawnData(ushort id)
        {
            Id = id;
        }

        public void Deserialize(DeserializeEvent e)
        {
            Id = e.Reader.ReadUInt16();
        }

        public void Serialize(SerializeEvent e)
        {
            e.Writer.Write(Id);
        }
    }
    
    public struct GameUpdateData : IDarkRiftSerializable
    {
        public PlayerStateData[] UpdateData;

        public GameUpdateData(PlayerStateData[] updateData)
        {
            UpdateData = updateData; 
        }

        public void Deserialize(DeserializeEvent e)
        {
            UpdateData = e.Reader.ReadSerializables<PlayerStateData>();
        }

        public void Serialize(SerializeEvent e)
        {
            e.Writer.Write(UpdateData);
        }

        public String toString()
        {
            String m = ""; //$"{MSGID} ";
            foreach (PlayerStateData dat in UpdateData)
            {
                m += $"\n{dat.Id} {dat.Position.X}, {dat.Position.Y} {dat.LookDirection}, {dat.LastProcessedInput}";
            }

            return m;
        }
    }
    
    public struct GameStartData : IDarkRiftSerializable
    {
        public uint OnJoinServerTick;
        public PlayerSpawnData[] Players;

        public GameStartData(PlayerSpawnData[] players, uint serverTick)
        {
            Players = players;
            OnJoinServerTick = serverTick;
        }

        public void Deserialize(DeserializeEvent e)
        {
            OnJoinServerTick = e.Reader.ReadUInt32();
            Players = e.Reader.ReadSerializables<PlayerSpawnData>();
        }

        public void Serialize(SerializeEvent e)
        {
            e.Writer.Write(OnJoinServerTick);
            e.Writer.Write(Players);
        }
    }
}