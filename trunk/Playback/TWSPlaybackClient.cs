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
using IBNet.Client;
using IBNet.Server;
using System.IO;
using System.Threading;

namespace IBNet.Playback
{
    public enum PlaybackSpeed
    {
        Normal,
        FullSpeedProcessing
    }

    public class TWSPlaybackClient : TWSClient
    {
        private bool _isRunning;
        private PlaybackSpeed _speed;
        private Stream _logStream;
        private long _position;
        private int _numMessages;
        private int _lastMsgSize;
        private DateTime _lastTimeStamp;
        private DateTime _nextTimeStamp;
        private BinaryReader _reader;
        private TWSServerClientState _loopbackServer;
        private bool _doWork;
        private bool _loginCompleted;

        public TWSPlaybackClient()
        {
            _position = 0;
            _numMessages = 0;
            _lastTimeStamp = DateTime.MinValue;
            _nextTimeStamp = DateTime.MinValue;
        }

        public TWSPlaybackClient(string fileName)
        {
            _logStream = new FileStream(fileName, FileMode.Open);
            Init();
        }

        private void Init()
        {
            _reader = new BinaryReader(_logStream);

            // Make sure the playback process doesn't write 
            // anything into the playback stream
            _enc = new NullEncoding(_logStream);

            _loopbackServer = new TWSLoopbackServer(_logStream);
            _loopbackServer.Login += new EventHandler<TWSServerEventArgs>(LoopbackServer_Login);
            _loopbackServer.MarketDataRequest += 
                new EventHandler<TWSMarketDataRequestEventArgs>(LoopbackServer_MarketDataRequest);
            _loopbackServer.MarketDepthCancel += 
                new EventHandler<TWSMarketDataCancelEventArgs>(LoopbackServer_MarketDepthCancel);
        }

        void LoopbackServer_MarketDepthCancel(object sender, TWSMarketDataCancelEventArgs e)
        {
            // Have any market data request sent to the loopback server
            // simulate the end-result of a CancelMarketDataRequest() call...
            _marketDataRecords.Remove(e.ReqId);
        }

        void LoopbackServer_MarketDataRequest(object sender, TWSMarketDataRequestEventArgs e)
        {
            // Have any market data request sent to the loopback server
            // simulate the end-result of a SendMarketDataRequest() call...
            _marketDataRecords.Add(e.ReqId, new TWSMarketDataSnapshot(e.Contract));
        }

        void LoopbackServer_Login(object sender, TWSServerEventArgs e)
        {
            _loginCompleted = true;
        }

        public TWSPlaybackClient(Stream stream)
        {
            _logStream = stream;
            Init();
        }

        public void Reset()
        {
            throw new Exception("The method or operation is not implemented.");
        }

        public void Pause()
        {
            throw new Exception("The method or operation is not implemented.");
        }

        public void Step()
        {
            throw new Exception("The method or operation is not implemented.");
        }

        public PlaybackSpeed Speed
        {
            get
            {
                return _speed;
            }
            set
            {
                _speed = value;
            }
        }

        public bool IsRunning
        {
            get { return _isRunning; }
        }

        private void ProcessPlaybackStream()
        {
            while (_doWork) {
                IBPlaybackMessage msg = ReadLogMetaData();
                switch (msg) {
                    case IBPlaybackMessage.Receive:
                        // Read a single message processing it
                        ProcessSingleMessage();
                        break;
                    case IBPlaybackMessage.Send:
                        // Send the server message to our dummy server,
                        // this server will round-trip the server message and "understand" what
                        // client side message + parameters were used when issueing the server command...
                        _loopbackServer.ProcessSingleMessage();            
                        break;
                }
            }
        }

        public void Start()
        {
            Thread t = new Thread(new ThreadStart(ProcessPlaybackStream));
            _doWork = true;
            t.Start();
        }

        private IBPlaybackMessage ReadLogMetaData()
        {
            IBPlaybackMessage msg = (IBPlaybackMessage) _reader.ReadUInt32();
            _lastTimeStamp = _nextTimeStamp;
            _nextTimeStamp = DateTime.FromBinary(_reader.ReadInt64());
            _lastMsgSize = _reader.ReadInt32();
            _position = _logStream.Position;
            return msg;
        }

        public void Stop()
        {
            _doWork = false;
        }
    }
}
