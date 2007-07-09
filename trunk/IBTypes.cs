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

namespace IBNet
{
    public class TWSClientInfo
    {
        // Client version history
        //
        // 	6 = Added parentId to orderStatus
        // 	7 = The new execDetails event returned for an order filled status and reqExecDetails
        //     Also market depth is available.
        // 	8 = Added lastFillPrice to orderStatus() event and permId to execution details
        //  9 = Added 'averageCost', 'unrealizedPNL', and 'unrealizedPNL' to updatePortfolio event
        // 10 = Added 'serverId' to the 'open order' & 'order status' events.
        //      We Send back all the API open orders upon connection.
        //      Added new methods reqAllOpenOrders, reqAutoOpenOrders()
        //      Added FA support - reqExecution has filter.
        //                       - reqAccountUpdates takes acct code.
        // 11 = Added permId to openOrder event.
        // 12 = requsting open order attributes ignoreRth, hidden, and discretionary
        // 13 = added goodAfterTime
        // 14 = always Send size on bid/ask/last tick
        // 15 = Send allocation description string on openOrder
        // 16 = can receive account name in account and portfolio updates, and fa params in openOrder
        // 17 = can receive liquidation field in exec reports, and notAutoAvailable field in mkt data
        // 18 = can receive good till date field in open order messages, and request intraday backfill
        // 19 = can receive rthOnly flag in ORDER_STATUS
        // 20 = expects TWS time string on connection after server version >= 20.
        // 21 = can receive bond contract details.
        // 22 = can receive price magnifier in version 2 contract details message
        // 23 = support for scanner
        // 24 = can receive volatility order parameters in open order messages
        // 25 = can receive HMDS query start and end times
        // 26 = can receive option vols in option market data messages
        // 27 = can receive delta neutral order type and delta neutral aux price in place order version 20: API 8.85
        // 28 = can receive option model computation ticks: API 8.9
        // 29 = can receive trail stop limit price in open order and can place them: API 8.91
        // 30 = can receive extended bond contract def, new ticks, and trade count in bars
        // 31 = can receive EFP extensions to scanner and market data, and combo legs on open orders
        //    ; can receive RT bars 
        // 32 = can receive TickType.LAST_TIMESTAMP
        //    ; can receive "whyHeld" in order status messages 

        private const int CLIENT_VERSION = 32;
        public TWSClientInfo(int version)
        {
            Version = version;
        }
        public TWSClientInfo()
        {
            Version = CLIENT_VERSION;
        }

        public int Version { get; private set; }
    }

    public class TWSServerInfo
    {
        public TWSServerInfo(int version)
        { Version = version; }
        public int Version { get; private set; }
    }

    public class TWSClientId
    {
        public TWSClientId(int id)
        { Id = id; }
        public int Id { get; private set; }
    }


    public class IBExecutionFilter
    {
        public int ClientId { get; set; }
        public string AcctCode { get; set; }
        public DateTime Time { get; set; }
        public string Symbol { get; set; }
        public IBSecType SecType { get; set; }
        public string Exchange { get; set; }
        public string Side { get; set; }

        public IBExecutionFilter()
        {
            ClientId = 0;
        }

        public IBExecutionFilter(int clientId, string acctCode, DateTime time,
                                 string symbol, IBSecType secType, string exchange, string side)
        {
            ClientId = clientId;
            AcctCode = acctCode;
            Time = time;
            Symbol = symbol;
            SecType = secType;
            Exchange = exchange;
            Side = side;
        }
    }

    public class IBComboLeg
    {
        public IBAction Action;
        public int ConId;
        public string Exchange;
        public int OpenClose;
        public int Ratio;

        public IBComboLeg()
        {
            ConId = 0;
            Ratio = 0;
            Exchange = null;
            OpenClose = 0;
        }
    }

    public class IBContract
    {
        public string Symbol { get; set; }
        public IBSecType SecType { get; set; }
        public DateTime Expiry { get; set; }
        public double Strike { get; set; }
        public string Right { get; set; }
        public int Multiplier { get; set; }
        public string Exchange { get; set; }

        public string Currency { get; set; }
        public string LocalSymbol { get; set; }
        // pick an actual (ie non-aggregate) exchange that the contract trades on.  DO NOT SET TO SMART.
        public string PrimaryExch { get; set; }
        public bool IncludeExpired { get; set; }

        // received in open order version 14 and up for all combos
        public String ComboLegsDescrip { get; set; }
        public List<IBComboLeg> ComboLegs { get; set; }


        // BOND values
        public string Cusip { get; set; }
        public string Ratings { get; set; }
        public string DescAppend { get; set; }
        public string BondType { get; set; }
        public string CouponType { get; set; }
        public bool Callable { get; set; }
        public bool Putable { get; set; }
        public double Coupon { get; set; }
        public bool Convertible { get; set; }
        public string Maturity { get; set; }
        public string IssueDate { get; set; }

        public string NextOptionDate { get; set; }
        public string NextOptionType { get; set; }
        public bool NextOptionPartial { get; set; }
        public string Notes { get; set; }

        public IBContract()
        {
            ComboLegs = new List<IBComboLeg>();
        }

        public override bool Equals(object obj)
        {
            if ((obj == null) ||
                !(obj is IBContract))
                return false;


            IBContract other = obj as IBContract;

            if (other.ComboLegs.Count != this.ComboLegs.Count)
                return false;

            return (other.Exchange == Exchange) &&
                   (other.Symbol == Symbol) &&
                   (other.Currency == Currency) &&
                   (other.Expiry == Expiry) &&
                   (other.SecType == SecType) &&
                   (other.Strike == Strike) &&
                   (other.Multiplier == Multiplier);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
            //return Exchange.GetHashCode() +
            //       Symbol.GetHashCode() +
            //       Currency.GetHashCode() +
            //       Expiry.GetHashCode() +
            //       SecType.GetHashCode() +
            //       Strike.GetHashCode() +
            //       Multiplier.GetHashCode();
        }
    }

    public class IBContractDetails
    {
        public IBContract Summary { get; set; }
        public string MarketName { get; set; }
        public string TradingClass { get; set; }
        public int Conid { get; set; }
        public double MinTick { get; set; }
        public string Multiplier { get; set; }
        public int PriceMagnifier { get; set; }
        public string OrderTypes { get; set; }
        public string ValidExchanges { get; set; }

        public IBContractDetails()
        {
            Summary = new IBContract();
            ValidExchanges = OrderTypes = Multiplier = TradingClass = MarketName = null;
            PriceMagnifier = Conid = 0;
            MinTick = 0;
        }
    }

    public class IBExecution
    {
        public string AcctNumber { get; set; }
        public int ClientID { get; set; }
        public string Exchange { get; set; }
        public string ExecID { get; set; }
        public int Liquidation { get; set; }
        public int OrderID { get; set; }
        public int PermID { get; set; }
        public double Price { get; set; }
        public int Shares { get; set; }
        public string Side { get; set; }
        public string Time { get; set; }

        public IBExecution()
        {
            ClientID = OrderID = Shares = Liquidation = 0;
            Price = 0;
            ExecID = Time = AcctNumber = Exchange = Side = null;
        }

    }

    public class IBOrder
    {
        internal const int CUSTOMER = 0;
        internal const int FIRM = 1;
        internal const char OPT_UNKNOWN = '?';
        internal const char OPT_BROKER_DEALER = 'b';
        internal const char OPT_CUSTOMER = 'c';
        internal const char OPT_FIRM = 'f';
        internal const char OPT_ISEMM = 'm';
        internal const char OPT_FARMM = 'n';
        internal const char OPT_SPECIALIST = 'y';
        internal const int AUCTION_MATCH = 1;
        internal const int AUCTION_IMPROVEMENT = 2;
        internal const int AUCTION_TRANSPARENT = 3;
        internal const string EMPTY_STR = "";

        // main order fields
        public int OrderId;
        public int ClientId;
        public int PermId;
        public IBAction Action;
        public int TotalQuantity;
        public IBOrderType OrderType;
        public double LmtPrice;
        public double AuxPrice;

        // extended order fields
        public IBTimeInForce Tif; // "Time in Force" - DAY, GTC, etc.
        public string OcaGroup; // one cancels all group name
        public int OcaType; // 1 = CANCEL_WITH_BLOCK, 2 = REDUCE_WITH_BLOCK, 3 = REDUCE_NON_BLOCK
        public string OrderRef;
        public bool Transmit; // if false, order will be created but not transmited
        public int ParentId; // Parent order Id, to associate Auto STP or TRAIL orders with the original order.
        public bool BlockOrder;
        public bool SweepToFill;
        public int DisplaySize;
        public int TriggerMethod; // 0=Default, 1=Double_Bid_Ask, 2=Last, 3=Double_Last, 4=Bid_Ask, 7=Last_or_Bid_Ask, 8=Mid-point
        public bool IgnoreRth;
        public bool Hidden;
        public string GoodAfterTime; // FORMAT: 20060505 08:00:00 {time zone}
        public string GoodTillDate; // FORMAT: 20060505 08:00:00 {time zone}
        public bool RthOnly;
        public bool OverridePercentageConstraints;
        public string Rule80A; // Individual = 'I', Agency = 'A', AgentOtherMember = 'W', IndividualPTIA = 'J', AgencyPTIA = 'U', AgentOtherMemberPTIA = 'M', IndividualPT = 'K', AgencyPT = 'Y', AgentOtherMemberPT = 'N'
        public bool AllOrNone;
        public int MinQty;
        public double PercentOffset; // REL orders only
        public double TrailStopPrice; // for TRAILLIMIT orders only
        public string SharesAllocation; // deprecated

        // Financial advisors only 
        public string FaGroup;
        public string FaProfile;
        public string FaMethod;
        public string FaPercentage;

        // Institutional orders only
        public string Account;
        public string SettlingFirm;
        public string OpenClose; // O=Open, C=Close
        public int Origin; // 0=Customer, 1=Firm
        public int ShortSaleSlot; // 1 if you hold the shares, 2 if they will be delivered from elsewhere.  Only for Action="SSHORT
        public string DesignatedLocation; // set when slot=2 only.

        // SMART routing only
        public double DiscretionaryAmt;
        public bool ETradeOnly;
        public bool FirmQuoteOnly;
        public double NbboPriceCap;

        // BOX or VOL ORDERS ONLY
        public int AuctionStrategy; // 1=AUCTION_MATCH, 2=AUCTION_IMPROVEMENT, 3=AUCTION_TRANSPARENT

        // BOX ORDERS ONLY
        public double StartingPrice;
        public double StockRefPrice;
        public double Delta;

        // pegged to stock or VOL orders
        public double StockRangeLower;
        public double StockRangeUpper;

        // VOLATILITY ORDERS ONLY
        public double Volatility;
        public int VolatilityType; // 1=daily, 2=annual
        public int ContinuousUpdate;
        public int ReferencePriceType; // 1=Average, 2 = BidOrAsk
        public IBOrderType DeltaNeutralOrderType;
        public double DeltaNeutralAuxPrice;

        // COMBO ORDERS ONLY
        public double BasisPoints;      // EFP orders only
        public int BasisPointsType;  // EFP orders only

        public IBOrder()
        {
            OpenClose = "O";
            Origin = CUSTOMER;
            Transmit = true;
            DesignatedLocation = EMPTY_STR;
            MinQty = System.Int32.MaxValue;
            PercentOffset = System.Double.MaxValue;
            NbboPriceCap = System.Double.MaxValue;
            StartingPrice = System.Double.MaxValue;
            StockRefPrice = System.Double.MaxValue;
            Delta = System.Double.MaxValue;
            StockRangeLower = System.Double.MaxValue;
            StockRangeUpper = System.Double.MaxValue;
            Volatility = System.Double.MaxValue;
            VolatilityType = System.Int32.MaxValue;
            DeltaNeutralAuxPrice = System.Double.MaxValue;
            ReferencePriceType = System.Int32.MaxValue;
            TrailStopPrice = System.Double.MaxValue;
            BasisPoints = System.Int32.MaxValue;
            BasisPointsType = System.Int32.MaxValue;
        }
    }
}