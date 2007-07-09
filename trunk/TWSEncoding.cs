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
using System.IO;
using System.Globalization;

namespace IBNet
{
    public class TWSEncoding : IBNet.ITWSEncoding
    {
        protected Stream _stream;
        private string NUMBER_DECIMAL_SEPARATOR = null;
        private const string IB_EXPIRY_DATE_FORMAT = "yyyyMMdd";
        
        public TWSEncoding(Stream stream)
        {
            _stream = stream;
            NUMBER_DECIMAL_SEPARATOR = NumberFormatInfo.CurrentInfo.NumberDecimalSeparator;
        }

        #region Encode Wrappers
        public virtual void Encode(Messages.Server msg)
        { Encode((int)msg); }
        public virtual void Encode(Messages.Client msg)
        { Encode((int)msg); }
        public virtual void Encode(TWSClientInfo v)
        { Encode(v.Version); }
        public virtual void Encode(TWSServerInfo v)
        { Encode(v.Version); }
        public virtual void Encode(TWSClientId id)
        { Encode(id.Id); }
        public virtual void Encode(IBTimeInForce tif)
        { Encode(tif.ToString()); }
        public virtual void Encode(IBSecType secType)
        { Encode(secType.ToString()); }
        public virtual void Encode(IBOrderType orderType)
        { Encode(orderType.ToString()); }
        public virtual void Encode(IBAction action)
        { Encode(action.ToString()); }
        public virtual void EncodeExpiryDate(DateTime expiry)
        { Encode(expiry.ToString(IB_EXPIRY_DATE_FORMAT)); }
        public virtual void Encode(bool value)
        { Encode(value ? 1 : 0); }
        public virtual void Encode(double value)
        { Encode(value.ToString().Replace(',', '.')); }
        public virtual void Encode(int value)
        { Encode(value.ToString()); }
        public virtual void EncodeMax(double value)
        {
            if (value == double.MaxValue)
                Encode((string)null);
            else
                Encode(value);
        }
        public virtual void EncodeMax(int value)
        {
            if (value == 0x7fffffff)
                Encode((string)null);
            else
                Encode(value);
        }
        #endregion
        #region Decode Wrappers
        public virtual DateTime DecodeExpiryDate()
        {
            string expiryString = DecodeString();
            if (expiryString != null && expiryString.Length > 0)
                return DateTime.ParseExact(DecodeString(), IB_EXPIRY_DATE_FORMAT, CultureInfo.InvariantCulture);

            return new DateTime();
        }
        public virtual IBSecType DecodeSecType()
        { return (IBSecType)Enum.Parse(typeof(IBSecType), DecodeString()); }
        public virtual IBTimeInForce DecodeTif()
        { return (IBTimeInForce)Enum.Parse(typeof(IBTimeInForce), DecodeString()); }
        public virtual IBAction DecodeAction()
        { return (IBAction)Enum.Parse(typeof(IBAction), DecodeString()); }
        public virtual IBOrderType DecodeOrderType()
        { return (IBOrderType)Enum.Parse(typeof(IBOrderType), DecodeString()); }
        public virtual Messages.Client DecodeClientMessage()
        { return (Messages.Client)DecodeInt(); }
        public virtual Messages.Server DecodeServerMessage()
        { return (Messages.Server) DecodeInt(); }
        public virtual TWSServerInfo DecodeServerInfo()
        { return new TWSServerInfo(DecodeInt()); }
        public virtual TWSClientInfo DecodeClientInfo()
        { return new TWSClientInfo(DecodeInt()); }
        public virtual TWSClientId DecodeClientId()
        { return new TWSClientId(DecodeInt()); }        
        public virtual bool DecodeBool()
        { return (this.DecodeInt() != 0); }
        public virtual double DecodeDouble()
        {
            string txt = DecodeString();
            if (txt == null)
                return 0;
            txt = txt.Replace(".", NUMBER_DECIMAL_SEPARATOR);
            txt = txt.Replace(",", NUMBER_DECIMAL_SEPARATOR);
            return double.Parse(txt);
        }
        public virtual int DecodeInt()
        {
            string txt = DecodeString();
            if (txt != null)
                return Int32.Parse(txt);
            return 0;
        }
        public virtual long DecodeLong()
        {
            string txt = DecodeString();
            if (txt != null)
                return Int32.Parse(txt);
            return 0;
        }
        #endregion
        #region String Encoding/Decoding
        public virtual void Encode(string text)
        {
            if (text != null)
                foreach (char c in text.ToCharArray())
                    _stream.WriteByte((byte)c);
            _stream.WriteByte(0);
            _stream.Flush();
        }

        public virtual string DecodeString()
        {
            StringBuilder sb = new StringBuilder();

            while (true)
            {
                int b = _stream.ReadByte();
                if ((b == 0) || (b == -1))
                    goto decode_string_finished;
                sb.Append((char)b);
            }
        decode_string_finished:
            if (sb.Length != 0)
                return sb.ToString();
            return null;
        }
        #endregion
    }
}
