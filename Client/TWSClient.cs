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
using System.ComponentModel;
using System.Diagnostics;

namespace IBNet.Client
{
    public class TWSClient
    {
        public event EventHandler<TWSClientStatusEventArgs> StatusChanged;
        public event EventHandler<TWSClientErrorEventArgs> Error;
        public event EventHandler<TWSTickPriceEventArgs> TickPrice;
        public event EventHandler<TWSTickSizeEventArgs> TickSize;
        public event EventHandler<TWSTickStringEventArgs> TickString;
        public event EventHandler<TWSTickGenericEventArgs> TickGeneric;
        public event EventHandler<TWSTickOptionComputationEventArgs> TickOptionComputation;
        public event EventHandler<TWSTickEFPEventArgs> TickEFP;
        public event EventHandler<TWSCurrentTimeEventArgs> CurrentTime;
        public event EventHandler<TWSOrderStatusEventArgs> OrderStatus;
        public event EventHandler<TWSOpenOrderEventArgs> OpenOrder;
        public event EventHandler<TWSContractDetailsEventArgs> BondContractDetails;
        public event EventHandler<TWSContractDetailsEventArgs> ContractDetails;
        public event EventHandler<TWSUpdatePortfolioEventArgs> UpdatePortfolio;
        public event EventHandler<TWSExecDetailsEventArgs> ExecDetails;
        public event EventHandler<TWSMarketDepthEventArgs> MarketDepth;
        public event EventHandler<TWSMarketDepthEventArgs> MarketDepthL2;
        public event EventHandler<TWSHistoricalDataEventArgs> HistoricalData;
        public event EventHandler<TWSMarketDataEventArgs> MarketData;

        public const string DEFAULT_HOST = "127.0.0.1";
        public const int DEFAULT_PORT = 7496;

        private const string IB_EXPIRY_DATE_FORMAT = "yyyyMMdd";
        private const string IB_DATE_FORMAT = "yyyyMMdd  HH:mm:ss";
        private const string IB_HISTORICAL_COMPLETED = "finished";

        private string NUMBER_DECIMAL_SEPARATOR = null;
        private bool _doWork;
        private bool _reconnect;
        private int _nextValidId;

        private int _clientId;

        private string _twsTime;

        private Stream _stream;
        private Stream _recordStream;

        private bool _recordForPlayback;
        private IPEndPoint _endPoint;
        private TcpClient _tcpClient;
        private Thread _thread;
        protected Dictionary<int, TWSMarketDataSnapshot> _marketDataRecords;
        //private Dictionary<int, TWSMarketDepthSnapshot> _marketDepthRecords;
        private Dictionary<IBContract, KeyValuePair<AutoResetEvent, IBContractDetails>> _internalDetailRequests;
        private Dictionary<string, int> _orderIds;
        private Dictionary<int, OrderRecord> _orderRecords;
        protected ITWSEncoding _enc;

        private const int DEFAULT_WAIT_TIMEOUT = 10000;

        private TWSClientSettings _settings;

        #region Constructors
        public TWSClient()
        {
            _tcpClient = null;
            _stream = null;
            _thread = null;
            Status = TWSClientStatus.Unknown;
            _twsTime = String.Empty;
            _nextValidId = 0;

            //_historicalDataRecords = new Dictionary<int, TWSMarketDataSnapshot>();
            _marketDataRecords = new Dictionary<int, TWSMarketDataSnapshot>();
            //_marketDepthRecords = new Dictionary<int, TWSMarketDataSnapshot>();
            _orderRecords = new Dictionary<int, OrderRecord>();
            _internalDetailRequests = new Dictionary<IBContract, KeyValuePair<AutoResetEvent, IBContractDetails>>();
            _orderIds = new Dictionary<string, int>();

            ClientInfo = new TWSClientInfo();

            NUMBER_DECIMAL_SEPARATOR = NumberFormatInfo.CurrentInfo.NumberDecimalSeparator;
            EndPoint = new IPEndPoint(new IPAddress(new byte[] { 127, 0, 0, 1 }), DEFAULT_PORT);

            _settings = new TWSClientSettings();
        }
        public TWSClient(IPEndPoint server) : this()
        {
            _endPoint = server;
        }

        public TWSClient(string host, int port) : this()
        {
            _endPoint = new IPEndPoint(Dns.GetHostEntry(host).AddressList[0], port);
        }
        #endregion

        #region Connect/Disconnect
        
        /// <summary>
        /// Connect to the specified IB Trader Workstation Endpoint, auto-calculating the client id
        /// </summary>
        /// <remarks>
        /// Using this method may result in the connection being reject for duplicate client id, since the
        /// client id is automatically calculated from the local end point (ip address & port)
        /// </remarks>
        public void Connect()
        { Connect(-1); }

        /// <summary>
        /// Connect to the specified IB Trader Workstation Endpoint
        /// </summary>
        /// <param name="clientId">The client id to use when connecting</param>
        public void Connect(int clientId)
        {
            lock (this) {
                if (IsConnected) {
                    OnError(TWSErrors.ALREADY_CONNECTED);
                    return;
                }
                try {
                    _tcpClient = new TcpClient();
                    _tcpClient.Connect(_endPoint);                    
                    _tcpClient.NoDelay = true;

                    if (RecordForPlayback)
                    {
                        if (_recordStream == null)
                            _recordStream = SetupDefaultRecordStream();

                        _enc = new TWSPlaybackEncoding(new BufferedReadStream(_tcpClient.GetStream()), _recordStream);
                    } else
                        _enc = new TWSEncoding(new BufferedReadStream(_tcpClient.GetStream()));

                    
                    _enc.Encode(ClientInfo);
                    _doWork = true;

                    // Only create a reader thread if this Feed IS NOT reconnecting
                    if (!_reconnect) {
                        _thread = new Thread(new ThreadStart(ProcessMessages));
                        _thread.Name = "IB Reader";
                    }
                    // Get the server version
                    ServerInfo = _enc.DecodeServerInfo();
                    if (ServerInfo.Version >= 20) {
                        _twsTime = _enc.DecodeString();
                    }

                    // Send the client id
                    if (ServerInfo.Version >= 3)
                    {
                        if (clientId == -1)
                        {
                            if (_tcpClient.Client.LocalEndPoint is IPEndPoint)
                            {
                                IPEndPoint p = _tcpClient.Client.LocalEndPoint as IPEndPoint;
                                byte[] ab = p.Address.GetAddressBytes();
                                clientId = ab[ab.Length - 1] << 16 | p.Port;
                            }
                            else
                                clientId = new Random().Next();
                        }
                        TWSClientId id = new TWSClientId(clientId);
                        _enc.Encode(id);
                    }

                    // Only start the thread if this Feed IS NOT reconnecting
                    if (!_reconnect)
                        _thread.Start();

                    _clientId = clientId;
                    OnStatusChanged(Status = TWSClientStatus.Connected);                    
                }
                catch (Exception e) {
                    OnError(e.Message);
                    OnError(TWSErrors.CONNECT_FAIL);
                }
            }
        }

        /// <summary>
        /// Disconnect from the IB Trader Workstation endpoint
        /// </summary>
        public void Disconnect()
        {
            if (!IsConnected)
                return;

            lock (this) {
                _doWork = false;
                if (_tcpClient != null)
                    _tcpClient.Close();
                _thread = null;
                _tcpClient = null;
                _stream = null;
                if (RecordStream != null)
                {
                    RecordStream.Flush();
                    RecordStream.Close();
                }
                OnStatusChanged(Status = TWSClientStatus.Disconnected);                
            }

        }

        /// <summary>
        /// Reconnect to the IB Trader Workstation, re-register all markert data requests
        /// </summary>
        public void Reconnect()
        {
            if (!IsConnected)
                return;

            lock (this) {
                _reconnect = true;
                Disconnect();
                Connect(_clientId);
            }
        }
        #endregion

        #region Cancel Messages
        /// <summary>
        /// Cancel a registered scanner subscription
        /// </summary>
        /// <param name="reqId">The scanner subscription request id</param>
        public void CancelScannerSubscription(int reqId)
        {

            lock (this)
            {
                // not connected?
                if (!IsConnected) {
                    OnError(TWSErrors.NOT_CONNECTED);
                    return;
                }

                if (ServerInfo.Version < 24) {
                    OnError(TWSErrors.UPDATE_TWS);
                    return;
                }

                int reqVersion = 1;

                // Send cancel mkt data msg
                try {
                    _enc.Encode(Messages.Server.CANCEL_SCANNER_SUBSCRIPTION);
                    _enc.Encode(reqVersion);
                    _enc.Encode(reqId);
                }
                catch (System.Exception e)
                {
                    OnError(reqId, TWSErrors.FAIL_SEND_CANSCANNER);
                    OnError(e.Message);
                    Disconnect();                    
                }
            }
        }

        /// <summary>
        /// Cancel historical data subscription
        /// </summary>
        /// <param name="reqId">The historical data subscription request id</param>
        public void CancelHistoricalData(int reqId)
        {
            lock (this) {
                // not connected?
                if (!IsConnected) {
                    OnError(TWSErrors.NOT_CONNECTED);
                    return;
                }

                if (ServerInfo.Version < 24) {
                    OnError(TWSErrors.UPDATE_TWS);
                    return;
                }

                int reqVersion = 1;

                // Send cancel mkt data msg
                try {
                    _enc.Encode(Messages.Server.CANCEL_HISTORICAL_DATA);
                    _enc.Encode(reqVersion);
                    _enc.Encode(reqId);
                }
                catch (System.Exception e) {
                    OnError(reqId, TWSErrors.FAIL_SEND_CANHISTDATA);
                    OnError(e.Message);
                    Disconnect();
                }
            }
        }

        public void CancelMarketData(int reqId)
        {
            lock (this) {
                if (!IsConnected) {
                    OnError(TWSErrors.NOT_CONNECTED);
                    return;
                }
                int reqVersion = 1;
                try {
                    _enc.Encode(Messages.Server.CANCEL_MKT_DATA);
                    _enc.Encode(reqVersion);
                    _enc.Encode(reqId);
                }
                catch (Exception e) {
                    OnError(reqId, TWSErrors.FAIL_SEND_CANMKT);
                    OnError(e.Message);
                    Disconnect();
                }
            }
        }
        public void CancelMarketDepth(int reqId)
        {

            lock (this) {
                if (!IsConnected) {
                    OnError(TWSErrors.NOT_CONNECTED);
                    return;
                }
                if (ServerInfo.Version < 6) {
                    OnError(TWSErrors.UPDATE_TWS);
                    return;
                }
                int reqVersion = 1;
                try {
                    _enc.Encode(Messages.Server.CANCEL_MKT_DEPTH);
                    _enc.Encode(reqVersion);
                    _enc.Encode(reqId);
                }
                catch (Exception e) {
                    OnError(TWSErrors.FAIL_SEND_CANMKTDEPTH);
                    OnError(e.Message);
                    Disconnect();
                }
            }
        }
        public void CancelNewsBulletins()
        {
            lock (this) {
                // not connected?
                if (!IsConnected) {
                    OnError(TWSErrors.NOT_CONNECTED);
                    return;
                }

                int reqVersion = 1;

                // Send cancel order msg
                try {
                    _enc.Encode(Messages.Server.CANCEL_NEWS_BULLETINS);
                    _enc.Encode(reqVersion);
                }
                catch (System.Exception e) {
                    OnError(TWSErrors.FAIL_SEND_CORDER);
                    OnError(e.Message);
                    Disconnect();
                }
            }
        }

        public void CancelOrder(int orderId)
        {
            lock (this) {
                if (!IsConnected) {
                    OnError(TWSErrors.NOT_CONNECTED);
                    return;
                }
                int reqVersion = 1;
                try {
                    _enc.Encode(Messages.Server.CANCEL_ORDER);
                    _enc.Encode(reqVersion);
                    _enc.Encode(orderId);
                }
                catch (Exception e) {
                    OnError(TWSErrors.FAIL_SEND_CORDER);
                    OnError(e.Message);
                    Disconnect();
                }
            }
        }

        public void CancelRealTimeBars(int reqId)
        {
            // not connected?
            if (!IsConnected)
            {
                OnError(TWSErrors.NOT_CONNECTED);
                return;
            }

            if (ServerInfo.Version < 34)
            {
                OnError(TWSErrors.UPDATE_TWS);
                return;
            }

            int reqVersion = 1;

            // send cancel mkt data msg
            try
            {
                _enc.Encode(Messages.Server.CANCEL_HISTORICAL_DATA);
                _enc.Encode(reqVersion);
                _enc.Encode(reqId);
            }
            catch (Exception e)
            {
                OnError(reqId, TWSErrors.FAIL_SEND_CANRTBARS);
                OnError(e.Message);
                Disconnect();
            }
        }
        #endregion

        #region Event Notifiers
        protected virtual void OnStatusChanged(TWSClientStatus status)
        {
            if (StatusChanged != null)
                StatusChanged(this, new TWSClientStatusEventArgs(this, status));
        }

        protected virtual void OnError(TWSError error)
        {
            OnError(TWSErrors.NO_VALID_ID, error);
        }

        protected virtual void OnError(int reqId, TWSError error)
        {
            if (Error != null) {
                TWSMarketDataSnapshot snapshot;
                IBContract contract = null;

                if (_marketDataRecords.TryGetValue(reqId, out snapshot))
                    contract = snapshot.Contract;

                Error(this, new TWSClientErrorEventArgs(this, reqId, contract, error));
            }
        }
        protected void OnError(string message)
        { OnError(new TWSError(TWSErrors.NO_VALID_CODE, message)); }

        protected void OnMarketData(TWSMarketDataSnapshot snapshot, IBTickType tickType)
        {
            if (MarketData != null)
                MarketData(this, new TWSMarketDataEventArgs(this, snapshot, tickType));
        }

        protected void OnTickPrice(int reqId, IBTickType tickType, double price, int size, int canAutoExecute)
        {
            if (TickPrice != null)
                TickPrice(this, new TWSTickPriceEventArgs(this, reqId, tickType, price, size, canAutoExecute));

            TWSMarketDataSnapshot record;
            if (!_marketDataRecords.TryGetValue(reqId, out record)) {
                OnError(String.Format("OnTickPrice: Unknown request id {0}", reqId));
                return;
            }

            // Crap?
            if (record.Contract.SecType != IBSecType.IND &&
                record.Contract.SecType != IBSecType.CASH &&
                size == 0)
                return;

            switch (tickType) {
                case IBTickType.BID:
                    record.Bid = price;
                    if (!_settings.IgnoreSizeInPriceTicks)
                        record.BidSize = size;                    
                    record.BidTimeStamp = DateTime.Now;
                    break;
                case IBTickType.ASK:
                    record.Ask = price;
                    if (!_settings.IgnoreSizeInPriceTicks)
                        record.AskSize = size;
                    record.AskTimeStamp = DateTime.Now;
                    break;
                case IBTickType.LAST:
                    // Make sure we are allowed to generate trades from this event type
                    if ((_settings.TradeGeneration & TradeGeneration.LastSizePrice) == 0)
                        return;
                    record.Last = price;
                    if (!_settings.IgnoreSizeInPriceTicks)
                        record.LastSize = size;
                    record.TradeTimeStamp = DateTime.Now;
                    break;
                case IBTickType.HIGH:
                    record.High = price;
                    break;
                case IBTickType.LOW:
                    record.Low = price;
                    break;
                case IBTickType.CLOSE:
                    record.Close = price;
                    break;
                case IBTickType.OPEN:
                    record.Open = price;
                    break;

                default:
                    throw new ArgumentException("Unknown tick type - " + tickType);
            }
            OnMarketData(record, tickType);
        }

        protected void OnTickSize(int reqId, IBTickType tickType, int size)
        {
            if (size == 0)
                return;

            if (TickSize != null)
                TickSize(this, new TWSTickSizeEventArgs(this, reqId, tickType, size));

            TWSMarketDataSnapshot record;
            if (!_marketDataRecords.TryGetValue(reqId, out record)) {
                OnError(String.Format("OnTickPrice: Unknown request id {0}", reqId));
                return;
            }

            int recordSize = size;
            bool lastDupHit = false;

            switch (tickType) {
                case IBTickType.BID_SIZE:                  
                    if (record.BidSize == size && FilterDups(record.BidTimeStamp))
                    {
                        lastDupHit = true;
                        record.BidDups++;
                        break;
                    }
                    record.BidSize = size;
                    break;
                case IBTickType.ASK_SIZE:                    
                    if (record.AskSize == size && FilterDups(record.AskTimeStamp)) {                        
                        lastDupHit = true;
                        record.AskDups++;
                        break;
                    }
                    record.AskSize = size;
                    break;
                case IBTickType.LAST_SIZE:
                    // Make sure we are allowed to generate trades from this event type
                    if ((_settings.TradeGeneration & TradeGeneration.LastSize) == 0)
                        return;
                    if (record.LastSize == size && FilterDups(record.TradeTimeStamp))
                    {
                        lastDupHit = true;
                        record.TradeDups++;
                        break;
                    }
                    record.LastSize = size;
                    break;
                case IBTickType.VOLUME:                    
                    record.Volume = size;
                    if ((_settings.TradeGeneration & TradeGeneration.Volume) != 0) {
                        // Synthetic volume matches reported volume
                        if (record.SyntheticVolume == size)
                            break;

                        // This is just plain wrong... we may want to raise some sort
                        // of red flag here..?!?
                        if (record.SyntheticVolume > size)
                            break;

                        // If we got to here, it means we need to generate a trade from volume changes
                        // with the last price using the volume difference between
                        // the reported volume and the synthetic one...
                        record.LastSize = (size - record.SyntheticVolume);
                    }
                    break;
                default:
                    throw new ArgumentException("Unknown tick type - " + tickType);
            }
            if (lastDupHit == false)
                OnMarketData(record, tickType);
        }

        private bool FilterDups(DateTime dateTime)
        {
            return _settings.UseDupFilter && 
                   (DateTime.Now.Subtract(dateTime) < _settings.DupDetectionTimeout);
        }
        protected void OnTickOptionComputation(int reqId, IBTickType tickType, 
                                               double impliedVol, double delta, double modelPrice, double pvDividend)
        {
            if (TickOptionComputation != null)
                TickOptionComputation(this, 
                                      new TWSTickOptionComputationEventArgs(this, reqId, tickType, 
                                                                            impliedVol, delta, modelPrice, pvDividend));

            TWSMarketDataSnapshot record;
            if (!_marketDataRecords.TryGetValue(reqId, out record)) {
                OnError(String.Format("OnTickPrice: Unknown request id {0}", reqId));
                return;
            }

            switch (tickType)
            {
                case IBTickType.BID_OPTION:
                    record.BidImpliedVol = impliedVol;
                    record.BidDelta = delta;
                    break;
                case IBTickType.ASK_OPTION:
                    record.AskImpliedVol = impliedVol;
                    record.AskDelta = delta;
                    break;
                case IBTickType.LAST_OPTION:
                    record.ImpliedVol = impliedVol;
                    record.Delta = delta;
                    break;
                case IBTickType.MODEL_OPTION:
                    record.ImpliedVol = impliedVol;
                    record.Delta = delta;
                    record.PVDividend = pvDividend;
                    record.ModelPrice = modelPrice;
                    break;
                default:
                    throw new ArgumentException("Unknown tick type - " + tickType);
            }

            OnMarketData(record, tickType);
        }

        private void OnTickEFP(int reqId, IBTickType tickType, double basisPoints, string formattedBasisPoints, 
                               double impliedFuturesPrice, int holdDays, string futureExpiry, 
                               double dividendImpact, double dividendsToExpiry)
        {
            if (TickEFP != null)
                TickEFP(this, new TWSTickEFPEventArgs(this, reqId, tickType, basisPoints, formattedBasisPoints,
                                                       impliedFuturesPrice, holdDays, futureExpiry,
                                                       dividendImpact, dividendsToExpiry));

        }
        private void OnTickString(int reqId, IBTickType tickType, string value)
        {
            if (TickString != null)
                TickString(this, new TWSTickStringEventArgs(this, reqId, tickType, value));

        }
        private void OnCurrentTime(long time)
        {
            if (CurrentTime != null)
                CurrentTime(this, new TWSCurrentTimeEventArgs(this, time));

        }
        private void OnTickGeneric(int reqId, IBTickType tickType, double value)
        {
            if (TickGeneric != null)
                TickGeneric(this, new TWSTickGenericEventArgs(this, reqId, tickType, value));


            TWSMarketDataSnapshot record;
            if (!_marketDataRecords.TryGetValue(reqId, out record)) {
                OnError(String.Format("OnTickPrice: Unknown request id {0}", reqId));
                return;
            }

            bool tickRecognized = false;
            switch (tickType) {
                case IBTickType.LAST_TIMESTAMP:
                    record.LastTimeStamp = DateTime.FromFileTime((long) value);
                    tickRecognized = true;
                    break;
            }

            if (tickRecognized)
                OnMarketData(record, tickType);
        }

        protected void OnOrderStatus(int orderID, string status, int filled, int remaining, 
                                     double avgFillPrice, int permID, int parentID, 
                                     double lastFillPrice, int clientID, string whyHeld)
        {
            if (OrderStatus != null)
                OrderStatus(this, new TWSOrderStatusEventArgs(this, orderID, status, filled, remaining, 
                                                              avgFillPrice, permID, parentID, lastFillPrice,
                                                              clientID, whyHeld));
        }

        protected void OnOpenOrder(int orderId, IBOrder order, IBContract contract)
        {
            if (OpenOrder != null)
                OpenOrder(this, new TWSOpenOrderEventArgs(this, orderId, order, contract));

        }

        protected void OnBondContractDetails(IBContractDetails contract)
        {
            if (BondContractDetails != null)
                BondContractDetails(this, new TWSContractDetailsEventArgs(this, contract));
        }
        protected void OnContractDetails(IBContractDetails contract)
        {
            if (ContractDetails != null)
                ContractDetails(this, new TWSContractDetailsEventArgs(this, contract));
        }
        protected void OnManagedAccounts(string accountList)
        { }
        protected void OnReceiveFA(int faDataType, string xml)
        { }
        protected void OnScannerData(int reqId, int rank, IBContractDetails contract, 
                                     string distance, string benchmark, string projection)
        { }
        protected void OnScannerParameters(string xml)
        { }
        protected void OnUpdateAccountTime(string timestamp)
        { }
        protected void OnUpdateAccountValue(string key, string val, string cur, string accountName)
        { }
        protected void OnUpdateNewsBulletin(int newsMsgId, int newsMsgType, string newsMessage, string originatingExch)
        { }
        protected void OnUpdatePortfolio(IBContract contract, int position, double marketPrice, double marketValue, double averageCost, double unrealizedPNL, double realizedPNL, string accountName)
        {
            if (UpdatePortfolio != null)
                UpdatePortfolio(this, new TWSUpdatePortfolioEventArgs(this, contract, position, marketPrice, marketValue, averageCost, unrealizedPNL, realizedPNL, accountName));
        }

        protected void OnExecDetails(int orderId, IBContract contract, IBExecution execution)
        {
            if (ExecDetails != null)
                ExecDetails(this, new TWSExecDetailsEventArgs(this, orderId, contract, execution));
        }

        protected void OnMarketDepth(int reqId, int position, IBOperation operation, IBSide side, double price, int size)
        {
            if (MarketDepth != null)
                MarketDepth(this, new TWSMarketDepthEventArgs(this, reqId, position, String.Empty, operation, side, price, size));
        }
        protected void OnMarketDepthL2(int reqId, int position, string marketMaker, IBOperation operation, IBSide side, double price, int size)
        {
            if (MarketDepthL2 != null)
                MarketDepthL2(this, new TWSMarketDepthEventArgs(this, reqId, position, marketMaker, operation, side, price, size));

        }
        protected void OnHistoricalData(int reqId, TWSHistoricState state, DateTime date, double open, double high, double low, double close, int volume, double WAP, bool hasGaps)
        {
            if (HistoricalData != null)
                HistoricalData(this, new TWSHistoricalDataEventArgs(this, reqId, state, date, open, high, low, close, volume, WAP, hasGaps));
        }
        #endregion

        #region Synchronized Request Wrappers

        public IBContractDetails GetContractDetails(IBContract contract)
        {
            AutoResetEvent are = new AutoResetEvent(false);
            _internalDetailRequests.Add(contract, new KeyValuePair<AutoResetEvent, IBContractDetails>(are, null));
            RequestContractDetails(contract);            
            AutoResetEvent.WaitAny(new WaitHandle[] { are }, DEFAULT_WAIT_TIMEOUT, false);
            IBContractDetails ret = _internalDetailRequests[contract].Value;
            _internalDetailRequests.Remove(contract);
            return ret;
        }
        #endregion

        #region Raw Server Mesage Processing
        private void ProcessTickPrice()
        {
            int version = _enc.DecodeInt();
            int reqId = _enc.DecodeInt();
            IBTickType tickType = (IBTickType) _enc.DecodeInt();
            double price = _enc.DecodeDouble();
            int size = (version >= 2) ? _enc.DecodeInt() : 0;
            int canAutoExecute = (version >= 3) ? _enc.DecodeInt() : 0;
            OnTickPrice(reqId, tickType, price, size, canAutoExecute);

            // Contorary to standard IB socket implementation
            // I will no go on with the supitidy of simulating TickSize
            // events when this client library is obviously written
            // to support the combined tick price + size messages
        }

        private void ProcessTickSize()
        {
            int version = _enc.DecodeInt();
            int reqId = _enc.DecodeInt();
            IBTickType tickType = (IBTickType) _enc.DecodeInt();
            int size = _enc.DecodeInt();
            OnTickSize(reqId, tickType, size);
        }

        private void ProcessOrderStatus()
        {
            int version = _enc.DecodeInt();
            int orderID = _enc.DecodeInt();
            string status = _enc.DecodeString();
            int filled = _enc.DecodeInt();
            int remaining = _enc.DecodeInt();
            double avgFillPrice = _enc.DecodeDouble();

            int permID = (version >= 2) ? _enc.DecodeInt() : 0;
            int parentID = (version >= 3) ? _enc.DecodeInt() : 0;
            double lastFillPrice = (version >= 4) ? _enc.DecodeDouble() : 0;
            int clientID = (version >= 5) ? _enc.DecodeInt() : 0;
            string whyHeld = (version >= 6) ? _enc.DecodeString() : null;
            OnOrderStatus(orderID, status, filled, remaining, avgFillPrice, permID, parentID, lastFillPrice, clientID, whyHeld);
        }

        private void ProcessErrMsg()
        {
            int version = _enc.DecodeInt();
            if (version < 2) {
                string message = _enc.DecodeString();
                OnError(message);
            }
            else {
                int id = _enc.DecodeInt();
                int errorCode = _enc.DecodeInt();
                string message = _enc.DecodeString();
                OnError(id, new TWSError(errorCode, message));
            }
        }

        private void ProcessOpenOrder()
        {
            IBOrder order = new IBOrder();

            // read version
            int version = _enc.DecodeInt();
            // read order id
            order.OrderId = _enc.DecodeInt();

            // read contract fields
            IBContract contract = new IBContract();
            contract.Symbol = _enc.DecodeString();
            contract.SecType = _enc.DecodeSecType();
            contract.Expiry = DateTime.ParseExact(_enc.DecodeString(), IB_EXPIRY_DATE_FORMAT, CultureInfo.InvariantCulture);
            contract.Strike = _enc.DecodeDouble();
            contract.Right = _enc.DecodeString();
            contract.Exchange = _enc.DecodeString();
            contract.Currency = _enc.DecodeString();
            contract.LocalSymbol = (version >= 2) ? _enc.DecodeString() : null;

            // read other order fields
            order.Action = _enc.DecodeAction();
            order.TotalQuantity = _enc.DecodeInt();
            order.OrderType = _enc.DecodeOrderType();
            order.LmtPrice = _enc.DecodeDouble();
            order.AuxPrice = _enc.DecodeDouble();
            order.Tif = _enc.DecodeTif();
            order.OcaGroup = _enc.DecodeString();
            order.Account = _enc.DecodeString();
            order.OpenClose = _enc.DecodeString();
            order.Origin = _enc.DecodeInt();
            order.OrderRef = _enc.DecodeString();

            if (version >= 3)
                order.ClientId = _enc.DecodeInt();

            if (version >= 4) {
                order.PermId = _enc.DecodeInt();
                order.IgnoreRth = _enc.DecodeInt() == 1;
                order.Hidden = _enc.DecodeInt() == 1;
                order.DiscretionaryAmt = _enc.DecodeDouble();
            }

            if (version >= 5)
                order.GoodAfterTime = _enc.DecodeString();

            if (version >= 6)
                order.SharesAllocation = _enc.DecodeString();

            if (version >= 7) {
                order.FaGroup = _enc.DecodeString();
                order.FaMethod = _enc.DecodeString();
                order.FaPercentage = _enc.DecodeString();
                order.FaProfile = _enc.DecodeString();
            }

            if (version >= 8)
                order.GoodTillDate = _enc.DecodeString();

            if (version >= 9) {
                order.Rule80A = _enc.DecodeString();
                order.PercentOffset = _enc.DecodeDouble();
                order.SettlingFirm = _enc.DecodeString();
                order.ShortSaleSlot = _enc.DecodeInt();
                order.DesignatedLocation = _enc.DecodeString();
                order.AuctionStrategy = _enc.DecodeInt();
                order.StartingPrice = _enc.DecodeDouble();
                order.StockRefPrice = _enc.DecodeDouble();
                order.Delta = _enc.DecodeDouble();
                order.StockRangeLower = _enc.DecodeDouble();
                order.StockRangeUpper = _enc.DecodeDouble();
                order.DisplaySize = _enc.DecodeInt();
                order.RthOnly = _enc.DecodeBool();
                order.BlockOrder = _enc.DecodeBool();
                order.SweepToFill = _enc.DecodeBool();
                order.AllOrNone = _enc.DecodeBool();
                order.MinQty = _enc.DecodeInt();
                order.OcaType = _enc.DecodeInt();
                order.ETradeOnly = _enc.DecodeBool();
                order.FirmQuoteOnly = _enc.DecodeBool();
                order.NbboPriceCap = _enc.DecodeDouble();
            }

            if (version >= 10) {
                order.ParentId = _enc.DecodeInt();
                order.TriggerMethod = _enc.DecodeInt();
            }

            if (version >= 11) {
                order.Volatility = _enc.DecodeDouble();
                order.VolatilityType = _enc.DecodeInt();
                if (version == 11) {
                    int receivedInt = _enc.DecodeInt();
                    order.DeltaNeutralOrderType = ((receivedInt == 0) ? IBOrderType.NONE : IBOrderType.MKT);
                }
                else {
                    // version 12 and up
                    order.DeltaNeutralOrderType = _enc.DecodeOrderType();
                    order.DeltaNeutralAuxPrice = _enc.DecodeDouble();
                }
                order.ContinuousUpdate = _enc.DecodeInt();
                if (ServerInfo.Version == 26) {
                    order.StockRangeLower = _enc.DecodeDouble();
                    order.StockRangeUpper = _enc.DecodeDouble();
                }
                order.ReferencePriceType = _enc.DecodeInt();
            }

            if (version >= 13)
                order.TrailStopPrice = _enc.DecodeDouble();

            if (version >= 14)
            {
                order.BasisPoints = _enc.DecodeDouble();
                order.BasisPointsType = _enc.DecodeInt();
                contract.ComboLegsDescrip = _enc.DecodeString();
            }

            OnOpenOrder(order.OrderId, order, contract);

        }

        private void ProcessAcctValue()
        {
            int version = _enc.DecodeInt();
            string key = _enc.DecodeString();
            string val = _enc.DecodeString();
            string cur = _enc.DecodeString();
            string accountName = null;
            accountName = (version >= 2) ? _enc.DecodeString() : null;
            OnUpdateAccountValue(key, val, cur, accountName);

        }
        private void ProcessPortfolioValue()
        {
            int version = _enc.DecodeInt();
            IBContract contractDetails = new IBContract();
            contractDetails.Symbol = _enc.DecodeString();
            contractDetails.SecType = _enc.DecodeSecType();
            contractDetails.Expiry = DateTime.ParseExact(_enc.DecodeString(), IB_EXPIRY_DATE_FORMAT, CultureInfo.InvariantCulture);
            contractDetails.Strike = _enc.DecodeDouble();
            contractDetails.Right = _enc.DecodeString();
            contractDetails.Currency = _enc.DecodeString();
            if (version >= 2)
                contractDetails.LocalSymbol = _enc.DecodeString();

            int position = _enc.DecodeInt();
            double marketPrice = _enc.DecodeDouble();
            double marketValue = _enc.DecodeDouble();
            double averageCost = 0.0;
            double unrealizedPNL = 0.0;
            double realizedPNL = 0.0;
            if (version >= 3) {
                averageCost = _enc.DecodeDouble();
                unrealizedPNL = _enc.DecodeDouble();
                realizedPNL = _enc.DecodeDouble();
            }

            string accountName = null;
            if (version >= 4)
                accountName = _enc.DecodeString();
            OnUpdatePortfolio(contractDetails, position, marketPrice, marketValue, averageCost, unrealizedPNL, realizedPNL, accountName);
        }
        private void ProcessAcctUpdateTime()
        {
            int version = _enc.DecodeInt();
            string timeStamp = _enc.DecodeString();
            OnUpdateAccountTime(timeStamp);
        }
        private void ProcessNextValidID()
        {
            int version = _enc.DecodeInt();
            _nextValidId = _enc.DecodeInt();
        }
        private void ProcessContractData()
        {
            int version = _enc.DecodeInt();
            IBContractDetails contractDetails = new IBContractDetails();
            contractDetails.Summary.Symbol = _enc.DecodeString();
            contractDetails.Summary.SecType = _enc.DecodeSecType();
            contractDetails.Summary.Expiry = DateTime.ParseExact(_enc.DecodeString(), IB_EXPIRY_DATE_FORMAT, CultureInfo.InvariantCulture);
            contractDetails.Summary.Strike = _enc.DecodeDouble();
            contractDetails.Summary.Right = _enc.DecodeString();
            contractDetails.Summary.Exchange = _enc.DecodeString();
            contractDetails.Summary.Currency = _enc.DecodeString();
            contractDetails.Summary.LocalSymbol = _enc.DecodeString();
            contractDetails.MarketName = _enc.DecodeString();
            contractDetails.TradingClass = _enc.DecodeString();
            contractDetails.Conid = _enc.DecodeInt();
            contractDetails.MinTick = _enc.DecodeDouble();
            contractDetails.Multiplier = _enc.DecodeString();
            contractDetails.OrderTypes = _enc.DecodeString();
            contractDetails.ValidExchanges = _enc.DecodeString();
            if (version >= 2)
                contractDetails.PriceMagnifier = _enc.DecodeInt();
            OnContractDetails(contractDetails);
        }
        private void ProcessExecutionData()
        {
            int version = _enc.DecodeInt();
            int orderId = _enc.DecodeInt();
            IBContract contract = new IBContract();
            contract.Symbol = _enc.DecodeString();
            contract.SecType = _enc.DecodeSecType();
            contract.Expiry = DateTime.ParseExact(_enc.DecodeString(), IB_EXPIRY_DATE_FORMAT, CultureInfo.InvariantCulture);
            contract.Strike = _enc.DecodeDouble();
            contract.Right = _enc.DecodeString();
            contract.Exchange = _enc.DecodeString();
            contract.Currency = _enc.DecodeString();
            contract.LocalSymbol = _enc.DecodeString();
            IBExecution execution = new IBExecution();
            execution.OrderID = orderId;
            execution.ExecID = _enc.DecodeString();
            execution.Time = _enc.DecodeString();
            execution.AcctNumber = _enc.DecodeString();
            execution.Exchange = _enc.DecodeString();
            execution.Side = _enc.DecodeString();
            execution.Shares = _enc.DecodeInt();
            execution.Price = _enc.DecodeDouble();
            if (version >= 2)
                execution.PermID = _enc.DecodeInt();
            if (version >= 3)
                execution.ClientID = _enc.DecodeInt();
            if (version >= 4)
                execution.Liquidation = _enc.DecodeInt();

            OnExecDetails(orderId, contract, execution);
        }

        private void ProcessMarketDepth()
        {
            int version = _enc.DecodeInt();
            int id = _enc.DecodeInt();
            int position = _enc.DecodeInt();
            IBOperation operation = (IBOperation) _enc.DecodeInt();
            IBSide side = (IBSide) _enc.DecodeInt();
            double price = _enc.DecodeDouble();
            int size = _enc.DecodeInt();
            OnMarketDepth(id, position, operation, side, price, size);
        }
        private void ProcessMarketDepthL2()
        {
            int version = _enc.DecodeInt();
            int id = _enc.DecodeInt();
            int position = _enc.DecodeInt();
            string marketMaker = _enc.DecodeString();
            IBOperation operation = (IBOperation) _enc.DecodeInt();
            IBSide side = (IBSide) _enc.DecodeInt();
            double price = _enc.DecodeDouble();
            int size = _enc.DecodeInt();
            OnMarketDepthL2(id, position, marketMaker, operation, side, price, size);
        }
        private void ProcessNewsBulletins()
        {
            int version = _enc.DecodeInt();
            int newsMsgId = _enc.DecodeInt();
            int newsMsgType = _enc.DecodeInt();
            string newsMessage = _enc.DecodeString();
            string originatingExch = _enc.DecodeString();
            OnUpdateNewsBulletin(newsMsgId, newsMsgType, newsMessage, originatingExch);
        }
        private void ProcessManagedAccts()
        {
            int version = _enc.DecodeInt();
            string accountsList = _enc.DecodeString();

            OnManagedAccounts(accountsList);
        }
        private void ProcessReceiveFA()
        {
            int version = _enc.DecodeInt();
            int faDataType = _enc.DecodeInt();
            System.String xml = _enc.DecodeString();

            OnReceiveFA(faDataType, xml);
        }
        private void ProcessHistoricalData()
        {
            int version = _enc.DecodeInt();
            int reqId = _enc.DecodeInt();
            string startDateStr;
            string endDateStr;
            DateTime startDateTime = DateTime.Now;
            DateTime endDateTime = startDateTime;
            if (version >= 2) {
                startDateStr = _enc.DecodeString();
                endDateStr = _enc.DecodeString();
                startDateTime = DateTime.ParseExact(startDateStr, IB_DATE_FORMAT, CultureInfo.InvariantCulture);
                endDateTime = DateTime.ParseExact(endDateStr, IB_DATE_FORMAT, CultureInfo.InvariantCulture); 
            }
            OnHistoricalData(reqId, TWSHistoricState.Starting, startDateTime, -1, -1, -1, -1, -1, -1, false);
            int itemCount = _enc.DecodeInt();
            for (int i = 0; i < itemCount; i++) {
                string date = _enc.DecodeString();
                DateTime dateTime = DateTime.ParseExact(date, IB_DATE_FORMAT, CultureInfo.InvariantCulture);
                double open = _enc.DecodeDouble();
                double high = _enc.DecodeDouble();
                double low = _enc.DecodeDouble();
                double close = _enc.DecodeDouble();
                int volume = _enc.DecodeInt();
                double WAP = _enc.DecodeDouble();
                string hasGaps = _enc.DecodeString();
                OnHistoricalData(reqId, TWSHistoricState.Downloading, dateTime, open, high, low, close, volume, WAP, System.Boolean.Parse(hasGaps));
            }
            // Send end of dataset marker
            OnHistoricalData(reqId, TWSHistoricState.Finished, endDateTime, -1, -1, -1, -1, -1, -1, false);
        }
        private void ProcessBondContractData()
        {
            int version = _enc.DecodeInt();
            IBContractDetails contract = new IBContractDetails();

            contract.Summary.Symbol = _enc.DecodeString();
            contract.Summary.SecType = _enc.DecodeSecType();
            contract.Summary.Cusip = _enc.DecodeString();
            contract.Summary.Coupon = _enc.DecodeDouble();
            contract.Summary.Maturity = _enc.DecodeString();
            contract.Summary.IssueDate = _enc.DecodeString();
            contract.Summary.Ratings = _enc.DecodeString();
            contract.Summary.BondType = _enc.DecodeString();
            contract.Summary.CouponType = _enc.DecodeString();
            contract.Summary.Convertible = _enc.DecodeBool();
            contract.Summary.Callable = _enc.DecodeBool();
            contract.Summary.Putable = _enc.DecodeBool();
            contract.Summary.DescAppend = _enc.DecodeString();
            contract.Summary.Exchange = _enc.DecodeString();
            contract.Summary.Currency = _enc.DecodeString();
            contract.MarketName = _enc.DecodeString();
            contract.TradingClass = _enc.DecodeString();
            contract.Conid = _enc.DecodeInt();
            contract.MinTick = _enc.DecodeDouble();
            contract.OrderTypes = _enc.DecodeString();
            contract.ValidExchanges = _enc.DecodeString();

            if (version >= 2) {
                contract.Summary.NextOptionDate = _enc.DecodeString();
                contract.Summary.NextOptionType = _enc.DecodeString();
                contract.Summary.NextOptionPartial = _enc.DecodeBool();
                contract.Summary.Notes = _enc.DecodeString();
            }
            OnBondContractDetails(contract);
        }

        private void ProcessScannerParameters()
        {
            int version = _enc.DecodeInt();
            string xml = _enc.DecodeString();
            //_ibtws.OnScannerParameters(xml);
        }
        private void ProcessScannerData()
        {
            IBContractDetails contract = new IBContractDetails();
            int version = _enc.DecodeInt();
            int reqId = _enc.DecodeInt();
            int numberOfElements = _enc.DecodeInt();
            for (int i = 0; i < numberOfElements; i++) {
                int rank = _enc.DecodeInt();
                contract.Summary.Symbol = _enc.DecodeString();
                contract.Summary.SecType = _enc.DecodeSecType();
                contract.Summary.Expiry = DateTime.ParseExact(_enc.DecodeString(), IB_EXPIRY_DATE_FORMAT, CultureInfo.InvariantCulture);
                contract.Summary.Strike = _enc.DecodeDouble();
                contract.Summary.Right = _enc.DecodeString();
                contract.Summary.Exchange = _enc.DecodeString();
                contract.Summary.Currency = _enc.DecodeString();
                contract.Summary.LocalSymbol = _enc.DecodeString();
                contract.MarketName = _enc.DecodeString();
                contract.TradingClass = _enc.DecodeString();
                string distance = _enc.DecodeString();
                string benchmark = _enc.DecodeString();
                string projection = _enc.DecodeString();
                //_ibtws.OnScannerData(reqId, rank, contract, distance, benchmark, projection);
            }

        }
        private void ProcessTickOptionComputation()
        {
            int version = _enc.DecodeInt();
            int reqId = _enc.DecodeInt();
            IBTickType tickType = (IBTickType) _enc.DecodeInt();
            double impliedVol = _enc.DecodeDouble();
            // -1 is the "not yet computed" indicator
            if (impliedVol < 0)
                impliedVol = System.Double.MaxValue;
            double delta = _enc.DecodeDouble();
            // -2 is the "not yet computed" indicator
            if (System.Math.Abs(delta) > 1)
                delta = System.Double.MaxValue;
            double modelPrice, pvDividend;
            // introduced in version == 5
            if (tickType == IBTickType.MODEL_OPTION) {
                modelPrice = _enc.DecodeDouble();
                pvDividend = _enc.DecodeDouble();
            }
            else
                modelPrice = pvDividend = System.Double.MaxValue;

            OnTickOptionComputation(reqId, tickType, impliedVol, delta, modelPrice, pvDividend);
        }

        private void ProcessTickEFP()
        {
            int version = _enc.DecodeInt();
            int reqId = _enc.DecodeInt();
            IBTickType tickType = (IBTickType) _enc.DecodeInt();
            double basisPoints = _enc.DecodeDouble();
            string formattedBasisPoints = _enc.DecodeString();
            double impliedFuturesPrice = _enc.DecodeDouble();
            int holdDays = _enc.DecodeInt();
            string futureExpiry = _enc.DecodeString();
            double dividendImpact = _enc.DecodeDouble();
            double dividendsToExpiry = _enc.DecodeDouble();
            OnTickEFP(reqId, tickType, basisPoints, formattedBasisPoints,
                      impliedFuturesPrice, holdDays, futureExpiry, dividendImpact, dividendsToExpiry);
        }


        private void ProcessTickString()
        {
            int version = _enc.DecodeInt();
            int reqId = _enc.DecodeInt();
            IBTickType tickType = (IBTickType) _enc.DecodeInt();
            String value = _enc.DecodeString();

            OnTickString(reqId, tickType, value);
        }

        private void ProcessTickGeneric()
        {
            int version = _enc.DecodeInt();
            int reqId = _enc.DecodeInt();
            IBTickType tickType = (IBTickType) _enc.DecodeInt();
            double value = _enc.DecodeDouble();
            OnTickGeneric(reqId, tickType, value);
        }

        private void ProcessCurrentTime()
        {            
            int version = _enc.DecodeInt();
            long time = _enc.DecodeLong();
            OnCurrentTime(time);
        }

        private void ProcessRealTimeBars()
        {
            int version = _enc.DecodeInt();
            int reqId = _enc.DecodeInt();
            long time = _enc.DecodeLong();
            double open = _enc.DecodeDouble();
            double high = _enc.DecodeDouble();
            double low = _enc.DecodeDouble();
            double close = _enc.DecodeDouble();
            long volume = _enc.DecodeLong();
            double wap = _enc.DecodeDouble();
            int count = _enc.DecodeInt();
            //_ibtws.OnRealtimeBar(reqId, time, open, high, low, close, volume, wap, count);
        }

        private void ProcessMessages()
        {
            try {
                while (_doWork) {
                    if (!ProcessSingleMessage())
                        return;
                }
            }
            catch (Exception e) {
                OnError(e.ToString());
                return;
            }
            finally {
                Disconnect();
            }
        }

        protected bool ProcessSingleMessage()
        {
            Messages.Client msgCode = _enc.DecodeClientMessage();

            // Can't process this
            if (msgCode == Messages.Client.UNKNOWN)
                return false;

            switch (msgCode) {
                case Messages.Client.TICK_PRICE: ProcessTickPrice(); break;
                case Messages.Client.TICK_SIZE: ProcessTickSize(); break;
                case Messages.Client.TICK_OPTION_COMPUTATION: ProcessTickOptionComputation(); break;
                case Messages.Client.TICK_GENERIC: ProcessTickGeneric(); break;
                case Messages.Client.TICK_STRING: ProcessTickString(); break;
                case Messages.Client.TICK_EFP: ProcessTickEFP(); break;
                case Messages.Client.ORDER_STATUS: ProcessOrderStatus(); break;
                case Messages.Client.ERR_MSG: ProcessErrMsg(); break;
                case Messages.Client.OPEN_ORDER: ProcessOpenOrder(); break;
                case Messages.Client.ACCT_VALUE: ProcessAcctValue(); break;
                case Messages.Client.PORTFOLIO_VALUE: ProcessPortfolioValue(); break;
                case Messages.Client.ACCT_UPDATE_TIME: ProcessAcctUpdateTime(); break;
                case Messages.Client.NEXT_VALID_ID: ProcessNextValidID(); break;
                case Messages.Client.CONTRACT_DATA: ProcessContractData(); break;
                case Messages.Client.EXECUTION_DATA: ProcessExecutionData(); break;
                case Messages.Client.MARKET_DEPTH: ProcessMarketDepth(); break;
                case Messages.Client.MARKET_DEPTH_L2: ProcessMarketDepthL2(); break;
                case Messages.Client.NEWS_BULLETINS: ProcessNewsBulletins(); break;
                case Messages.Client.MANAGED_ACCTS: ProcessManagedAccts(); break;
                case Messages.Client.RECEIVE_FA: ProcessReceiveFA(); break;
                case Messages.Client.HISTORICAL_DATA: ProcessHistoricalData(); break;
                case Messages.Client.BOND_CONTRACT_DATA: ProcessBondContractData(); break;
                case Messages.Client.SCANNER_PARAMETERS: ProcessScannerParameters(); break;
                case Messages.Client.SCANNER_DATA: ProcessScannerData(); break;
                case Messages.Client.CURRENT_TIME: ProcessCurrentTime(); break;
                case Messages.Client.REAL_TIME_BARS: ProcessRealTimeBars(); break;
                default:
                    OnError("Unknown message id - " + msgCode.ToString());
                    break;
            }

            // All is well
            return true;
        }
        #endregion

        #region Request Data Methods
        public virtual int RequestPlaceOrder(IBContract contract, IBOrder order)
        {
            lock (this) {
                if (!IsConnected) {
                    OnError(TWSErrors.NOT_CONNECTED);
                    return -1;
                }

                int reqVersion = 20;
                int orderId = NextValidId;
                try {
                    _enc.Encode(Messages.Server.PLACE_ORDER);
                    _enc.Encode(reqVersion);
                    _enc.Encode(orderId);
                    _enc.Encode(contract.Symbol);
                    _enc.Encode(contract.SecType.ToString());
                    _enc.Encode(contract.Expiry.ToString(IB_EXPIRY_DATE_FORMAT));
                    _enc.Encode(contract.Strike);
                    _enc.Encode(contract.Right);
                    if (ServerInfo.Version >= 15)
                        _enc.Encode(contract.Multiplier);
                    _enc.Encode(contract.Exchange);
                    if (ServerInfo.Version >= 14)
                        _enc.Encode(contract.PrimaryExch);
                    _enc.Encode(contract.Currency);
                    if (ServerInfo.Version >= 2)
                        _enc.Encode(contract.LocalSymbol);
                    _enc.Encode(order.Action);
                    _enc.Encode(order.TotalQuantity);
                    _enc.Encode(order.OrderType);
                    _enc.Encode(order.LmtPrice);
                    _enc.Encode(order.AuxPrice);
                    _enc.Encode(order.Tif);
                    _enc.Encode(order.OcaGroup);
                    _enc.Encode(order.Account);
                    _enc.Encode(order.OpenClose);
                    _enc.Encode(order.Origin);
                    _enc.Encode(order.OrderRef);
                    _enc.Encode(order.Transmit);
                    if (ServerInfo.Version >= 4)
                        _enc.Encode(order.ParentId);
                    if (ServerInfo.Version >= 5) {
                        _enc.Encode(order.BlockOrder);
                        _enc.Encode(order.SweepToFill);
                        _enc.Encode(order.DisplaySize);
                        _enc.Encode(order.TriggerMethod);
                        _enc.Encode(order.IgnoreRth);
                    }
                    if (ServerInfo.Version >= 7)
                        _enc.Encode(order.Hidden);
                    if ((ServerInfo.Version >= 8) && (contract.SecType == IBSecType.BAG)) {
                        _enc.Encode(contract.ComboLegs.Count);
                        foreach (IBComboLeg leg in contract.ComboLegs) {
                            _enc.Encode(leg.ConId);
                            _enc.Encode(leg.Ratio);
                            _enc.Encode(leg.Action);
                            _enc.Encode(leg.Exchange);
                            _enc.Encode(leg.OpenClose);
                        }
                    }
                    if (ServerInfo.Version >= 9)
                        _enc.Encode(order.SharesAllocation);
                    if (ServerInfo.Version >= 10)
                        _enc.Encode(order.DiscretionaryAmt);
                    if (ServerInfo.Version >= 11)
                        _enc.Encode(order.GoodAfterTime);
                    if (ServerInfo.Version >= 12)
                        _enc.Encode(order.GoodTillDate);
                    if (ServerInfo.Version >= 13) {
                        _enc.Encode(order.FaGroup);
                        _enc.Encode(order.FaMethod);
                        _enc.Encode(order.FaPercentage);
                        _enc.Encode(order.FaProfile);
                    }
                    if (ServerInfo.Version >= 18) {
                        _enc.Encode(order.ShortSaleSlot);
                        _enc.Encode(order.DesignatedLocation);
                    }
                    if (ServerInfo.Version >= 19) {
                        _enc.Encode(order.OcaType);
                        _enc.Encode(order.RthOnly);
                        _enc.Encode(order.Rule80A);
                        _enc.Encode(order.SettlingFirm);
                        _enc.Encode(order.AllOrNone);
                        _enc.EncodeMax(order.MinQty);
                        _enc.EncodeMax(order.PercentOffset);
                        _enc.Encode(order.ETradeOnly);
                        _enc.Encode(order.FirmQuoteOnly);
                        _enc.EncodeMax(order.NbboPriceCap);
                        _enc.EncodeMax(order.AuctionStrategy);
                        _enc.EncodeMax(order.StartingPrice);
                        _enc.EncodeMax(order.StockRefPrice);
                        _enc.EncodeMax(order.Delta);
                        double stockRangeLower = ((ServerInfo.Version == 26) && (order.OrderType == IBOrderType.VOL)) ? 
                            Double.MaxValue : order.StockRangeLower;
                        double stockRangeUpper = ((ServerInfo.Version == 26) && (order.OrderType == IBOrderType.VOL)) ? 
                            Double.MaxValue : order.StockRangeUpper;
                        this._enc.EncodeMax(stockRangeLower);
                        this._enc.EncodeMax(stockRangeUpper);
                        if (ServerInfo.Version >= 22) {
                            _enc.Encode(order.OverridePercentageConstraints);
                        }
                        if (ServerInfo.Version >= 26) {
                            _enc.EncodeMax(order.Volatility);
                            _enc.EncodeMax(order.VolatilityType);
                            if (ServerInfo.Version < 28) {
                                _enc.Encode(order.DeltaNeutralOrderType == IBOrderType.MKT);
                            }
                            else {
                                _enc.Encode(order.DeltaNeutralOrderType);
                                this._enc.EncodeMax(order.DeltaNeutralAuxPrice);
                            }
                            this._enc.Encode(order.ContinuousUpdate);
                            if (ServerInfo.Version == 26) {
                                if (order.OrderType == IBOrderType.VOL) {
                                    _enc.EncodeMax(order.StockRangeLower);
                                    _enc.EncodeMax(order.StockRangeUpper);
                                }
                                else {
                                    _enc.EncodeMax(Double.MaxValue);
                                    _enc.EncodeMax(Double.MaxValue);
                                }
                            }
                            _enc.EncodeMax(order.ReferencePriceType);
                        }
                    }

                }
                catch (Exception e) {
                    OnError(TWSErrors.FAIL_SEND_ORDER);
                    OnError(e.Message);
                    orderId = -1;
                    Disconnect();
                }

                _orderRecords.Add(orderId, new OrderRecord(order, contract));
                return orderId;
            }
        }

        public virtual void RequestExerciseOptions(int reqId, IBContract contract,
                                                   int exerciseAction, int exerciseQuantity,
                                                   string account, int overrideOrder)
        {
            // not connected?
            if (!IsConnected) {
                OnError(TWSErrors.NOT_CONNECTED);
                return;
            }

            int reqVersion = 1;

            try {
                if (ServerInfo.Version < 21) {
                    OnError(TWSErrors.UPDATE_TWS);
                    return;
                }

                _enc.Encode(Messages.Server.EXERCISE_OPTIONS);
                _enc.Encode(reqVersion);
                _enc.Encode(reqId);
                _enc.Encode(contract.Symbol);
                _enc.Encode(contract.SecType.ToString());
                _enc.Encode(contract.Expiry.ToString(IB_DATE_FORMAT));
                _enc.Encode(contract.Strike);
                _enc.Encode(contract.Right);
                _enc.Encode(contract.Multiplier);
                _enc.Encode(contract.Exchange);
                _enc.Encode(contract.Currency);
                _enc.Encode(contract.LocalSymbol);
                _enc.Encode(exerciseAction);
                _enc.Encode(exerciseQuantity);
                _enc.Encode(account);
                _enc.Encode(overrideOrder);
            }
            catch (Exception e) {
                OnError(reqId, TWSErrors.FAIL_SEND_REQMKT);
                OnError(e.Message);
                Disconnect();
            }
        }


        public virtual void RequestServerLogLevelChange(int logLevel)
        {
            // not connected?
            if (!IsConnected) {
                OnError(TWSErrors.NOT_CONNECTED);
                return;
            }

            int reqVersion = 1;

            // send the set server logging level message
            try {
                _enc.Encode(Messages.Server.SET_SERVER_LOGLEVEL);
                _enc.Encode(reqVersion);
                _enc.Encode(logLevel);
            }
            catch (Exception e) {
                OnError(TWSErrors.FAIL_SEND_SERVER_LOG_LEVEL);
                OnError(e.Message);
                Disconnect();
            }
        }

        public virtual int RequestHistoricalData(IBContract contract, string endDateTime, 
                                                 string durationStr, int barSizeSetting, 
                                                 string whatToShow, int useRTH, int formatDate)
        {
            lock (this) {
                // not connected?
                if (!IsConnected) {
                    OnError(TWSErrors.NOT_CONNECTED);
                    return -1;
                }

                int reqVersion = 3;
                int reqID = NextValidId;

                try {
                    if (ServerInfo.Version < 16) {
                        OnError(TWSErrors.UPDATE_TWS);
                        return -1;
                    }

                    _enc.Encode(Messages.Server.REQ_HISTORICAL_DATA);
                    _enc.Encode(reqVersion);
                    _enc.Encode(reqID);
                    _enc.Encode(contract.Symbol);
                    _enc.Encode(contract.SecType);
                    _enc.Encode(contract.Expiry.ToString(IB_EXPIRY_DATE_FORMAT));
                    _enc.Encode(contract.Strike);
                    _enc.Encode(contract.Right);
                    _enc.Encode(contract.Multiplier);
                    _enc.Encode(contract.Exchange);
                    _enc.Encode(contract.PrimaryExch);
                    _enc.Encode(contract.Currency);
                    _enc.Encode(contract.LocalSymbol);
                    if (ServerInfo.Version >= 20) {
                        _enc.Encode(endDateTime);
                        _enc.Encode(barSizeSetting);
                    }
                    _enc.Encode(durationStr);
                    _enc.Encode(useRTH);
                    _enc.Encode(whatToShow);
                    if (ServerInfo.Version > 16) {
                        _enc.Encode(formatDate);
                    }
                    if (IBSecType.BAG == contract.SecType) {
                        if (contract.ComboLegs == null) {
                            _enc.Encode(0);
                        }
                        else {
                            _enc.Encode(contract.ComboLegs.Count);

                            IBComboLeg comboLeg;
                            for (int i = 0; i < contract.ComboLegs.Count; i++) {
                                comboLeg = contract.ComboLegs[i];
                                _enc.Encode(comboLeg.ConId);
                                _enc.Encode(comboLeg.Ratio);
                                _enc.Encode(comboLeg.Action);
                                _enc.Encode(comboLeg.Exchange);
                            }
                        }
                    }
                }
                catch (Exception e) {
                    OnError(reqID, TWSErrors.FAIL_SEND_REQHISTDATA);
                    OnError(e.Message);
                    Disconnect();
                }
                return reqID;
            }
        }

        public virtual int RequestMarketData(IBContract contract, IList<IBGenericTickType> genericTickList)
        {
            lock (this) {
                if (!IsConnected) {
                    OnError(TWSErrors.NOT_CONNECTED);
                    return -1;
                }
                int reqVersion = 5;
                int reqID = NextValidId;
                Debug.WriteLine(String.Format("REQ: {0}", reqID));
                try {
                    _enc.Encode(Messages.Server.REQ_MKT_DATA);
                    _enc.Encode(reqVersion);
                    _enc.Encode(reqID);
                    _enc.Encode(contract.Symbol);
                    _enc.Encode(contract.SecType);
                    _enc.Encode(contract.Expiry.ToString(IB_EXPIRY_DATE_FORMAT));
                    _enc.Encode(contract.Strike);
                    _enc.Encode(contract.Right);
                    if (ServerInfo.Version >= 15)
                        _enc.Encode(contract.Multiplier);
                    _enc.Encode(contract.Exchange);
                    if (ServerInfo.Version >= 14)
                        _enc.Encode(contract.PrimaryExch);
                    _enc.Encode(contract.Currency);
                    if (ServerInfo.Version >= 2)
                        _enc.Encode(contract.LocalSymbol);
                    if (ServerInfo.Version >= 8 && (contract.SecType == IBSecType.BAG)) {
                        if (contract.ComboLegs == null)
                            _enc.Encode(0);
                        else {
                            _enc.Encode(contract.ComboLegs.Count);
                            foreach (IBComboLeg leg in contract.ComboLegs) {
                                _enc.Encode(leg.ConId);
                                _enc.Encode(leg.Ratio);
                                _enc.Encode(leg.Action);
                                _enc.Encode(leg.Exchange);
                            }
                        }
                    }
                    if (ServerInfo.Version >= 31) {
                        var sb = new StringBuilder();
                        if (genericTickList != null) {
                            foreach (IBGenericTickType tick in genericTickList)
                                sb.Append((int)tick).Append(',');
                            sb.Remove(sb.Length - 2, 1);
                        }
                        _enc.Encode(sb.ToString());
                    }

                    // If we got to here without choking on something
                    // we update the request registry
                    _marketDataRecords.Add(reqID, new TWSMarketDataSnapshot(contract));
                }
                catch (Exception e) {
                    OnError(TWSErrors.FAIL_SEND_REQMKT);
                    OnError(e.Message);
                    reqID = -1;
                    Disconnect();
                }
                return reqID;
            }
        }

        public virtual void RequestRealTimeBars(int reqId, IBContract contract,
                                                int barSize, string whatToShow, bool useRTH)
        {
            // not connected?
            if (!IsConnected)
            {
                OnError(TWSErrors.NOT_CONNECTED);
                return;
            }
            if (ServerInfo.Version < 34)
            {
                OnError(TWSErrors.UPDATE_TWS);
                return;
            }

            int reqVersion = 1;

            try
            {
                // send req mkt data msg
                _enc.Encode(Messages.Server.REQ_REAL_TIME_BARS);
                _enc.Encode(reqVersion);
                _enc.Encode(reqId);

                _enc.Encode(contract.Symbol);
                _enc.Encode(contract.SecType);
                _enc.Encode(contract.Expiry.ToString(IB_EXPIRY_DATE_FORMAT));
                _enc.Encode(contract.Strike);
                _enc.Encode(contract.Right);
                _enc.Encode(contract.Multiplier);
                _enc.Encode(contract.Exchange);
                _enc.Encode(contract.PrimaryExch);
                _enc.Encode(contract.Currency);
                _enc.Encode(contract.LocalSymbol);
                _enc.Encode(barSize);
                _enc.Encode(whatToShow);
                _enc.Encode(useRTH);

            }
            catch (Exception e)
            {
                OnError(reqId, TWSErrors.FAIL_SEND_REQRTBARS);
                OnError(e.Message);
                Disconnect();
            }
        }
        public virtual int RequestMarketDepth(IBContract contract, int numRows)
        {
            lock (this) {
                if (!IsConnected) {
                    OnError(TWSErrors.NOT_CONNECTED);
                    return -1;
                }
                if (ServerInfo.Version < 6) {
                    OnError(TWSErrors.UPDATE_TWS);
                    return -1;
                }
                int reqVersion = 3;
                int reqID = NextValidId;
                try {
                    _enc.Encode(Messages.Server.REQ_MKT_DEPTH);
                    _enc.Encode(reqVersion);
                    _enc.Encode(reqID);
                    _enc.Encode(contract.Symbol);
                    _enc.Encode(contract.SecType);
                    _enc.Encode(contract.Expiry.ToString(IB_EXPIRY_DATE_FORMAT));
                    _enc.Encode(contract.Strike);
                    _enc.Encode(contract.Right);
                    if (ServerInfo.Version >= 15)
                        _enc.Encode(contract.Multiplier);
                    _enc.Encode(contract.Exchange);
                    _enc.Encode(contract.Currency);
                    _enc.Encode(contract.LocalSymbol);
                    if (ServerInfo.Version >= 19)
                        _enc.Encode(numRows);
                }
                catch (Exception e) {
                    OnError(TWSErrors.FAIL_SEND_REQMKTDEPTH);
                    OnError(e.Message);
                    reqID = -1;
                    Disconnect();
                }
                return reqID;
            }
        }

        public virtual void RequestAutoOpenOrders(bool autoBind)
        {
            // not connected?
            if(!IsConnected) {
                OnError(TWSErrors.NOT_CONNECTED);
                return;
            }

            int reqVersion = 1;

            // send req open orders msg
            try {
                _enc.Encode(Messages.Server.REQ_AUTO_OPEN_ORDERS);
                _enc.Encode(reqVersion);
                _enc.Encode(autoBind);
            }
            catch( Exception e) {
                OnError(TWSErrors.FAIL_SEND_OORDER);
                OnError(e.Message);
                Disconnect();            
            }
        }

        public virtual void RequestIds(int numIds)
        {
            // not connected?
            if (!IsConnected)
            {
                OnError(TWSErrors.NOT_CONNECTED);
                return;
            }

            int reqVersion = 1;

            try
            {
                _enc.Encode(Messages.Server.REQ_IDS);
                _enc.Encode(reqVersion);
                _enc.Encode(numIds);
            }
            catch (Exception e)
            {
                OnError(TWSErrors.FAIL_SEND_CORDER);
                OnError(e.Message);
                Disconnect();
            }
        }

        public virtual void RequestOpenOrders() 
        {
            // not connected?
            if(!IsConnected) {
                OnError(TWSErrors.NOT_CONNECTED);
                return;
            }

            int reqVersion = 1;

            // send cancel order msg
            try {
                _enc.Encode(Messages.Server.REQ_OPEN_ORDERS);
                _enc.Encode(reqVersion);
            }
            catch (Exception e) {
                OnError(TWSErrors.FAIL_SEND_OORDER);
                OnError(e.Message);
                Disconnect();
            }
        }

        public virtual void RequestAccountUpdates(bool subscribe, string acctCode) 
        {
            // not connected?
            if (!IsConnected) {
                OnError(TWSErrors.NOT_CONNECTED);
                return;
            }

            int reqVersion = 2;

            // send cancel order msg
            try {
                _enc.Encode(Messages.Server.REQ_ACCOUNT_DATA);
                _enc.Encode(reqVersion);
                _enc.Encode(subscribe);

                // Send the account code. This will only be used for FA clients
                if (ServerInfo.Version >= 9) {
                    _enc.Encode(acctCode);
                }
            }
            catch (Exception e) {
                OnError(TWSErrors.FAIL_SEND_ACCT);
                OnError(e.Message);
                Disconnect();
            }
        }

        public virtual void RequestAllOpenOrders() 
        {
            // not connected?
            if (!IsConnected) {
                OnError(TWSErrors.NOT_CONNECTED);
                return;
            }

            int reqVersion = 1;

            // send req all open orders msg
            try {
                _enc.Encode(Messages.Server.REQ_ALL_OPEN_ORDERS);
                _enc.Encode(reqVersion);
            }
            catch (Exception e) {
                OnError(TWSErrors.FAIL_SEND_OORDER);
                OnError(e.Message);
                Disconnect();
            }
        }

        public virtual void RequestExecutions(IBExecutionFilter filter) 
        {
            // not connected?
            if(!IsConnected) {
                OnError(TWSErrors.NOT_CONNECTED);
                return;
            }

            int reqVersion = 2;

            // send cancel order msg
            try {
                _enc.Encode(Messages.Server.REQ_EXECUTIONS);
                _enc.Encode(reqVersion);

                // Send the execution rpt filter data
                if (ServerInfo.Version >= 9) 
                {
                    _enc.Encode(filter.ClientId);
                    _enc.Encode(filter.AcctCode);

                    // Note that the valid format for m_time is "yyyymmdd-hh:mm:ss"
                    _enc.Encode(filter.Time.ToString(IB_DATE_FORMAT));
                    _enc.Encode(filter.Symbol);
                    _enc.Encode(filter.SecType);
                    _enc.Encode(filter.Exchange);
                    _enc.Encode(filter.Side);
                }
            }
            catch (Exception e) {
                OnError(TWSErrors.FAIL_SEND_EXEC);
                OnError(e.Message);
                Disconnect();
            }
        }

        public virtual void RequestNewsBulletins(bool allMsgs)
        {
            // not connected?
            if (!IsConnected) {
                OnError(TWSErrors.NOT_CONNECTED);
                return;
            }

            int reqVersion = 1;

            try {
                _enc.Encode(Messages.Server.REQ_NEWS_BULLETINS);
                _enc.Encode(reqVersion);
                _enc.Encode(allMsgs);
            }
            catch (Exception e) {
                OnError(TWSErrors.FAIL_SEND_CORDER);
                OnError(e.Message);
                Disconnect();
            }
        }

        public virtual void RequestContractDetails(IBContract contract)
        {
            // not connected?
            if (!IsConnected) {
                OnError(TWSErrors.NOT_CONNECTED);
                return;
            }

            // This feature is only available for versions of TWS >=4
            if (ServerInfo.Version < 4) {
                OnError(TWSErrors.UPDATE_TWS);
                return;
            }

            int reqVersion = 3;

            try {
                // send req mkt data msg
                _enc.Encode(Messages.Server.REQ_CONTRACT_DATA);
                _enc.Encode(reqVersion);

                _enc.Encode(contract.Symbol);
                _enc.Encode(contract.SecType);
                _enc.Encode(contract.Expiry.ToString(IB_EXPIRY_DATE_FORMAT));
                _enc.Encode(contract.Strike);
                _enc.Encode(contract.Right);
                if (ServerInfo.Version >= 15) {
                    _enc.Encode(contract.Multiplier);
                }
                _enc.Encode(contract.Exchange);
                _enc.Encode(contract.Currency);
                _enc.Encode(contract.LocalSymbol);
                if (ServerInfo.Version >= 31) {
                    _enc.Encode(contract.IncludeExpired);
                }
            }
            catch(Exception e) {
                OnError(TWSErrors.FAIL_SEND_REQCONTRACT);
                OnError(e.Message);
                Disconnect();
            }
        }

        public virtual void RequestCurrentTime() 
        {
            // not connected?
            if( !IsConnected) {
                OnError(TWSErrors.NOT_CONNECTED);
                return;
            }

            // This feature is only available for versions of TWS >= 33
            if (ServerInfo.Version < 33) {
                OnError(TWSErrors.UPDATE_TWS);
                return;
            }

            int reqVersion = 1;

            try {
                _enc.Encode(Messages.Server.REQ_CURRENT_TIME );
                _enc.Encode(reqVersion);
            }
            
            catch(Exception e) {
                OnError(TWSErrors.FAIL_SEND_REQCURRTIME);
                OnError(e.Message);
                Disconnect();
            }
        }
        #endregion

        #region Utilities
        private Stream SetupDefaultRecordStream()
        {
            int count = 1;
            Stream s = null;
            while (true)
            {
                try
                {
                    string name = "ib-log-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + count + ".log";
                    s = File.Open(name, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read);
                    break;
                }
                catch (IOException e)
                {
                    count++;
                }
            }
            return s;
        }
        #endregion

        private int NextValidId
        { get { return _nextValidId++; } }

        public bool IsConnected
        {
            get { return Status == TWSClientStatus.Connected; }
        }

        public TWSClientStatus Status
        { get; private set; }

        public bool RecordForPlayback
        {
            get { return _recordForPlayback; }
            set
            {
                if (IsConnected && _recordForPlayback != value)
                    throw new InvalidOperationException("Cannot set the RecordForPlayback property while connected");

                _recordForPlayback = value;
            }
        }

        public Stream RecordStream
        {
            get
            {
                return _recordStream;
            }
            set
            {
                if (IsConnected && _recordStream != value)
                    throw new InvalidOperationException("Cannot set the RecordForPlayback property while connected");

                _recordStream = value;
            }
        }

        public IPEndPoint EndPoint
        {
            get { return _endPoint; }
            set
            {
                if (IsConnected)
                    throw new Exception("Client already connected, cannot set the EndPoint");
                _endPoint = value;
            }
        }

        public TWSClientSettings Settings
        {
            get { return _settings; }
            set
            {
                if (!IsConnected)
                    _settings = value;
            }
        }

        public TWSClientInfo ClientInfo{ get; private set; }
        public TWSServerInfo ServerInfo { get; private set; }
    }
}
