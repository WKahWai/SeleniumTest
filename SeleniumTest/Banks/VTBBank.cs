using BankAPI.Exceptions;
using BankAPI.Model;
using OpenQA.Selenium;
using SeleniumTest.Banks.Core;
using SeleniumTest.Models;
using SeleniumTest.SeleniumHelpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using OpenQA.Selenium.Support.UI;
using System.Threading;

namespace SeleniumTest.Banks
{
    public class VTBBank : BankBase
    {
        public VTBBank(SocketItem item) : base(item, DriverToUse.Chrome)
        {

        }

        protected override void CheckTransferStatus()
        {
            throw new NotImplementedException();
        }

        protected override void Login()
        {
            var condition = StepLooping(new StepLoopOption((sleep) =>
            {
                driver.Url = "https://ebanking.vietinbank.vn/rcas/portal/web/retail/bflogin";
                driver.Navigate();
                SwitchToEnglish();
                driver.SwitchTo().Frame(0);
                sleep();
                return driver.PageSource.Contains("Sign In");
            })
            {
                MaxLoop = 2,
                SleepInterval = 3
            });
            if (condition.HasError || !condition.IsComplete) throw new Exception("Timeout of getting login page");

            condition = StepLooping(new StepLoopOption((sleep) => 
            {
                try
                {
                    var username = driver.FindElement(By.CssSelector("input[placeholder^='User Name']"));
                    username.Clear();
                    username.SendKeys(param.AccountID);

                    var password = driver.FindElement(By.CssSelector("input[placeholder^='Password']"));
                    password.Clear();
                    password.SendKeys(param.Password);

                    driver.FindElement(By.CssSelector("input[value^='Sign In']")).Click();

                    sleep();

                    if (!driver.PageSource.Contains("Sign In") && driver.PageSource.Contains("Dashboard"))
                    {
                        return true;
                    }
                    else if (driver.PageSource.Contains("Unsuccessful logon"))
                    {
                        driver.FindElement(By.CssSelector("input[value^='Ok']")).Click();
                        return true;
                    }
                    else if (driver.PageSource.Contains("Sign In"))
                    {
                        return false;
                    }
                    else
                    {
                        string message = (string)driver.ToChromeDriver().ExecuteScript("return $('.mes_error').text()");
                        if (string.IsNullOrEmpty(message))
                        {
                            logger.Info("Have unhandled error, so the system log the page soruce to debug log");
                            logger.Debug($"Page source for account - {param.AccountNo}. {driver.PageSource}");
                        }
                        else
                        {
                            logger.Info($"Account [{param.AccountNo}] - Error occur during login. {message}");
                        }
                        throw new Exception("Login failed");
                    }
                }
                catch (Exception ex)
                {
                    return false;
                }
            })
            {
                MaxLoop = 2,
                SleepInterval = 3
            });
            if (condition.HasError && !condition.IsComplete) throw new Exception(condition.Message);
        }

        private void SwitchToEnglish()
        {
            string type = (string)driver.ToChromeDriver().ExecuteScript("return $('html').attr('lang');");
            if (type.ToUpper().Contains("VI")) 
                driver.ToChromeDriver().ExecuteScript("$('a[lang=\"en-US\"]')[0].click();");
        }


        protected override void Logout()
        {
            try
            {
                driver.FindElement(By.CssSelector("a[title^='Sign Out']")).Click();
                logger.Info($"Account [{param.AccountNo}] - Logout successful");
            }   
            catch (Exception ex)
            {
                logger.Info($"Account [{param.AccountNo}] - Logout failed");
                logger.Error($"Account [{param.AccountNo}] - {ex.Message}");
            }
        }

        protected override void OTP()
        {
            throw new NotImplementedException();
        }

        protected override void RenewOTP()
        {
            throw new NotImplementedException();
        }

        protected override void Transfer()
        {
            StepLoopResult result = null;
            if (!param.IsSameBank)
            {
                result = StepLooping(new StepLoopOption((sleep) =>
                {
                    driver.FindElement(By.XPath("//*[contains(text(), 'Transfer')]")).Click();
                    driver.FindElement(By.CssSelector("a[title^='Internal Transfer']")).Click();

                    driver.SwitchTo().Frame(0);

                    var selectedAccount = "(VND) - 104868097259";
                    SelectUserAccount(selectedAccount);
               
                    return true; //to change
                })
                {
                    MaxLoop = 3,
                    SleepInterval = 1
                });

            }
            else
            {

            }

        }

        private void SelectUserAccount(string selectedAccount)
        {
            IWebElement accountList = driver.FindElement(By.CssSelector("select[id^='frAcct']"));
            SelectElement selectElement = new SelectElement(accountList);

            for (int i = 0; i < selectElement.Options.Count; i++)
            {
                selectElement.SelectByIndex(i);
                //driver.
            }

            if (selectElement.Options.Count(c => c.Text.Contains(param.AccountNo)) > 0)
            {
                selectElement.SelectByText(selectedAccount);
                Thread.Sleep(2000);
                var balanceTxt = (string)driver.ToChromeDriver().ExecuteScript("return $('#LB_SoDu').text().replace('VND','').replace('','').trim(' ')");
                double balance = double.Parse(balanceTxt);
                if ((balance - param.Amount) < 0) throw new TransferProcessException("Insufficient amount");
            }
            else
            {
                throw new TransferProcessException("Your account is not match in the bank account, please make sure your account number is correct and valid");
            }
        }
    }
}