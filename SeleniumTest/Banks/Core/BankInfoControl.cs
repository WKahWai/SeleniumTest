using SeleniumTest.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace SeleniumTest.Banks.Core
{
    public partial class BankBase
    {
        public static BankInfo GetBankInfoByBank(Bank bank)
        {
            return GetBankInfoList().SingleOrDefault(c => c.Bank == bank.ToString());
        }

        public static List<BankInfo> GetBankInfoList()
        {
            return new List<BankInfo>
            {
                new BankInfo
                {
                    Bank = Bank.BIDVBank.ToString(),
                    ReenterOTP = false,
                    SelectAccount = false,
                    RenewableOtp = true,
                    SupportReselectAccount = true,
                    OTPType = "SMS"
                },
                new BankInfo
                {
                    Bank = Bank.VCBBank.ToString(),
                    ReenterOTP = false,
                    SelectAccount = false,
                    RenewableOtp = false,
                    SupportReselectAccount = true,
                    OTPType = "SMS"
                },
                new BankInfo
                {
                    Bank = Bank.VIBBank.ToString(),
                    ReenterOTP = false,
                    SelectAccount = true,
                    RenewableOtp = false,
                    SupportReselectAccount = true,
                    OTPType = "SMS"
                },
                new BankInfo
                {
                    Bank = Bank.VTBBank.ToString(),
                    ReenterOTP = true,
                    SelectAccount = true,
                    RenewableOtp = false,
                    SupportReselectAccount = false,
                    OTPType = "SMS"
                },

            };
        }
    }
}