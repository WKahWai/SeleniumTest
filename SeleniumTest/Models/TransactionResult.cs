using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace SeleniumTest.Models
{
    public class TransactionResult
    {
        public int Code { get; set; }

        public string Message { get; set; }

        public object Result { get; set; }

        public static TransactionResult Success(string message, object data)
        {
            return new TransactionResult()
            {
                Code = 600,
                Message = message,
                Result = data
            };
        }

        public static TransactionResult Failed(string message, int code = 500)
        {
            return new TransactionResult()
            {
                Code = code,
                Message = message,
                Result = null
            };
        }
    }
}