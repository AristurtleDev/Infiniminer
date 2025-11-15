/* ----------------------------------------------------------------------------
MIT License

Copyright (c) 2009 Zach Barth
Copyright (c) 2023 Christopher Whitley

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
---------------------------------------------------------------------------- */

using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Media;
using Lidgren.Network;
using Lidgren.Network.Xna;
using Infiniminer.IO;

namespace Infiniminer
{
    public class InfiniminerGame : StateMasher.StateMachine
    {
        double timeSinceLastUpdate = 0;
        string playerHandle = "Player";
        float volumeLevel = 1.0f;
        NetBuffer msgBuffer = null;
        Song songTitle = null;

        public bool RenderPretty = true;
        public bool DrawFrameRate = false;
        public bool InvertMouseYAxis = false;
        public bool NoSound = false;

        public InfiniminerGame(string[] args)
        {
            this.Exiting += Game_OnExiting;
        }

        public void JoinGame(IPEndPoint serverEndPoint)
        {
            // Clear out the map load progress indicator.
            propertyBag.mapLoadProgress = new bool[64, 64];
            for (int i = 0; i < 64; i++)
                for (int j = 0; j < 64; j++)
                    propertyBag.mapLoadProgress[i, j] = false;

            // Create our connect message.
            NetBuffer connectBuffer = propertyBag.netClient.CreateBuffer();
            connectBuffer.Write(propertyBag.playerHandle);
            connectBuffer.Write(Defines.INFINIMINER_VERSION);

            // Connect to the server.
            propertyBag.netClient.Connect(serverEndPoint, connectBuffer.ToArray());
        }

        public List<ServerInformation> EnumerateServers(float discoveryTime)
        {
            List<ServerInformation> serverList = new List<ServerInformation>();

            // Discover local servers.
            propertyBag.netClient.DiscoverLocalServers(5565);
            NetBuffer msgBuffer = propertyBag.netClient.CreateBuffer();
            NetMessageType msgType;
            float timeTaken = 0;
            while (timeTaken < discoveryTime)
            {
                while (propertyBag.netClient.ReadMessage(msgBuffer, out msgType))
                {
                    if (msgType == NetMessageType.ServerDiscovered)
                    {
                        bool serverFound = false;
                        ServerInformation serverInfo = new ServerInformation(msgBuffer);
                        foreach (ServerInformation si in serverList)
                            if (si.Equals(serverInfo))
                                serverFound = true;
                        if (!serverFound)
                            serverList.Add(serverInfo);
                    }
                }

                timeTaken += 0.1f;
                Thread.Sleep(100);
            }

            // Discover remote servers.
            try
            {
                // 'serversV1.txt' format:
                // name;IPaddress;INFINIMINER;numPlayers;maxPlayers;extra
                string publicList = HttpRequest.Get("https://AristurtleDev.github.io/Infiniminer/serversV1.txt", null);
                foreach (string s in publicList.Split("\r\n".ToCharArray()))
                {
                    string[] args = s.Split(";".ToCharArray());
                    if (args.Length == 6)
                    {
                        IPAddress serverIp;
                        if (IPAddress.TryParse(args[1], out serverIp) && args[2] == "INFINIMINER")
                        {
                            ServerInformation serverInfo = new ServerInformation(serverIp, args[0], args[5], args[3], args[4]);
                            serverList.Add(serverInfo);
                        }
                    }
                }
            }
            catch (Exception)
            {
            }

            return serverList;
        }

        public void UpdateNetwork(GameTime gameTime)
        {
            // Update the server with our status.
            timeSinceLastUpdate += gameTime.ElapsedGameTime.TotalSeconds;
            if (timeSinceLastUpdate > 0.05)
            {
                timeSinceLastUpdate = 0;
                if (CurrentStateType == "Infiniminer.States.MainGameState")
                    propertyBag.SendPlayerUpdate();
            }

            // Recieve messages from the server.
            NetMessageType msgType;
            while (propertyBag.netClient.ReadMessage(msgBuffer, out msgType))
            {
                switch (msgType)
                {
                    case NetMessageType.StatusChanged:
                        {
                            if (propertyBag.netClient.Status == NetConnectionStatus.Disconnected)
                                ChangeState("Infiniminer.States.ServerBrowserState");
                        }
                        break;

                    case NetMessageType.ConnectionRejected:
                        {
                            string[] reason = msgBuffer.ReadString().Split(";".ToCharArray());
                            if (reason.Length < 2 || reason[0] == "VER")
                            {
                                //  [MG_PORT_NOTE] System.Windows.Forms is Windows Only
                                // MessageBox.Show("Error: client/server version incompability!\r\nServer: " + msgBuffer.ReadString() + "\r\nClient: " + Defines.INFINIMINER_VERSION);
                            }
                            else
                            {
                                //  [MG_PORT_NOTE] System.Windows.Forms is Windows Only
                                // MessageBox.Show("Error: you are banned from this server!");
                            }
                            ChangeState("Infiniminer.States.ServerBrowserState");
                        }
                        break;

                    case NetMessageType.Data:
                        {
                            InfiniminerMessage dataType = (InfiniminerMessage)msgBuffer.ReadByte();
                            switch (dataType)
                            {
                                case InfiniminerMessage.BlockBulkTransfer:
                                    {
                                        byte x = msgBuffer.ReadByte();
                                        byte y = msgBuffer.ReadByte();
                                        propertyBag.mapLoadProgress[x, y] = true;
                                        for (byte dy = 0; dy < 16; dy++)
                                            for (byte z = 0; z < 64; z++)
                                            {
                                                BlockType blockType = (BlockType)msgBuffer.ReadByte();
                                                if (blockType != BlockType.None)
                                                    propertyBag.blockEngine.downloadList[x, y + dy, z] = blockType;
                                            }
                                        bool downloadComplete = true;
                                        for (x = 0; x < 64; x++)
                                            for (y = 0; y < 64; y += 16)
                                                if (propertyBag.mapLoadProgress[x, y] == false)
                                                {
                                                    downloadComplete = false;
                                                    break;
                                                }
                                        if (downloadComplete)
                                        {
                                            ChangeState("Infiniminer.States.TeamSelectionState");
                                            if (!NoSound)
                                                MediaPlayer.Stop();
                                            propertyBag.blockEngine.DownloadComplete();
                                        }
                                    }
                                    break;

                                case InfiniminerMessage.SetBeacon:
                                    {
                                        Vector3 position = msgBuffer.ReadVector3();
                                        string text = msgBuffer.ReadString();
                                        PlayerTeam team = (PlayerTeam)msgBuffer.ReadByte();

                                        if (text == "")
                                        {
                                            if (propertyBag.beaconList.ContainsKey(position))
                                                propertyBag.beaconList.Remove(position);
                                        }
                                        else
                                        {
                                            Beacon newBeacon = new Beacon(text, team);
                                            propertyBag.beaconList.Add(position, newBeacon);
                                        }
                                    }
                                    break;

                                case InfiniminerMessage.TriggerConstructionGunAnimation:
                                    {
                                        propertyBag.constructionGunAnimation = msgBuffer.ReadFloat();
                                        if (propertyBag.constructionGunAnimation <= -0.1)
                                            propertyBag.PlaySound(InfiniminerSound.RadarSwitch);
                                    }
                                    break;

                                case InfiniminerMessage.ResourceUpdate:
                                    {
                                        // ore, cash, weight, max ore, max weight, team ore, red cash, blue cash, all uint
                                        propertyBag.playerOre = msgBuffer.ReadUInt32();
                                        propertyBag.playerCash = msgBuffer.ReadUInt32();
                                        propertyBag.playerWeight = msgBuffer.ReadUInt32();
                                        propertyBag.playerOreMax = msgBuffer.ReadUInt32();
                                        propertyBag.playerWeightMax = msgBuffer.ReadUInt32();
                                        propertyBag.teamOre = msgBuffer.ReadUInt32();
                                        propertyBag.teamRedCash = msgBuffer.ReadUInt32();
                                        propertyBag.teamBlueCash = msgBuffer.ReadUInt32();
                                    }
                                    break;

                                case InfiniminerMessage.BlockSet:
                                    {
                                        // x, y, z, type, all bytes
                                        byte x = msgBuffer.ReadByte();
                                        byte y = msgBuffer.ReadByte();
                                        byte z = msgBuffer.ReadByte();
                                        BlockType blockType = (BlockType)msgBuffer.ReadByte();
                                        if (blockType == BlockType.None)
                                        {
                                            if (propertyBag.blockEngine.BlockAtPoint(new Vector3(x, y, z)) != BlockType.None)
                                                propertyBag.blockEngine.RemoveBlock(x, y, z);
                                        }
                                        else
                                        {
                                            if (propertyBag.blockEngine.BlockAtPoint(new Vector3(x, y, z)) != BlockType.None)
                                                propertyBag.blockEngine.RemoveBlock(x, y, z);
                                            propertyBag.blockEngine.AddBlock(x, y, z, blockType);
                                            CheckForStandingInLava();
                                        }
                                    }
                                    break;

                                case InfiniminerMessage.TriggerExplosion:
                                    {
                                        Vector3 blockPos = msgBuffer.ReadVector3();

                                        // Play the explosion sound.
                                        propertyBag.PlaySound(InfiniminerSound.Explosion, blockPos);

                                        // Create some particles.
                                        propertyBag.particleEngine.CreateExplosionDebris(blockPos);

                                        // Figure out what the effect is.
                                        float distFromExplosive = (blockPos + 0.5f * Vector3.One - propertyBag.playerPosition).Length();
                                        if (distFromExplosive < 3)
                                            propertyBag.KillPlayer("WAS KILLED IN AN EXPLOSION!");
                                        else if (distFromExplosive < 8)
                                        {
                                            // If we're not in explosion mode, turn it on with the minimum ammount of shakiness.
                                            if (propertyBag.screenEffect != ScreenEffect.Explosion)
                                            {
                                                propertyBag.screenEffect = ScreenEffect.Explosion;
                                                propertyBag.screenEffectCounter = 2;
                                            }
                                            // If this bomb would result in a bigger shake, use its value.
                                            double effectValue = Math.Min(propertyBag.screenEffectCounter, (distFromExplosive - 2) / 5);
                                            propertyBag.screenEffectCounter = effectValue;

                                            //  TODO: Move this to server message being sent, but need to calcualte
                                            //  the same distance/effect value on server to do that.
                                            propertyBag.inputEngine.VibrateGamepad((float)effectValue, TimeSpan.FromSeconds(effectValue));
                                        }
                                    }
                                    break;

                                case InfiniminerMessage.PlayerSetTeam:
                                    {
                                        uint playerId = msgBuffer.ReadUInt32();
                                        if (propertyBag.playerList.ContainsKey(playerId))
                                        {
                                            ClientPlayer player = propertyBag.playerList[playerId];
                                            player.Team = (PlayerTeam)msgBuffer.ReadByte();
                                        }
                                    }
                                    break;

                                case InfiniminerMessage.PlayerJoined:
                                    {
                                        uint playerId = msgBuffer.ReadUInt32();
                                        string playerName = msgBuffer.ReadString();
                                        bool thisIsMe = msgBuffer.ReadBoolean();
                                        bool playerAlive = msgBuffer.ReadBoolean();
                                        propertyBag.playerList[playerId] = new ClientPlayer(this);
                                        propertyBag.playerList[playerId].Handle = playerName;
                                        propertyBag.playerList[playerId].ID = playerId;
                                        propertyBag.playerList[playerId].Alive = playerAlive;
                                        if (thisIsMe)
                                            propertyBag.playerMyId = playerId;
                                    }
                                    break;

                                case InfiniminerMessage.PlayerLeft:
                                    {
                                        uint playerId = msgBuffer.ReadUInt32();
                                        if (propertyBag.playerList.ContainsKey(playerId))
                                            propertyBag.playerList.Remove(playerId);
                                    }
                                    break;

                                case InfiniminerMessage.PlayerDead:
                                    {
                                        uint playerId = msgBuffer.ReadUInt32();
                                        if (propertyBag.playerList.ContainsKey(playerId))
                                        {
                                            ClientPlayer player = propertyBag.playerList[playerId];
                                            player.Alive = false;
                                            propertyBag.particleEngine.CreateBloodSplatter(player.Position, player.Team == PlayerTeam.Red ? Color.Red : Color.Blue);
                                            if (playerId != propertyBag.playerMyId)
                                                propertyBag.PlaySound(InfiniminerSound.Death, player.Position);
                                        }
                                    }
                                    break;

                                case InfiniminerMessage.PlayerAlive:
                                    {
                                        uint playerId = msgBuffer.ReadUInt32();
                                        if (propertyBag.playerList.ContainsKey(playerId))
                                        {
                                            ClientPlayer player = propertyBag.playerList[playerId];
                                            player.Alive = true;
                                        }
                                    }
                                    break;

                                case InfiniminerMessage.PlayerUpdate:
                                    {
                                        uint playerId = msgBuffer.ReadUInt32();
                                        if (propertyBag.playerList.ContainsKey(playerId))
                                        {
                                            ClientPlayer player = propertyBag.playerList[playerId];
                                            player.UpdatePosition(msgBuffer.ReadVector3(), gameTime.TotalGameTime.TotalSeconds);
                                            player.Heading = msgBuffer.ReadVector3();
                                            player.Tool = (PlayerTools)msgBuffer.ReadByte();
                                            player.UsingTool = msgBuffer.ReadBoolean();
                                            player.Score = (uint)(msgBuffer.ReadUInt16() * 100);
                                        }
                                    }
                                    break;

                                case InfiniminerMessage.GameOver:
                                    {
                                        propertyBag.teamWinners = (PlayerTeam)msgBuffer.ReadByte();
                                    }
                                    break;

                                case InfiniminerMessage.ChatMessage:
                                    {
                                        ChatMessageType chatType = (ChatMessageType)msgBuffer.ReadByte();
                                        string chatString = msgBuffer.ReadString();
                                        ChatMessage chatMsg = new ChatMessage(chatString, chatType, 10);
                                        propertyBag.chatBuffer.Insert(0, chatMsg);
                                        propertyBag.PlaySound(InfiniminerSound.ClickLow);
                                    }
                                    break;

                                case InfiniminerMessage.PlayerPing:
                                    {
                                        uint playerId = (uint)msgBuffer.ReadInt32();
                                        if (propertyBag.playerList.ContainsKey(playerId))
                                        {
                                            if (propertyBag.playerList[playerId].Team == propertyBag.playerTeam)
                                            {
                                                propertyBag.playerList[playerId].Ping = 1;
                                                propertyBag.PlaySound(InfiniminerSound.Ping);
                                            }
                                        }
                                    }
                                    break;

                                case InfiniminerMessage.PlaySound:
                                    {
                                        InfiniminerSound sound = (InfiniminerSound)msgBuffer.ReadByte();
                                        bool hasPosition = msgBuffer.ReadBoolean();
                                        if (hasPosition)
                                        {
                                            Vector3 soundPosition = msgBuffer.ReadVector3();
                                            propertyBag.PlaySound(sound, soundPosition);
                                        }
                                        else
                                            propertyBag.PlaySound(sound);
                                    }
                                    break;
                                case InfiniminerMessage.VibrateGamePad:
                                    {
                                        float strength = msgBuffer.ReadSingle();
                                        uint ms = msgBuffer.ReadUInt32();
                                        propertyBag.inputEngine.VibrateGamepad(strength, TimeSpan.FromMilliseconds(ms));
                                    }
                                    break;
                            }
                        }
                        break;
                }
            }

            // Make sure our network thread actually gets to run.
            Thread.Sleep(0);
        }

        private void CheckForStandingInLava()
        {
            // Copied from TryToMoveTo; responsible for checking if lava has flowed over us.

            Vector3 movePosition = propertyBag.playerPosition;
            Vector3 midBodyPoint = movePosition + new Vector3(0, -0.7f, 0);
            Vector3 lowerBodyPoint = movePosition + new Vector3(0, -1.4f, 0);
            BlockType lowerBlock = propertyBag.blockEngine.BlockAtPoint(lowerBodyPoint);
            BlockType midBlock = propertyBag.blockEngine.BlockAtPoint(midBodyPoint);
            BlockType upperBlock = propertyBag.blockEngine.BlockAtPoint(movePosition);
            if (upperBlock == BlockType.Lava || lowerBlock == BlockType.Lava || midBlock == BlockType.Lava)
            {
                propertyBag.KillPlayer("WAS INCINERATED BY LAVA!");
            }
        }

        protected override void Initialize()
        {
            graphicsDeviceManager.IsFullScreen = false;
            graphicsDeviceManager.PreferredBackBufferWidth = 1024;
            graphicsDeviceManager.PreferredBackBufferHeight = 768;
            graphicsDeviceManager.PreferredDepthStencilFormat = DepthFormat.Depth24Stencil8;
            graphicsDeviceManager.PreferMultiSampling = true;

            using(ConfigurationFileReader reader = new ConfigurationFileReader(TitleContainer.OpenStream("client.config.txt")))
            {
                ConfigurationItem? item = null;

                while((item = reader.ReadLine()) is not null)
                {
                    switch(item.Key)
                    {
                        case "width":
                            graphicsDeviceManager.PreferredBackBufferWidth = int.Parse(item.Value, System.Globalization.CultureInfo.InvariantCulture);
                            break;

                        case "height":
                            graphicsDeviceManager.PreferredBackBufferHeight = int.Parse(item.Value, System.Globalization.CultureInfo.InvariantCulture);
                            break;

                        case "fullscreen":
                            graphicsDeviceManager.IsFullScreen = bool.Parse(item.Value);
                            break;

                        case "resizing":
                            Window.AllowUserResizing = bool.Parse(item.Value);
                            break;

                        case "handle":
                            playerHandle = item.Value;
                            break;

                        case "showfps":
                            DrawFrameRate = bool.Parse(item.Value);
                            break;

                        case "yinvert":
                            InvertMouseYAxis = bool.Parse(item.Value);
                            break;
                        
                        case "nosound":
                            NoSound = bool.Parse(item.Value);
                            break;

                        case "pretty":
                            RenderPretty = bool.Parse(item.Value);
                            break;

                        case "volume":
                            volumeLevel = Math.Max(0, Math.Min(1, float.Parse(item.Value, System.Globalization.CultureInfo.InvariantCulture)));
                            break;

                        default: continue;
                    }
                }
            }

            graphicsDeviceManager.ApplyChanges();
            base.Initialize();
        }

        protected override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
        }

        protected void Game_OnExiting(object sender, EventArgs args)
        {
            propertyBag.netClient.Shutdown("Client exiting.");
        }

        public void ResetPropertyBag()
        {
            if (propertyBag != null)
                propertyBag.netClient.Shutdown("");

            propertyBag = new Infiniminer.PropertyBag(this);
            propertyBag.playerHandle = playerHandle;
            propertyBag.volumeLevel = volumeLevel;
            msgBuffer = propertyBag.netClient.CreateBuffer();
        }

        protected override void LoadContent()
        {
            // Initialize the property bag.
            ResetPropertyBag();

            // Set the initial state to team selection
            ChangeState("Infiniminer.States.TitleState");

            // Play the title music.
            if (!NoSound)
            {
                songTitle = Content.Load<Song>("song_title");
                MediaPlayer.Play(songTitle);
                MediaPlayer.Volume = propertyBag.volumeLevel;
            }
        }
    }
}
