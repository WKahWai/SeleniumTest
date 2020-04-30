using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace BankAPI.Exceptions
{
    public class TransferProcessException : Exception
    {
        public int ErrorCode;
        public TransferProcessException(string message,int ErrorCode = 402) : base(message)
        {
            this.ErrorCode = ErrorCode;
        }
    }
}