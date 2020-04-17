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
    public class VTBBank : BankBase
    {
        public VTBBank(SocketItem item) : base(item, DriverToUse.Chrome)
        {
            bankInfo = new BankInfo
            {
                ReenterOTP = true,
                SelectAccount = true,
                RenewableOtp = false
            };
        }

        private void SwitchToEnglish()
        {
            string type = (string)driver.ToChromeDriver().ExecuteScript("return $('html').attr('lang');");
            if (type.ToUpper().Contains("VI"))
                driver.ToChromeDriver().ExecuteScript("$('a[lang=\"en-US\"]')[0].click();");
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
                    driver.ToChromeDriver().ExecuteScript("$('input[placeholder^=\"User Name\"]').val('"+ param.AccountID +"');");
                    driver.ToChromeDriver().ExecuteScript("$('input[placeholder^=\"Password\"]').val('" + param.Password + "');");

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
                        throw new TransferProcessException("登录失败，请确保密码或户名正确");
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

        protected override void Transfer()
        {
            StepLoopResult result = null;
            if (param.IsSameBank)
            {
                driver.FindElement(By.XPath("//*[contains(text(), 'Transfer')]")).Click();
                driver.FindElement(By.CssSelector("a[title^='Internal Transfer']")).Click();

                driver.SwitchTo().Frame(0);

                result = StepLooping(new StepLoopOption((sleep) =>
                {
                    SelectUserAccount();

                    driver.ToChromeDriver().ExecuteScript("$('input[id^=\"toAcct\"]').val('" + param.RecipientAccount + "');");
                    driver.ToChromeDriver().ExecuteScript("$('input[id^=\"amt\"]').val('" + param.Amount.ToString() + "');");
                    driver.FindElement(By.CssSelector("input[id^='amt']")).SendKeys(Keys.Tab);
                    driver.ToChromeDriver().ExecuteScript("$('textarea[id^=\"memo\"]').val('" + param.Remark + "');");

                    driver.FindElement(By.CssSelector("input[id^='btnSubmit']")).Click();

                    string errorMsg = null;
                    try
                    {
                        if (driver.IsElementPresent(By.CssSelector("div[id^='serverErrorMsg']")))
                        {
                            errorMsg = driver.FindElement(By.CssSelector("div[id^='serverErrorMsg']")).Text;
                            throw new TransferProcessException(errorMsg);
                        }
                        else
                        {
                            return true;
                        }

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

                if (result.HasError || !result.IsComplete) throw new Exception($"Transfer exception - {result.Message}");
            }
            else
            {

            }

        }

        private void SelectUserAccount()
        {
            List<IWebElement> accountDropdown = driver.FindElements(By.XPath("//option[not(contains(text(), 'Select'))]")).ToList();
            List<string> accountList = new List<string>();

            accountDropdown.ForEach((item) =>
            {
                driver.FindElement(By.CssSelector("select[id^='frAcct']")).Click();

                var account = item.Text;
                driver.FindElement(By.XPath("//*[contains(text(), '"+ account +"')]")).Click();

                var balance = driver.ToChromeDriver().ExecuteScript("return $(\"input[id^='currBalance']\").val()");

                accountList.Add(account + " - " + balance);
            });

            socket.Clients.Client(socket.ConnectionId).Receive(JsonResponse.success(accountList, "系统正在等待您选择使用的账号", 101));

            string errorMsg = "";
            var result = SelectAccountListener((selectedAccount) =>
            {
                try
                {
                    selectedAccount = Regex.Match(selectedAccount, "\\(VND\\) - (\\d*? )").Groups[0].Value.Trim();
                    driver.FindElement(By.XPath("//*[contains(text(), '" + selectedAccount + "')]")).Click();
                    double balance = double.Parse(driver.ToChromeDriver().ExecuteScript("return $(\"input[id^='currBalance']\").val()").ToString());
                    if ((balance - param.Amount) < 0)
                        errorMsg = "账号余额少于存款金额，选择无效，请重新选择。";
                }
                catch
                {
                    errorMsg = "系统出错，请联系客服提供协助。";
                }
                return errorMsg;
            });

            var test = result;
        }

        protected override void OTP()
        {
            var result = StepLooping(new StepLoopOption((sleep) =>
            {
                ////匹配所有以下的資訊再點擊btnSubmit提交 否則取消交易 特別是在賬號名字不對的時候
                //IWebElement transferAccNoLabel = driver.FindElement(By.XPath("//*[contains(text(), 'Transfer')]"));
                //IWebElement transferAccNo = transferAccNoLabel.FindElement(By.XPath("./..")).FindElements(By.TagName("div"))[1];

                //IWebElement receiverNameLabel = driver.FindElement(By.XPath("//*[contains(text(), 'Receiver')]"));
                //IWebElement receiverName = receiverNameLabel.FindElement(By.XPath("./..")).FindElements(By.TagName("div"))[1];

                //IWebElement receiverAccNoLabel = driver.FindElement(By.XPath("//*[contains(text(), 'Account No')]"));
                //IWebElement receiverAccNo = receiverAccNoLabel.FindElement(By.XPath("./..")).FindElements(By.TagName("div"))[1];

                //// Pass進來時either處理了所有標號or接收沒有標號的數目
                //IWebElement amountLabel = driver.FindElement(By.XPath("//*[contains(text(), 'Amount')]"));
                //IWebElement amount = amountLabel.FindElement(By.XPath("./..")).FindElements(By.TagName("div"))[1];

                //IWebElement remarkLabel = driver.FindElement(By.XPath("//*[contains(text(), 'Content')]"));
                //IWebElement remark = remarkLabel.FindElement(By.XPath("./..")).FindElements(By.TagName("div"))[1];

                driver.FindElement(By.CssSelector("input[id^='btnSubmit']")).Click();

                //var referenceValue = driver.FindElement(By.Id("ST2_SoLenh"));
                return true;
            })
            {
                MaxLoop = 2,
                SleepInterval = 2
            });

            if (result.HasError || !result.IsComplete) throw new Exception("The previous steps have unexpected error occured so cannot proceed to waiting OTP response step");
            socket.Clients.Client(socket.ConnectionId).Receive(JsonResponse.success(null, "系统正在等待您收到的短信验证码，请检查您的手机", 200));
            IsWaitingOTP = true;
            result = OTPListener((otp) =>
            {
                driver.ToChromeDriver().ExecuteScript("$('input[id^=\"otp\"]').val('" + otp + "');");
                driver.FindElement(By.CssSelector("input[id^='btnSubmit']")).Click();

                IWebElement errorBox = null;
                try
                {
                    if (driver.IsElementPresent(By.CssSelector("div[class^='errmsg']")))
                    {
                        errorBox = driver.FindElement(By.CssSelector("div[class^='errmsg']"));
                        socket.Clients.Client(socket.ConnectionId).Receive(JsonResponse.success(null, "OTP錯誤，請稍等再次輸入", 700));
                        driver.FindElement(By.CssSelector("input[id^='btnReset']")).Click();
                        Thread.Sleep(500);
                        driver.FindElement(By.CssSelector("input[id^='btnSubmit']")).Click();
                        Thread.Sleep(500);
                        driver.FindElement(By.CssSelector("input[id^='btnSubmit']")).Click();
                    }
                    else
                    {
                        errorBox = null;
                    }
                }
                catch
                {
                    errorBox = null;
                    driver.FindElement(By.CssSelector("input[id^='btnReset']")).Click();
                    Thread.Sleep(500);
                    driver.FindElement(By.CssSelector("input[id^='btnSubmit']")).Click();
                    Thread.Sleep(500);
                    driver.FindElement(By.CssSelector("input[id^='btnSubmit']")).Click();
                }
                return new Tuple<string, bool>(errorBox?.Text, errorBox == null);
            });
            if (result.HasError) throw new Exception("System have error during process the receive OTP");
            if (!result.IsComplete) throw new TransferProcessException("等待短信验证输入超时");
        }

        protected override void RenewOTP()
        {
            throw new NotImplementedException();
        }

        protected override void CheckTransferStatus()
        {
            //throw new NotImplementedException();
            Thread.Sleep(1000);
            if (driver.PageSource.Contains("Transaction is sucessful") || driver.PageSource.Contains("Transaction is successful") || driver.PageSource.Contains("Giao dịch thành công") || (driver.PageSource.Contains("receipt-number") && driver.PageSource.Contains("breadcrumb-item active last")))
            {
                //if (!IsSameBank)
                //{
                //    DefaultFrame();
                //    ExcuteScript("document.getElementsByTagName('body')[0].style.zoom=0.8;");//缩小屏幕
                //    ExcuteScript("window.scrollTo(0,180)");
                //    var bmp = GetScreenSelenium();
                //    bmp.Save(Application.StartupPath + $"//log//VTB//{payee.Id}.bmp");//跨行，成功保存截图
                //    ExcuteScript("document.getElementsByTagName('body')[0].style.zoom=1");//还原屏幕
                //}
                socket.Clients.Client(socket.ConnectionId).Receive(JsonResponse.success(null, "轉賬成功", 600));
            }
            else
            {
                socket.Clients.Client(socket.ConnectionId).Receive(JsonResponse.failed(null, "轉賬失敗", 700));
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
    }
}