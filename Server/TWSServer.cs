#region Copyright (c) 2007 by Dan Shechter
////////////////////////////////////////////////////////////////////////////////////////
////
//  IBNet, an Interactive Brokers TWS .NET Client & Server implmentation
//  by Dan Shechter
////////////////////////////////////////////////////////////////////////////////////////
//  License: MPL 1.1/GPL 2.0/LGPL 2.1
//  
//  The contents of this file are subject to the Mozilla Public License Version 
//  1.1 (the "License"); you may not use this file except in compliance with 
//  the License. You may obtain a copy of the License at 
//  http://www.mozilla.org/MPL/
//  
//  Software distributed under the License is distributed on an "AS IS" basis,
//  WITHOUT WARRANTY OF ANY KIND, either express or implied. See the License
//  for the specific language governing rights and limitations under the
//  License.
//  
//  The Original Code is any part of this file that is not marked as a contribution.
//  
//  The Initial Developer of the Original Code is Dan Shecter.
//  Portions created by the Initial Developer are Copyright (C) 2007
//  the Initial Developer. All Rights Reserved.
//  
//  Contributor(s): None.
//  
//  Alternatively, the contents of this file may be used under the terms of
//  either the GNU General Public License Version 2 or later (the "GPL"), or
//  the GNU Lesser General Public License Version 2.1 or later (the "LGPL"),
//  in which case the provisions of the GPL or the LGPL are applicable instead
//  of those above. If you wish to allow use of your version of this file only
//  under the terms of either the GPL or the LGPL, and not to allow others to
//  use your version of this file under the terms of the MPL, indicate your
//  decision by deleting the provisions above and replace them with the notice
//  and other provisions required by the GPL or the LGPL. If you do not delete
//  the provisions above, a recipient may use your version of this file under
//  the terms of any one of the MPL, the GPL or the LGPL.
////////////////////////////////////////////////////////////////////////////////////////
#endregion
using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Globalization;
using System.IO;
using IBNet.Client;

namespace IBNet.Server
{
    public class TWSServer
    {
        //public event EventHandler<TWSClientStatusEventArgs> StatusChanged;

        public event EventHandler<TWSServerEventArgs> Login;
        public event EventHandler<TWSServerErrorEventArgs> Error;
        public event EventHandler<TWSTcpClientConnectedEventArgs> TcpClientConnected;
        public event EventHandler<TWSMarketDataRequestEventArgs> MarketDataRequest;
        public event EventHandler<TWSMarketDataRequestEventArgs> MarketDepthRequest;
        public event EventHandler<TWSMarketDataCancelEventArgs> MarketDataCancel;
        public event EventHandler<TWSMarketDataCancelEventArgs> MarketDepthCancel;


        
        public const int DEFAULT_PORT = 7496;

        private TcpListener _listener;
        public TWSServerInfo ServerInfo { get; private set; }
        public TWSServerStatus Status { get; private set; }

        public bool IsRunning { get { return Status == TWSServerStatus.Running; } }

        private List<TWSServerClientState> _clients;
        private int _clientCount;
        private AsyncCallback _connectCallback;

        private const int DEFAULT_BUFFER_SIZE = 4096;

        private const int SERVER_VERSION = 34;

        public TWSServer()
        {
            _clients = new List<TWSServerClientState>();
            _clientCount = 0;
            ServerInfo = new TWSServerInfo(SERVER_VERSION);
            EndPoint = new IPEndPoint(IPAddress.Loopback, DEFAULT_PORT);
        }

        public void Start()
        {
            _listener = new TcpListener(EndPoint);
            _connectCallback = new AsyncCallback(OnTcpClientConnect);
            _listener.Start();
            _listener.BeginAcceptTcpClient(_connectCallback, null);
            Status = TWSServerStatus.Running;
        }

        public void Stop()
        {
            foreach (var c in _clients)
                c.Disconnect();

            _listener.Stop();
        }

        public IPEndPoint EndPoint { get; set; }

        protected internal void OnError(TWSError error)
        {
            if (Error != null)
                Error(this, new TWSServerErrorEventArgs(this, error));
        }


        public virtual void OnTcpClientConnect(IAsyncResult asyn)
        {
            if (TcpClientConnected != null)
                TcpClientConnected(this, new TWSTcpClientConnectedEventArgs(this, null));
            try
            {
                // Here we complete/end the BeginAccept() asynchronous call
                // by calling EndAccept() - which returns the reference to
                // a new Socket object
                TcpClient tc = _listener.EndAcceptTcpClient(asyn);                
                _clientCount++;
                var s = new BufferedReadStream(tc.GetStream(), DEFAULT_BUFFER_SIZE);
                TWSServerClientState connection = new TWSServerClientState(this, s);

                lock (_clients) {
                    _clients.Add(connection);
                }
                
                connection.Start();

                // Since the main Socket is now free, it can go back and wait for
                // other clients who are attempting to connect
                _listener.BeginAcceptTcpClient(_connectCallback, null);
            }
            catch (ObjectDisposedException)
            {
                System.Diagnostics.Debugger.Log(0, "1", "\n OnClientConnection: Socket has been closed\n");
            }
            catch (SocketException se)
            {
                OnError(new TWSError(TWSErrors.NO_VALID_CODE, se.Message));
            }
        }

        public virtual void OnTWSClientConnect(TWSServerClientState client, int clientId)
        {
        }

        public virtual void OnMarketDataRequest(TWSServerClientState client, int reqId, IBContract contract)
        {
            if (MarketDataRequest != null)
                MarketDataRequest(this, new TWSMarketDataRequestEventArgs(client, reqId, contract));
        }
        public virtual void OnMarketDataCancel(TWSServerClientState client, int reqId, IBContract contract)
        {
            if (MarketDataCancel != null)
                MarketDataCancel(this, new TWSMarketDataCancelEventArgs(client, reqId, contract));
        }
        public virtual void OnMarketDepthCancel(TWSServerClientState client, int reqId, IBContract contract)
        {
            if (MarketDataCancel != null)
                MarketDataCancel(this, new TWSMarketDataCancelEventArgs(client, reqId, contract));
        }
        public virtual void OnContractDetailsRequest(TWSServerClientState client, IBContract contract)
        {
        }
        public virtual void OnLogin(TWSServerClientState clientState, TWSClientInfo clientInfo, TWSClientId clientId)
        {
            if (Login != null)
                Login(this, new TWSServerEventArgs(clientState));
        }
        public virtual void OnMarketDepthRequest(TWSServerClientState clientState, int reqId, IBContract contract, int numRows)
        {
            if (MarketDepthRequest != null)
                MarketDepthRequest(this, new TWSMarketDepthRequestEventArgs(clientState, reqId, contract, numRows));
        }
    }
}
