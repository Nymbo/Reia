﻿/**
 * Copyright (C) 2020 Quaint Studios, Kristopher Ali (Makosai) <kristopher.ali.dev@gmail.com>
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as
 * published by the Free Software Foundation, either version 3 of the
 * License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */

namespace SustenetUnity
{
    using Sustenet.Transport;
    using System.Threading;
    using TMPro;
    using UnityEngine;
    using UnityEngine.UI;

    /// <summary>
    /// Creates and manages a Client.
    /// </summary>
    [RequireComponent(typeof(UnityThreadManager))]

    public class ClientManager : MonoBehaviour
    {
        #region Fields
        [SerializeField] private string ipAddress = "127.0.0.1";
        [SerializeField] private ushort port = 6256;
        public Client client;
        private bool gracefullyDisconnected = false; // If true, don't auto reconnect.

        private string _username;
        [SerializeField]
        public string Username
        {
            get
            {
                return _username;
            }

            set
            {
                if(value.Length >= Constants.USER_LEN_MIN && value.Length <= Constants.USER_LEN_MAX)
                {
                    _username = value;
                }
                else
                {
                    _username = "";
                }
            }
        }

        [System.Serializable]
        private class ClientInterface
        {
            public TMP_InputField username;
            public TMP_InputField password;
            public TMP_Text status;
            public Button login;
            public Button requestClusterServers;
            public Button reconnect;
        }
        [SerializeField] private ClientInterface Interface = new();

        private SynchronizationContext syncContext;
        #endregion

        #region Setup
        /// <summary>
        /// Sets up the client's events.
        /// </summary>
        private void SetupClient()
        {
            client.onConnected.Run += () => OnClientConnected();
            client.onDisconnected.Run += (proto) => OnClientDisconnected(proto);
            client.onInitialized.Run += () => OnClientInitialized();
            client.onClusterServerList.Run += (ci) => OnClusterServerList(ci);
            client.onReceived.Run += (protocol, data) => OnClientReceived(protocol, data);
        }

        /// <summary>
        /// Sets up the user interface events.
        /// </summary>
        private void SetupInterface()
        {
            Debug.Log("Client Interface Setup.");
            Interface.login.interactable = false;
            Interface.requestClusterServers.interactable = false;

            Interface.login.onClick.AddListener(() =>
            {
                Connect(Interface.username.text);
            });

            Interface.username.onValueChanged.AddListener((inputValue) =>
            {
                Username = inputValue;
            });

            Interface.requestClusterServers.onClick.AddListener(() =>
            {
                RequestClusterServers();
            });

            Interface.reconnect.onClick.AddListener(() =>
            {
                Debug.Log("Reconnecting manually...");
                gracefullyDisconnected = true;

                client.HandleDisconnect(Protocols.TCP);
                client.Connect();
            });

            syncContext = SynchronizationContext.Current;
        }

        private void Awake()
        {
            SetupInterface();
        }

        /// <summary>
        /// Create the client, set it up, and connect.
        /// </summary>
        private void Start()
        {
            Debug.Log("Starting...");

#if !UNITY_SERVER
            client = new Client(ipAddress, port);
            SetupClient();
            client.Connect();
            Debug.Log("Test");
#endif
        }

        /// <summary>
        /// Tries to connect to the master server with a username.
        /// TODO: Utilize a password. Or maybe a token.
        /// </summary>
        public void Connect(string username, string password = null)
        {
            Debug.Log($"Connecting with {Username} and {password}");
            if(Username != "") // If the username was set by the setter properly
            {
                // TODO: client.isConnected;
                client.Login(Username);
                return;
            }
        }

        public void RequestClusterServers()
        {
            Debug.Log($"Requesting cluster servers.");
            client.GetClusterServers();
        }
        #endregion

        #region Events
        /// <summary>
        /// After a client has successfully formed at least a TCP connection with the server.
        /// </summary>
        public void OnClientConnected()
        {
            Debug.Log("Client Connected.");
            gracefullyDisconnected = false;
            syncContext.Post(_ =>
            {
                Interface.login.interactable = true;
                Interface.requestClusterServers.interactable = true;
                Interface.status.text = "Connected";
                Interface.status.color = Color.green;
            }, null);
        }

        /// <summary>
        /// When a client loses connection to a server or closes itself.
        /// </summary>
        public void OnClientDisconnected(Protocols proto)
        {
            Debug.Log($"Client Disconnected ({proto}).");
            syncContext.Post(_ =>
            {
                Interface.login.interactable = false;
                Interface.requestClusterServers.interactable = false;
                Interface.status.text = "Lost connection...";
                Interface.status.color = Color.red;
            }, null);

            if(gracefullyDisconnected == false)
            {
                Debug.Log("Reconnecting...");
                client.Connect();
            }
        }

        /// <summary>
        /// After a client has fully logged in and gotten an ID & username validation back.
        /// This is also when the UDP connection is ready.
        /// </summary>
        public void OnClientInitialized()
        {
            // 1. Make sure ClusterServers are sending their name and extra data.
            // 2. Request all ClusterServers and their related data. <-- start this here
            // 3. Display them in the UI.
            // 4. Allow a user to click join to enter that cluster or
            //    auto select to find the best one (random currently).
            // 5. Handle disconnects properly. <-- don't do this here, separate it.
            Debug.Log("Client Initialized.");

        }

        /// <summary>
        /// Whenever the client gets a response with a list of cluster servers containing
        /// their name, ip, and port.
        /// </summary>
        /// <param name="clusterServers"></param>
        public void OnClusterServerList(Sustenet.World.ClusterInfo[] clusterServers)
        {
            Debug.Log("Received a list of cluster servers.");

            foreach(Sustenet.World.ClusterInfo clusterInfo in clusterServers)
            {
                Debug.Log(clusterInfo.name);
                Debug.Log(clusterInfo.ip);
                Debug.Log(clusterInfo.port);
                Debug.Log("===");
            }
            if(clusterServers.Length <= 0)
            {
                Debug.Log("No cluster servers registered.");
                return;
            }

            client.clusterConnection = new Client.Connection { Ip = clusterServers[0].ip, Port = clusterServers[0].port == 0 ? Sustenet.Utils. };
            client.HandleDisconnect(Protocols.TCP);
            client.Connect(Client.ConnectionType.ClusterServer);
        }

        /// <summary>
        /// Whenever a client receives data.
        /// </summary>
        /// <param name="protocol">TCP or UDP.</param>
        /// <param name="data">The data received.</param>
        public void OnClientReceived(Protocols protocol, byte[] data)
        {
            Debug.Log(protocol);
            Debug.Log(System.BitConverter.ToString(data));
        }
        #endregion
    }
}
