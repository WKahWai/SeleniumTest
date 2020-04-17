using Newtonsoft.Json;
using System;
using JZLibraries_Bank.Common;

namespace SeleniumTest.Models
{
    public class TransferResult
    {
        public string AccountNo { get; set; }

        public string Remark { get; set; }

        public double Amount { get; set; }

        public readonly long ActiveDate;
        public TransferResult(TransferParam param)
        {
            AccountNo = param.AccountNo;
            Remark = param.Remark;
            Amount = param.Amount;
            ActiveDate = (long)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
        }

        public string Encrypt()
        {
            string json = JsonConvert.SerializeObject(this);
            return json.EncryptConnectionString();
        }
    }
}
