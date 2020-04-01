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
            socket.Clients.Client(socket.ConnectionId).Receive(JsonResponse.success(null, "System start login the bank"));
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
            socket.Clients.Client(socket.ConnectionId).Receive(JsonResponse.success(null, "System logout the bank"));
            try
            {
                driver.ToChromeDriver().ExecuteScript("$('a[title^=\"Sign Out\"]').click();");
                Thread.Sleep(2000);
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
            var result = StepLooping(new StepLoopOption((sleep) =>
            {
                //匹配所有以下的資訊再點擊btnSubmit提交 否則取消交易 特別是在賬號名字不對的時候

                IWebElement transferAccNoLabel = driver.FindElement(By.XPath("//*[contains(text(), 'Transfer')]"));
                IWebElement transferAccNo = transferAccNoLabel.FindElement(By.XPath("./..")).FindElements(By.TagName("div"))[1];

                IWebElement receiverNameLabel = driver.FindElement(By.XPath("//*[contains(text(), 'Receiver')]"));
                IWebElement receiverName = receiverNameLabel.FindElement(By.XPath("./..")).FindElements(By.TagName("div"))[1];

                IWebElement receiverAccNoLabel = driver.FindElement(By.XPath("//*[contains(text(), 'Account No')]"));
                IWebElement receiverAccNo = receiverAccNoLabel.FindElement(By.XPath("./..")).FindElements(By.TagName("div"))[1];

                // Pass進來時either處理了所有標號or接收沒有標號的數目
                IWebElement amountLabel = driver.FindElement(By.XPath("//*[contains(text(), 'Amount')]"));
                IWebElement amount = amountLabel.FindElement(By.XPath("./..")).FindElements(By.TagName("div"))[1];

                IWebElement remarkLabel = driver.FindElement(By.XPath("//*[contains(text(), 'Content')]"));
                IWebElement remark = remarkLabel.FindElement(By.XPath("./..")).FindElements(By.TagName("div"))[1];

                driver.FindElement(By.CssSelector("input[id^='btnSubmit']")).Click();

                Thread.Sleep(2000);

                var referenceValue = driver.FindElement(By.Id("ST2_SoLenh"));
                return !string.IsNullOrEmpty(referenceValue.Text);
            })
            {
                MaxLoop = 2,
                SleepInterval = 3
            });

            if (result.HasError || !result.IsComplete) throw new Exception("The previous steps have unexpected error occured so cannot proceed to waiting OTP response step");
            socket.Clients.Client(socket.ConnectionId).Receive(JsonResponse.success(null, "系统正在等待您收到的短信验证码，请检查您的手机"));

            result = OTPListener((otp) =>
            {
                driver.FindElement(By.CssSelector("input[id^='otp']")).SendKeys(otp);

                driver.FindElement(By.CssSelector("input[id^='btnSubmit']")).Click();

                if (driver.FindElement(By.CssSelector("div[class^='errmsg']")) == null)
                {

                }
                else
                {
                    driver.FindElement(By.CssSelector("input[id^='btnReset']")).Click();
                    Thread.Sleep(2000);
                    driver.FindElement(By.CssSelector("input[id^='btnSubmit']")).Click();

                }
                return new Tuple<string, bool>(notificationBox?.Text, notificationBox == null);
            });

            if (result.HasError) throw new Exception("System have error during process the receive OTP");
            if (!result.IsComplete) throw new TransferProcessException("等待短信验证输入超时");
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
                driver.FindElement(By.XPath("//*[contains(text(), 'Transfer')]")).Click();
                driver.FindElement(By.CssSelector("a[title^='Internal Transfer']")).Click();

                driver.SwitchTo().Frame(0);

                result = StepLooping(new StepLoopOption((sleep) =>
                {
                    var selectedAccount = "(VND) - 104868097259";
                    var data = SelectUserAccount(selectedAccount);

                    driver.FindElement(By.CssSelector("input[id^='toAcct']")).SendKeys(param.RecipientAccount);
                    driver.FindElement(By.CssSelector("input[id^='amt']")).SendKeys(param.Amount.ToString());
                    driver.FindElement(By.CssSelector("textarea[id^='memo']")).SendKeys(param.Remark);

                    driver.FindElement(By.CssSelector("input[id^='btnSubmit']")).Click();

                    if (driver.FindElement(By.CssSelector("div[id^='serverErrorMsg']")) == null)
                    {
                        return true;
                    }
                    else
                    {
                        driver.FindElement(By.CssSelector("input[id^='btnReset']")).Click();
                        return false;
                    }
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

        private List<string> SelectUserAccount(string selectedAccount)
        {
            List<IWebElement> accountList2 = driver.FindElements(By.XPath("//option[not(contains(text(), 'Select'))]")).ToList();
            List<string> list = new List<string>();

            accountList2.ForEach((item) =>
            {
                driver.FindElement(By.CssSelector("select[id^='frAcct']")).Click();

                var account = item.Text;
                driver.FindElement(By.XPath("//*[contains(text(), '"+ account +"')]")).Click();

                var script = "return $(\"input[id^='currBalance']\").val()";
                var balance = driver.ToChromeDriver().ExecuteScript(script);

                list.Add(account + " - " + balance);
            });

            return list;
        }
    }
}