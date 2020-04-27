using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Newtonsoft.Json;

namespace SeleniumTest.Models
{
    public class TransferParam
    {
        public string IdenityCardNo { get; set; }

        public string Password { get; set; }

        public string AccountID { get; set; }

        public string AccountNo { get; set; }

        public bool IsSameBank { get; set; }

        public string FromBank { get; set; }

        public string TargetBank { get; set; }

        public bool TransferOK { get; set; }
        public string RecipientAccount { get; set; }
        public string RecipientName { get; set; }

        /// <summary>
        /// 1 = sms 2 = smart otp
        /// </summary>
        public int OTPType { get; set; }

        public string Remark { get; set; }

        public double Amount { get; set; }
        public string ToJson()
        {
            TransferParam param = new TransferParam
            {
                IdenityCardNo = IdenityCardNo,
                Password = "",
                AccountID = AccountID,
                IsSameBank = IsSameBank,
                TransferOK = TransferOK
            };
            return JsonConvert.SerializeObject(param);
        }

        public static TransferParam StrToObject(string data)
        {
            var obj = JsonConvert.DeserializeObject<TransferParam>(data);
            if (obj == null) throw new Exception("参数不正确");
            return obj;
        }

        public Bank GetBankName()
        {
            Bank bank = (Bank)Enum.Parse(typeof(Bank), FromBank);
            return bank;
        }
    }
}