﻿#region Copyright (c) 2007 by Dan Shechter
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
    public enum IBTickType : int
    {
        UNKNOWN = -1,
        BID_SIZE = 0,
        BID = 1,
        ASK = 2,
        ASK_SIZE = 3,
        LAST = 4,
        LAST_SIZE = 5,
        HIGH = 6,
        LOW = 7,
        VOLUME = 8,
        CLOSE = 9,
        BID_OPTION = 10,
        ASK_OPTION = 11,
        LAST_OPTION = 12,
        MODEL_OPTION = 13,
        OPEN         = 14,
        LOW_13_WEEK  = 15,
        HIGH_13_WEEK = 16,
        LOW_26_WEEK  = 17,
        HIGH_26_WEEK = 18,
        LOW_52_WEEK  = 19,
        HIGH_52_WEEK = 20,
        AVG_VOLUME   = 21,
        OPEN_INTEREST = 22,
        OPTION_HISTORICAL_VOL = 23,
        OPTION_IMPLIED_VOL = 24,
        PTION_BID_EXCH = 25,
        OPTION_ASK_EXCH = 26,
        OPTION_CALL_OPEN_INTEREST = 27,
        OPTION_PUT_OPEN_INTEREST = 28,
        OPTION_CALL_VOLUME = 29,
        OPTION_PUT_VOLUME = 30,
        INDEX_FUTURE_PREMIUM = 31,
        BID_EXCH = 32,
        ASK_EXCH = 33,
        AUCTION_VOLUME = 34,
        AUCTION_PRICE = 35,
        AUCTION_IMBALANCE = 36,
        MARK_PRICE = 37,
        BID_EFP_COMPUTATION  = 38,
        ASK_EFP_COMPUTATION  = 39,
        LAST_EFP_COMPUTATION = 40,
        OPEN_EFP_COMPUTATION = 41,
        HIGH_EFP_COMPUTATION = 42,
        LOW_EFP_COMPUTATION = 43,
        CLOSE_EFP_COMPUTATION = 44,
        LAST_TIMESTAMP = 45,
        SHORTABLE = 46
    }

    public enum IBGenericTickType
    {
        OPTION_VOLLUME = 100,
        OPTION_OPEN_INTEREST = 101,
        HISTORICAL_VOLATILITY = 104,
        OPTION_IMPLIED_VOLATILITY = 106,
        INDEX_FUTURE_PREMIUM = 162,
        MISCELLANEOUS_STATSD = 165,
        MARK_PRICE = 221,
        AUCTION_PRICE = 225
    }

    public enum IBSide : int
    {
        ASK = 0,
        BID = 1,
    }

    public enum IBOperation : int
    {
        INSERT = 0,
        UPDATE = 1,
        DELETE = 2
    }

    public enum IBOrderType
    {
        LMT,
        LMTCLS,
        MKT,
        MKTCLS,
        PEGMKT,
        REL,
        NONE,
        STP,
        STPLMT,
        TRAIL,
        VWAP,
        VOL,
    }

    public enum IBSecType
    {
        BAG,
        CASH,
        FUT,
        FOP,
        IND,
        OPT,
        STK
    }

    public enum IBAction
    {
        BUY,
        SELL,
        SSHORT
    }

    public enum IBTimeInForce
    {
        DAY,
        GTC,
        IOC
    }

    internal enum IBPlaybackMessage : uint
    {
        Send = 0xDEADBEAF,
        Receive = 0x12345678,
    }

    public static class Messages
    {
        public enum Client
        {
            UNKNOWN = -1,
            TICK_PRICE = 1,
            TICK_SIZE = 2,
            ORDER_STATUS = 3,
            ERR_MSG = 4,
            OPEN_ORDER = 5,
            ACCT_VALUE = 6,
            PORTFOLIO_VALUE = 7,
            ACCT_UPDATE_TIME = 8,
            NEXT_VALID_ID = 9,
            CONTRACT_DATA = 10,
            EXECUTION_DATA = 11,
            MARKET_DEPTH = 12,
            MARKET_DEPTH_L2 = 13,
            NEWS_BULLETINS = 14,
            MANAGED_ACCTS = 15,
            RECEIVE_FA = 16,
            HISTORICAL_DATA = 17,
            BOND_CONTRACT_DATA = 18,
            SCANNER_PARAMETERS = 19,
            SCANNER_DATA = 20,
            TICK_OPTION_COMPUTATION = 21,
            TICK_GENERIC = 45,
            TICK_STRING = 46,
            TICK_EFP = 47,
            CURRENT_TIME = 49,
            REAL_TIME_BARS = 50,
        }
        public enum Server
        {
            UNKNOWN = -1,
            REQ_MKT_DATA = 1,
            CANCEL_MKT_DATA = 2,
            PLACE_ORDER = 3,
            CANCEL_ORDER = 4,
            REQ_OPEN_ORDERS = 5,
            REQ_ACCOUNT_DATA = 6,
            REQ_EXECUTIONS = 7,
            REQ_IDS = 8,
            REQ_CONTRACT_DATA = 9,
            REQ_MKT_DEPTH = 10,
            CANCEL_MKT_DEPTH = 11,
            REQ_NEWS_BULLETINS = 12,
            CANCEL_NEWS_BULLETINS = 13,
            SET_SERVER_LOGLEVEL = 14,
            REQ_AUTO_OPEN_ORDERS = 15,
            REQ_ALL_OPEN_ORDERS = 16,
            REQ_MANAGED_ACCTS = 17,
            REQ_FA = 18,
            REPLACE_FA = 19,
            REQ_HISTORICAL_DATA = 20,
            EXERCISE_OPTIONS = 21,
            REQ_SCANNER_SUBSCRIPTION = 22,
            CANCEL_SCANNER_SUBSCRIPTION = 23,
            REQ_SCANNER_PARAMETERS = 24,
            CANCEL_HISTORICAL_DATA = 25,
            REQ_CURRENT_TIME = 49,
            REQ_REAL_TIME_BARS = 50,
            CANCEL_REAL_TIME_BARS = 51,

        }
    }

    public enum TWSHistoricState
    {
        Starting,
        Downloading,
        Finished
    }

}
