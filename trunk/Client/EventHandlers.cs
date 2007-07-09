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

namespace IBNet.Client
{
    public class TWSClientEventArgs : EventArgs
    {
        public TWSClientEventArgs(TWSClient client)
        { Client = client; }
        public TWSClient Client { get; private set; }
    }

    public class TWSClientStatusEventArgs : TWSClientEventArgs
    {
        public TWSClientStatusEventArgs(TWSClient client, TWSClientStatus status) : base(client)
        { Status = status; }
        public TWSClientStatus Status { get; private set; }
    }

    public class TWSClientErrorEventArgs : TWSClientEventArgs
    {
        public TWSClientErrorEventArgs(TWSClient client, int tickerId, IBContract contract, TWSError error) : base(client)
        {
            RequestId = tickerId;
            Error = error;
            Contract = contract;
        }
        public IBContract Contract { get; private set; }
        public int RequestId { get; private set; }
        public TWSError Error { get; private set; }
    }

    public class TWSTickPriceEventArgs : TWSClientEventArgs
    {
        public TWSTickPriceEventArgs(TWSClient client, int requestId, IBTickType tickType, 
                                     double price, int size, int canAutoExecute) 
            : base(client)
        {
            RequestId = requestId;
            TickType = tickType;
            Price = price;
            Size = size;
            CanAutoExecute = canAutoExecute;
        }
        public int RequestId { get; private set; }
        public IBTickType TickType  { get; private set; }
        public double Price { get; private set; }
        public int Size { get; private set; }
        public int CanAutoExecute { get; private set; }
    }

    public class TWSTickSizeEventArgs : TWSClientEventArgs
    {
        public TWSTickSizeEventArgs(TWSClient client, int requestId, IBTickType sizeTickType, int size) : base(client)
        {
            RequestId = requestId;
            TickType = sizeTickType;
            Size = size;
        }

        public int RequestId { get; private set; }
        public IBTickType TickType  { get; private set; }
        public int Size { get; private set; }
    }

    public class TWSTickGenericEventArgs : TWSClientEventArgs
    {
        public TWSTickGenericEventArgs(TWSClient client, int requestId, IBTickType tickType, double value)
            : base(client)
        {
            RequestId = requestId;
            TickType = tickType;
            Value = value;
        }

        public int RequestId { get; private set; }
        public IBTickType TickType { get; private set; }
        public double Value { get; private set; }
    }

    public class TWSTickStringEventArgs : TWSClientEventArgs
    {
        public TWSTickStringEventArgs(TWSClient client, int requestId, IBTickType tickType, string value)
            : base(client)
        {
            RequestId = requestId;
            TickType = tickType;
            Value = value;
        }

        public int RequestId { get; private set; }
        public IBTickType TickType { get; private set; }
        public string Value { get; private set; }
    }

    public class TWSTickEFPEventArgs : TWSClientEventArgs
    {
        public TWSTickEFPEventArgs(TWSClient client, int requestId, IBTickType tickType, double basisPoints, 
                                   string formattedBasisPoints, double impliedFuturesPrice, int holdDays, 
                                   string futureExpiry, double dividendImpact, double dividendsToExpiry)
            : base(client)
        {
            RequestId = requestId;
            TickType = tickType;
            BasisPoints = basisPoints;
            FormattedBasisPoints = formattedBasisPoints;
            ImpliedFuturesPrice = impliedFuturesPrice;
            HoldDays = holdDays;
            FutureExpiry = futureExpiry;
            DividendImpact = dividendImpact;
            DividendsToExpiry = dividendsToExpiry;            
        }

        public int RequestId { get; private set; }
        public IBTickType TickType { get; private set; }
        public double BasisPoints { get; private set; }
        public string FormattedBasisPoints { get; private set; }
        public double ImpliedFuturesPrice { get; private set; }
        public int HoldDays { get; private set; }
        public string FutureExpiry { get; private set; }
        public double DividendImpact { get; private set; }
        public double DividendsToExpiry { get; private set; }        
    }


    public class TWSTickOptionComputationEventArgs : TWSClientEventArgs
    {
        public TWSTickOptionComputationEventArgs(TWSClient client, int reqId, IBTickType tickType,
                                                 double impliedVol, double delta, double modelPrice, double pvDividend)
            : base(client)
        {
            RequestId = reqId;
            TickType = tickType;
            ImpliedVol = impliedVol;
            Delta = delta;
            ModelPrice = modelPrice;
            PVDividend = pvDividend;            
        }
        public int RequestId { get; private set; }
        public IBTickType TickType { get; private set; }
        public double ImpliedVol { get; private set; }
        public double Delta { get; private set; }
        public double ModelPrice { get; private set; }
        public double PVDividend { get; private set; }
    }

    public class TWSCurrentTimeEventArgs : TWSClientEventArgs
    {
        public TWSCurrentTimeEventArgs(TWSClient client, long time)
            : base(client)
        {
            Time = DateTime.FromFileTimeUtc(time);
        }

        public DateTime Time { get; private set; }
    }

    public class TWSMarketDataEventArgs : TWSClientEventArgs
    {
        public TWSMarketDataEventArgs(TWSClient client, 
                                      TWSMarketDataSnapshot snapshot, IBTickType tickType)        
            : base(client)
        {
            TickType = tickType;
            Snapshot = snapshot;        
        }
        
        public IBTickType TickType { get; private set; }
        public TWSMarketDataSnapshot Snapshot { get; private set; }        
    }

    public class TWSOrderStatusEventArgs : TWSClientEventArgs
    {
        public TWSOrderStatusEventArgs(TWSClient client, int orderID, string status, int filled, int remaining, double avgFillPrice, 
                                       int permID, int parentID, double lastFillPrice, int clientID, string whyHeld)
            : base(client)
        {
            OrderID = orderID;
            Status = status;
            Filled = filled;
            Remaining = remaining;
            AvgFillPrice = avgFillPrice;
            PermID = permID;
            ParentID = parentID;
            LastFillPrice = lastFillPrice;
            ClientID = clientID;
            WhyHeld = whyHeld;
        }
        public int OrderID { get; private set; }
        public string Status { get; private set; }
        public int Filled { get; private set; }
        public int Remaining { get; private set; }
        public double AvgFillPrice { get; private set; }
        public int PermID { get; private set; }
        public int ParentID { get; private set; }
        public double LastFillPrice { get; private set; }
        public int ClientID { get; private set; }
        public string WhyHeld { get; private set; }
    }

    public class TWSOpenOrderEventArgs : TWSClientEventArgs
    {
        public TWSOpenOrderEventArgs(TWSClient client, int orderId, IBOrder order, IBContract contract)
            : base(client)
        {
            OrderId = orderId;
            Order = order;
            Contract = contract;
        }

        public int OrderId { get; private set; }
        public IBOrder Order { get; private set; }
        public IBContract Contract { get; private set; }
    }

    public class TWSContractDetailsEventArgs : TWSClientEventArgs
    {
        public TWSContractDetailsEventArgs(TWSClient client, IBContractDetails contractDetails) : base(client)
        {
            ContractDetails = contractDetails;
        }

        public IBContractDetails ContractDetails { get; private set; }
    }

    public class TWSUpdatePortfolioEventArgs : TWSClientEventArgs
    {
        public TWSUpdatePortfolioEventArgs(TWSClient client, IBContract contract, int position,
                                           double marketPrice, double marketValue, double averageCost,
                                           double unrealizedPNL, double realizedPNL, string accountName)
            : base(client)
        {
            Contract = contract;
            Position = position;
            MarketPrice = marketPrice;
            MarketValue = marketValue;
            AverageCost = averageCost;
            UnrealizedPnL = unrealizedPNL;
            RealizedPnL = realizedPNL;
            AccountName = accountName;
        }

        public IBContract Contract { get; private set; }
        public int Position { get; private set; }
        public double MarketPrice { get; private set; }
        public double MarketValue { get; private set; }
        public double AverageCost { get; private set; }
        public double UnrealizedPnL { get; private set; }
        public double RealizedPnL { get; private set; }
        public string AccountName { get; private set; }
    }

    public class TWSExecDetailsEventArgs : TWSClientEventArgs
    {
        public TWSExecDetailsEventArgs(TWSClient client, int orderId, IBContract contract, IBExecution execution)
            : base(client)
        {
            OrderId = orderId;
            Contract = contract;
            Execution = execution;
        }
        public int OrderId { get; private set; }
        public IBContract Contract { get; private set; }
        public IBExecution Execution { get; private set; }
    }

    public class TWSMarketDepthEventArgs : TWSClientEventArgs
    {
        public TWSMarketDepthEventArgs(TWSClient client, int requestId, int position, 
                                       string marketMaker, IBOperation operation, 
                                       IBSide side, double price, int size)
            : base(client)
        {
            RequestId = requestId;
            Position = position;
            MarketMaker = marketMaker;
            Operation = operation;
            Side = side;
            Price = price;
            Size = size;
        }
        public int RequestId { get; private set; }
        public int Position { get; private set; }
        public string MarketMaker { get; private set; }
        public IBOperation Operation { get; private set; }
        public IBSide Side { get; private set; }
        public double Price { get; private set; }
        public int Size { get; private set; }
    }

    public class TWSHistoricalDataEventArgs : TWSClientEventArgs
    {
        public TWSHistoricalDataEventArgs(TWSClient client, int tickerId, 
                                          TWSHistoricState state, DateTime date, 
                                          double open, double high, double low, double close,
                                          int volume, double wap, bool hasGaps)
            : base(client)
        {
            TickerId = tickerId;
            State = state;
            Date = date;
            Open = open;
            High = high;
            Low = low;
            Close = close;
            Volume = volume;
            WAP = wap;
            HasGaps = hasGaps;
        }

        public int TickerId { get; private set; }
        public TWSHistoricState State { get; private set; }
        public DateTime Date { get; private set; }
        public double Open { get; private set; }
        public double Low { get; private set; }
        public double High { get; private set; }
        public double Close { get; private set; }
        public int Volume { get; private set; }
        public double WAP { get; private set; }
        public bool HasGaps { get; private set; }
    }
}
