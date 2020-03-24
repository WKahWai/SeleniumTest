using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using OpenQA.Selenium;
using SeleniumTest.Banks.Core;
using SeleniumTest.Models;
using SeleniumTest.SeleniumHelpers;

namespace SeleniumTest.Banks
{
    public class VCBBank : BankBase
    {
        public VCBBank(TransferParam param) : base(param, DriverToUse.Chrome)
        {

        }

        protected override void CheckTransferStatus(IWebDriver driver)
        {
            throw new NotImplementedException();
        }

        protected override void Login(IWebDriver driver)
        {
            throw new NotImplementedException();
        }

        protected override void Logout(IWebDriver driver)
        {
            //throw new NotImplementedException();
        }

        protected override void OTP(IWebDriver driver)
        {
            throw new NotImplementedException();
        }

        protected override void RenewOTP(IWebDriver driver)
        {
            throw new NotImplementedException();
        }

        protected override void Transfer(IWebDriver driver)
        {
            throw new NotImplementedException();
        }
    }
}