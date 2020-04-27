using SeleniumTest.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
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
                    ReenterOTP = true,
                    SelectAccount = false,
                    RenewableOtp = true,
                    SupportReselectAccount = true,
                    SupportedOTPType = new string[] {"SMS","SMART_OTP"}
                },
                new BankInfo
                {
                    Bank = Bank.VCBBank.ToString(),
                    ReenterOTP = false,
                    SelectAccount = false,
                    RenewableOtp = false,
                    SupportReselectAccount = true,
                    SupportedOTPType = new string[] {"SMS","SMART_OTP"}
                },
                new BankInfo
                {
                    Bank = Bank.VIBBank.ToString(),
                    ReenterOTP = false,
                    SelectAccount = true,
                    RenewableOtp = false,
                    SupportReselectAccount = true,
                    SupportedOTPType = new string[] {"SMS","SMART_OTP"}
                },
                new BankInfo
                {
                    Bank = Bank.VTBBank.ToString(),
                    ReenterOTP = true,
                    SelectAccount = true,
                    RenewableOtp = false,
                    SupportReselectAccount = false,
                    SupportedOTPType = new string[] {"SMS","SMART_OTP"}
                },

            };
        }

        protected virtual void InputString(string str)
        {
            BankAction.InputString(str, 200);
        }

    }

    public class BankAction
    {
        [DllImport("VHID.dll", EntryPoint = "InputString", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int InputString(string str, uint interval);
    }
}