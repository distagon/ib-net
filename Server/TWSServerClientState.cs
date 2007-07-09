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
using IBNet;
using System.Net.Sockets;
using System.IO;
using System.Threading;
using IBNet.Client;
using System.Globalization;

namespace IBNet.Server
{
    public enum TWSServerState {
        Unknown,
        Reading,
        Writing
    }

    public class TWSServerClientState
    {
        public event EventHandler<TWSServerEventArgs> Login;
        public event EventHandler<TWSServerErrorEventArgs> Error;
        public event EventHandler<TWSMarketDataRequestEventArgs> MarketDataRequest;
        public event EventHandler<TWSMarketDepthRequestEventArgs> MarketDepthRequest;
        public event EventHandler<TWSMarketDataCancelEventArgs> MarketDataCancel;
        public event EventHandler<TWSMarketDataCancelEventArgs> MarketDepthCancel;


        private Dictionary<int, IBContract> _marketDataSubscriptions;
        private Dictionary<int, IBContract> _marketDepthSubscriptions;

        private const string IB_DATE_FORMAT = "yyyyMMdd  HH:mm:ss";
        private const string IB_HISTORICAL_COMPLETED = "finished";

        private byte[] _buffer;
        public virtual TWSClientStatus Status { get; protected set; }
        public virtual TWSServerState State { get; protected set; }
        public virtual TWSServer Server { get; protected set; }
        public virtual TWSServerInfo ServerInfo { get { return Server.ServerInfo; } }
        public virtual TWSClientInfo ClientInfo { get; protected set; }
        public virtual TWSClientId ClientId { get; protected set; }
        private Thread Thread { get; set; }

        private bool _stillConnected;
        

        private Stream _stream;
        protected ITWSEncoding _enc;

        public TWSServerClientState(Stream stream)
        {
            _stream = stream;
            Init();
        }


        public TWSServerClientState(TWSServer server, Stream stream)
        {
            Server = server;
            _stream = stream;
            Init();
        }

        private void Init()
        {
            Status = TWSClientStatus.Unknown;
            // Create buffered read stream to minimize the # of recv system calls
            _enc = new TWSEncoding(_stream);
            _stillConnected = true;
            _marketDataSubscriptions = new Dictionary<int, IBContract>();
            _marketDepthSubscriptions = new Dictionary<int, IBContract>();
        }


        public void Start()
        {
            Thread t = new Thread(new ThreadStart(this.ProcessMessages));
            Thread = t;
            Thread.Start();
        }

        public void ProcessMessages()
		{
            try {
                while (_stillConnected) {
                    if (!ProcessSingleMessage())
                        break;
                }
            }

            catch (Exception e) {
                Server.OnError(new TWSError(TWSErrors.NO_VALID_CODE, e.Message));
            }
            Status = TWSClientStatus.Disconnected;
        }

        public bool ProcessSingleMessage()
        {
            // If the client status is unknow, it means it is still trying to login...
            if (Status == TWSClientStatus.Unknown) {
                ProcessLogin();
                return true;
            }

            if (Status != TWSClientStatus.Connected)
                return false;

            Messages.Server msgCode = _enc.DecodeServerMessage();

            switch (msgCode) {
                case Messages.Server.SET_SERVER_LOGLEVEL: ProcessSetServerLogLevel(); break;
                case Messages.Server.REQ_ACCOUNT_DATA: ProcessAccountDataRequest(); break;
                case Messages.Server.REQ_CONTRACT_DATA: ProcessContractDataRequest(); break;
                case Messages.Server.REQ_CURRENT_TIME: ProcessCurrentTimeRequest(); break;
                case Messages.Server.REQ_MKT_DATA: ProcessMarketDataRequest(); break;
                case Messages.Server.CANCEL_MKT_DATA: ProcessMarketDataCancel(); break;
                case Messages.Server.REQ_MKT_DEPTH: ProcessMarketDepthRequest(); break;
                case Messages.Server.CANCEL_MKT_DEPTH: ProcessMarketDepthCancel(); break;
                case Messages.Server.REQ_REAL_TIME_BARS: ProcessRealTimeBarsRequest(); break;
                case Messages.Server.CANCEL_REAL_TIME_BARS: ProcessRealTimeBarsCancel(); break;
                case Messages.Server.REQ_AUTO_OPEN_ORDERS: ProcessAutoOpenOrdersRequest(); break;
                case Messages.Server.REQ_ALL_OPEN_ORDERS: ProcessAllOpenOrdersRequest(); break;
                case Messages.Server.REQ_HISTORICAL_DATA: ProcessHistoricalDataRequest(); break;
                case Messages.Server.CANCEL_HISTORICAL_DATA: ProcessHistoricalDataCancel(); break;
                case Messages.Server.REQ_EXECUTIONS: ProcessExecutionsRequest(); break;
                case Messages.Server.PLACE_ORDER: ProcessPlaceOrder(); break;
                case Messages.Server.CANCEL_ORDER: ProcessCancelOrder(); break;
            }
            return true;
        }


        private void ProcessPlaceOrder()
        {
        }

        private void ProcessCancelOrder()
        {
        }

        private void ProcessExecutionsRequest()
        {
        }

        private void ProcessHistoricalDataCancel()
        {
        }

        private void ProcessHistoricalDataRequest()
        {
        }

        private void ProcessAllOpenOrdersRequest()
        {
        }

        private void ProcessAutoOpenOrdersRequest()
        {
        }

        private void ProcessRealTimeBarsCancel()
        {
        }

        private void ProcessRealTimeBarsRequest()
        {
        }

        private void ProcessMarketDepthCancel()
        {
            int reqId = -1;

            try {
                var reqVersion = _enc.DecodeInt();
                reqId = _enc.DecodeInt();
                OnMarketDepthCancel(reqId);
            }
            catch (Exception e) {
                OnError(reqId, TWSErrors.FAIL_SEND_CANMKTDEPTH);
                OnError(e.Message);
                Disconnect();
            }
        }

        private void ProcessMarketDepthRequest()
        {
            try {
                var reqVersion = _enc.DecodeInt();
                var reqId = _enc.DecodeInt();
                int numRows = 0;

                var contract = new IBContract();

                contract.Symbol = _enc.DecodeString();
                contract.SecType = _enc.DecodeSecType();
                contract.Expiry = _enc.DecodeExpiryDate();
                contract.Strike = _enc.DecodeDouble();
                contract.Right = _enc.DecodeString();
                if (ServerInfo.Version >= 15)
                    contract.Multiplier = _enc.DecodeInt();
                contract.Exchange = _enc.DecodeString();
                contract.Currency = _enc.DecodeString();
                contract.LocalSymbol =_enc.DecodeString();
                if (ServerInfo.Version >= 19)
                    numRows = _enc.DecodeInt();
                OnMarketDepthRequest(reqId, contract, numRows);
            }
            catch (Exception e) {
                OnError(TWSErrors.FAIL_SEND_REQMKTDEPTH);
                OnError(e.Message);
                Disconnect();
            }
        }

        private void ProcessMarketDataCancel()
        {
            int reqId = -1;

            try {
                var reqVersion = _enc.DecodeInt();
                reqId = _enc.DecodeInt();
                OnMarketDataCancel(reqId);
            }
            catch (Exception e) {
                OnError(reqId, TWSErrors.FAIL_SEND_CANMKT);
                OnError(e.Message);
                Disconnect();
            }
        }

        private void ProcessMarketDataRequest()
        {
            int reqId = -1;
            try {
                int reqVersion = _enc.DecodeInt();
                reqId = _enc.DecodeInt();

                var contract = new IBContract();
                contract.Symbol = _enc.DecodeString();
                contract.SecType = _enc.DecodeSecType();
                contract.Expiry = _enc.DecodeExpiryDate();
                contract.Strike = _enc.DecodeDouble();
                contract.Right = _enc.DecodeString();
                if (ServerInfo.Version >= 15)
                    contract.Multiplier = _enc.DecodeInt();
                contract.Exchange = _enc.DecodeString();
                if (ServerInfo.Version >= 14)
                    contract.PrimaryExch = _enc.DecodeString();
                contract.Currency = _enc.DecodeString();
                if (ServerInfo.Version >= 2)
                    contract.LocalSymbol = _enc.DecodeString();
                if (ServerInfo.Version >= 8 && (contract.SecType == IBSecType.BAG)) {
                    int comboLegCount = _enc.DecodeInt();
                    for (var i = 0; i < comboLegCount; i++) {
                        IBComboLeg leg = new IBComboLeg();
                        leg.ConId = _enc.DecodeInt();
                        leg.Ratio = _enc.DecodeInt();
                        leg.Action = _enc.DecodeAction();
                        leg.Exchange = _enc.DecodeString();
                        contract.ComboLegs.Add(leg);
                    }
                }
                
                if (ServerInfo.Version >= 31) {
                    string genericTickList = _enc.DecodeString();
                    if (genericTickList != null && genericTickList.Length > 0) {
                        var list = new List<IBGenericTickType>();
                        foreach (var s in genericTickList.Split(','))
                            list.Add((IBGenericTickType)Enum.Parse(typeof(IBGenericTickType), s));
                    }
                }

                OnMarketDataRequest(reqId, contract);
            }
            catch (Exception e) {
                OnError(reqId, TWSErrors.FAIL_SEND_REQMKT);
                OnError(e.Message);
                Disconnect();
            }
        }

        private void ProcessCurrentTimeRequest()
        {
        }

        private void ProcessContractDataRequest()
        {
            try {
                // send req mkt data msg
                var reqVersion = _enc.DecodeInt();

                var contract = new IBContract();

                contract.Symbol = _enc.DecodeString();
                contract.SecType = _enc.DecodeSecType();
                contract.Expiry = _enc.DecodeExpiryDate();
                contract.Strike = _enc.DecodeDouble();
                contract.Right = _enc.DecodeString();
                if (ServerInfo.Version >= 15) {
                    contract.Multiplier = _enc.DecodeInt();
                }
                contract.Exchange = _enc.DecodeString();
                contract.Currency = _enc.DecodeString();
                contract.LocalSymbol = _enc.DecodeString();
                if (ServerInfo.Version >= 31) {
                    contract.IncludeExpired = _enc.DecodeBool();
                }
                OnContractDataRequest(contract);
            }
            catch (Exception e) {
                OnError(TWSErrors.FAIL_SEND_REQCONTRACT);
                OnError(e.Message);
                Disconnect();
            }
        }

        private void ProcessAccountDataRequest()
        {
        }

        private void ProcessSetServerLogLevel()
        {
        }

        private void ProcessLogin()
        {
            try {
                ClientInfo = _enc.DecodeClientInfo();
                _enc.Encode(ServerInfo);
                _enc.Encode("TWS Local Time is to go fuck yourself");
                ClientId = _enc.DecodeClientId();
                Status = TWSClientStatus.Connected;
                OnLogin(ClientInfo, ClientId);
            }
            catch (Exception e) {
                Server.OnError(TWSErrors.CONNECT_FAIL);
                Server.OnError(new TWSError(TWSErrors.NO_VALID_CODE, e.Message));
            }
        }

        private void OnLogin(TWSClientInfo clientInfo, TWSClientId clientId)
        {
            if (Server != null)
                Server.OnLogin(this, clientInfo, clientId);
        }
        private void OnContractDataRequest(IBContract contract)
        {
            if (Server != null)
                Server.OnContractDetailsRequest(this, contract);
        }
        private void OnMarketDataRequest(int reqId, IBContract contract)
        {
            if (Server != null)
                Server.OnMarketDataRequest(this, reqId, contract);

            if (MarketDataRequest != null)
                MarketDataRequest(this, new TWSMarketDataRequestEventArgs(this, reqId, contract));

            _marketDataSubscriptions.Add(reqId, contract);
        }
        private void OnMarketDepthCancel(int reqId)
        {
            IBContract contract = null;

            if (_marketDepthSubscriptions.TryGetValue(reqId, out contract))
                _marketDepthSubscriptions.Remove(reqId);

            if (Server != null)
                Server.OnMarketDepthCancel(this, reqId, contract);

            if (MarketDataCancel != null)
                MarketDepthCancel(this, new TWSMarketDataCancelEventArgs(this, reqId, contract));
        }
        private void OnMarketDataCancel(int reqId)
        {
            IBContract contract = null;

            if (_marketDataSubscriptions.TryGetValue(reqId, out contract))
                _marketDataSubscriptions.Remove(reqId);

            if (Server != null)
                Server.OnMarketDataCancel(this, reqId, contract);

            if (MarketDataCancel != null)
                MarketDataCancel(this, new TWSMarketDataCancelEventArgs(this, reqId, contract));
        }
        private void OnMarketDepthRequest(int reqId, IBContract contract, int numRows)
        {
            if (Server != null)
                Server.OnMarketDepthRequest(this, reqId, contract, numRows);

            if (MarketDepthRequest != null)
                MarketDepthRequest(this, new TWSMarketDepthRequestEventArgs(this, reqId, contract, numRows));
        }

        private void OnError(string message)
        {
            OnError(TWSErrors.NO_VALID_ID, new TWSError(TWSErrors.NO_VALID_CODE, message));
        }

        private void OnError(TWSError error)
        {
            OnError(TWSErrors.NO_VALID_ID, error);
        }

        private void OnError(int reqId, TWSError error)
        {
            if (Server != null)
                Server.OnError(error);

            if (Error != null)
                Error(this, new TWSServerErrorEventArgs(this, error));
        }

        internal void Disconnect()
        {
            throw new Exception("The method or operation is not implemented.");
        }
    }
}
