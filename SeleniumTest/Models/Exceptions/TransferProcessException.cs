using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace BankAPI.Exceptions
{
    public class TransferProcessException : Exception
    {
        public TransferProcessException(string message) : base("转账过程有误： " + message)
        {

        }
    }


    public class TransferProcessInternalException : Exception
    {
        public TransferProcessInternalException(string message) : base("内部转账过程有误： " + message)
        {

        }
    }
}