using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using BankAPI.Model;
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
            var condition = StepLooping(new StepLoopOption(() =>
            {
                driver.Url = "https://www.vietcombank.com.vn/IBanking2020/55c3c0a782b739e063efa9d5985e2ab4/Account/Login";
                driver.Navigate();
                return driver.PageSource.ToUpper().Contains("Đổi tên đăng nhập") || driver.PageSource.ToUpper().Contains("Invest like an expert with Vietnam’s leading fund management company");
            })
            {
                MaxLoop = 2,
                SleepInterval = 3
            });
            if (condition.HasError || !condition.IsComplete) throw new Exception("Timeout of getting login page");
            SwitchToEnglish(driver);
        }

        private void SwitchToEnglish(IWebDriver driver)
        {
            string type = (string)driver.ToChromeDriver().ExecuteScript("return $('#linkLanguage').attr('language');");
            if (type.ToUpper() == "VI") driver.ToChromeDriver().ExecuteScript("$('#linkLanguage').click();");
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