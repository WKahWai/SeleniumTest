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
using System.Text.RegularExpressions;

namespace SeleniumTest.Banks
{
    public class VIBBank : BankBase
    {
        public VIBBank(SocketItem item) : base(item, DriverToUse.Chrome)
        {
            bankInfo = GetBankInfoByBank(Bank.VIBBank);
        }

        protected override void CheckTransferStatus()
        {
            StepLoopResult result = null;
            if (param.IsSameBank)
            {
                result = StepLooping(new StepLoopOption((sleep) =>
                {
                    logger.Debug("TransferOk" + driver.PageSource);
                    sleep();
                    return driver.PageSource.Contains("Your transfer has been successfully processed") || driver.PageSource.Contains("Bạn đã chuyển tiền thành công");
                })
                {
                    MaxLoop = 15,
                    SleepInterval = 1
                });
            }
            else
            {
                throw new NotImplementedException();
            }

            if (result.HasError || !result.IsComplete) throw new Exception("Transfer failed");
        }

        protected override void Login()
        {
            socket.Clients.Client(socket.ConnectionId).Receive(JsonResponse.success(null, "System start login the bank"));
            var condition = StepLooping(new StepLoopOption((sleep) =>
            {
                driver.Url = "https://ib.vib.com.vn/en-us/Login.aspx";
                driver.Navigate();
                Thread.Sleep(1000);
                return driver.PageSource.Contains("Login to Internet Banking");
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
                    driver.ToChromeDriver().ExecuteScript("$('input[placeholder^=\"Username\"]').val('" + param.AccountID + "');");

                    var passwordInput = driver.FindElement(By.CssSelector("input[placeholder^=\"Password\"]"));
                    passwordInput.Clear();
                    passwordInput.SendKeys(param.Password);

                    driver.FindElement(By.CssSelector("input[value^='Login']")).Click();
                    Thread.Sleep(2000);

                    if (driver.PageSource.Contains("Welcome"))
                    {
                        return true;
                    }
                    else if (driver.PageSource.Contains("Login"))
                    {
                        return false;
                    }
                    else
                    {
                        string message = (string)driver.ToChromeDriver().ExecuteScript("return $('#divMsgErr').text().trim()");
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
            if (condition.HasError && !condition.IsComplete)
            {
                socket.Clients.Client(socket.ConnectionId).Receive(JsonResponse.failed(null, condition.Message));
                throw new Exception(condition.Message);
            }
        }

        protected override void Logout()
        {
            //socket.Clients.Client(socket.ConnectionId).Receive(JsonResponse.success(null, "System logout the bank"));
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
                IWebElement transferAccNoLabel = driver.FindElement(By.XPath("//*[contains(text(), 'From account')]"));
                IWebElement transferAccNo = transferAccNoLabel.FindElement(By.XPath("./../..")).FindElements(By.TagName("td"))[1];

                IWebElement receiverNameLabel = driver.FindElement(By.XPath("//*[contains(text(), 'Available balance')]"));
                IWebElement receiverName = receiverNameLabel.FindElement(By.XPath("./../..")).FindElements(By.TagName("td"))[1];

                IWebElement receiverAccNoLabel = driver.FindElement(By.XPath("//*[contains(text(), 'To account')]"));
                IWebElement receiverAccNo = receiverAccNoLabel.FindElement(By.XPath("./../..")).FindElements(By.TagName("td"))[1];

                // Pass進來時either處理了所有標號or接收沒有標號的數目
                IWebElement amountLabel = driver.FindElement(By.XPath("//*[contains(text(), 'Amount')]"));
                IWebElement amount = amountLabel.FindElement(By.XPath("./../..")).FindElements(By.TagName("td"))[1];

                IWebElement remarkLabel = driver.FindElement(By.XPath("//*[contains(text(), 'Description')]"));
                IWebElement remark = remarkLabel.FindElement(By.XPath("./../..")).FindElements(By.TagName("td"))[1];

                var referenceValue = driver.FindElement(By.Id("divOTP"));
                return referenceValue.Text.Contains("Get security code");
            })
            {
                MaxLoop = 2,
                SleepInterval = 3
            });

            if (result.HasError || !result.IsComplete) throw new Exception("The previous steps have unexpected error occured so cannot proceed to waiting OTP response step");
            socket.Clients.Client(socket.ConnectionId).Receive(JsonResponse.success(null, "系统正在等待您收到的短信验证码，请检查您的手机"));
            IsWaitingOTP = true;
            result = OTPListener((otp) =>
            {
                IWebElement referenceValue2 = driver.FindElement(By.Id("txtotp1"));
                referenceValue2.SendKeys("123456");
                driver.FindElement(By.CssSelector("a[title^='Transfer']")).Click();

                IWebElement errorBox = null;
                try
                {
                    errorBox = driver.FindElement(By.CssSelector("div[id^='pnlError']"));
                }
                catch
                {
                    errorBox = null;
                }
                return new Tuple<string, bool>(errorBox?.Text, errorBox == null);
            }, bankInfo.ReenterOTP);
            if (result.HasError) throw new Exception("System have error during process the receive OTP");
            if (!result.IsComplete) throw new TransferProcessException("等待短信验证输入超时", 406);
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
                driver.FindElement(By.Id("dnn_MENU1_rptRootMenu_lnkRootMenu_3")).Click();
                driver.FindElement(By.Id("dnn_MENU1_rptRootMenu_rptSubMenu1_3_lnkSubMenu_1")).Click();
                Thread.Sleep(1000);

                result = StepLooping(new StepLoopOption((sleep) =>
                {
                    var selectedAccount = "627704061777254";
                    var data = SelectUserAccount(selectedAccount);

                    driver.ToChromeDriver().ExecuteScript("nextto2();"); // Go to 2nd section
                    Thread.Sleep(1000);

                    new SelectElement(driver.FindElement(By.Id("ddlToAcctType"))).SelectByText("VIB Account");
                    Thread.Sleep(1000);

                    IWebElement addNewAccLabel = driver.FindElement(By.XPath("//*[contains(text(), 'Add new account')]"));
                    addNewAccLabel.FindElement(By.XPath("./..")).FindElements(By.TagName("input"))[0].Click();

                    driver.ToChromeDriver().ExecuteScript("$('input[placeholder^=\"Enter new account\"]').val('" + param.RecipientAccount + "');");

                    driver.ToChromeDriver().ExecuteScript("nextto3();"); // Go to 3rd section
                    Thread.Sleep(1000);

                    IWebElement accountNameLabel = driver.FindElement(By.XPath("//span[contains(text(), 'Account name')]"));
                    string accountName = accountNameLabel.FindElement(By.XPath("./../..")).FindElements(By.TagName("td"))[1].Text;

                    driver.ToChromeDriver().ExecuteScript("$('input[id^=\"txtAmout\"]').val('" + param.Amount + "');");
                    driver.ToChromeDriver().ExecuteScript("$('textarea[id^=\"txtDescription\"]').val('" + param.Remark + "');");

                    IWebElement tsfrNowLabel = driver.FindElement(By.XPath("//*[contains(text(), 'Transfer now')]"));
                    tsfrNowLabel.FindElement(By.XPath("./..")).FindElements(By.TagName("input"))[0].Click();

                    driver.FindElement(By.CssSelector("a[title^='Next']")).Click();

                    IWebElement errorBox = null;
                    try
                    {
                        //errorBox = driver.FindElement(By.CssSelector("p[id^='pMessage']"));
                        return true;
                    }
                    catch
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
            List<IWebElement> accountList = driver.FindElements(By.CssSelector("div[class='selectOption']")).ToList();
            List<string> list = new List<string>();

            accountList.ForEach((item) =>
            {
                List<IWebElement> data = item.FindElements(By.XPath(".//*")).ToList();
                if (data.Count > 1)
                {
                    var accountType = Regex.Match(data[0].GetAttribute("innerText").Replace("\r\n", ""), "(\\D*account)").Value.Trim();
                    var accountBalance = Regex.Match(data[0].GetAttribute("innerText").Replace("\r\n", ""), "(\\d*,\\d*) *?VND").Groups[1].Value;
                    var accountNo = data[2].GetAttribute("innerText").Replace("\r\n", "").Trim();

                    list.Add(accountType + " - " + accountNo + " - " + accountBalance + " VND");
                }
            });

            driver.ToChromeDriver().ExecuteScript("$('div.selectOption[val^=\"" + selectedAccount + "\"]')[0].click();");

            var balance = 0;
            if ((balance - param.Amount) < 0) throw new TransferProcessException("Insufficient amount", 405);

            return list;
        }
    }
}