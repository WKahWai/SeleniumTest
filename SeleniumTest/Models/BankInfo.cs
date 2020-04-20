using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace SeleniumTest.Models
{
    public class BankInfo
    {
        public string Bank { get; set; }

        public string OTPType { get; set; }

        public bool RenewableOtp { get; set; }
        public bool SelectAccount { get; set; }
        public bool SupportReselectAccount { get; set; }
        public bool ReenterOTP { get; set; }
    }
}