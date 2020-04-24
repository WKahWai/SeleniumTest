using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web;
using BankAPI.Exceptions;
using BankAPI.Model;
using JZLibraries_Bank.DeCode;
using OpenQA.Selenium;
using SeleniumTest.Banks.Core;
using SeleniumTest.Models;
using OpenQA.Selenium.Support.UI;
using SeleniumTest.SeleniumHelpers;

namespace SeleniumTest.Banks
{
    public class VCBBank : BankBase
    {
        public VCBBank(SocketItem item) : base(item, DriverToUse.Chrome)
        {
            bankInfo = GetBankInfoByBank(Bank.VCBBank);
        }

        protected override void CheckTransferStatus()
        {
            StepLoopResult result = null;
            if (param.IsSameBank)
            {
                Thread.Sleep(3000);
                result = StepLooping(new StepLoopOption((sleep) =>
                 {
                     logger.Debug("TransferOk" + driver.PageSource);
                     sleep();
                     return driver.PageSource.Contains("Transaction successful!") || driver.PageSource.Contains("Giao dịch chuyển khoản thành công! ");
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
            Thread.Sleep(2000);
        }

        protected override void Login()
        {
            socket.Clients.Client(socket.ConnectionId).Receive(JsonResponse.success(null, "System start login the bank"));
            var condition = StepLooping(new StepLoopOption((sleep) =>
            {
                driver.Url = "https://www.vietcombank.com.vn/IBanking2020/55c3c0a782b739e063efa9d5985e2ab4/Account/Login";
                driver.Navigate();
                sleep();
                return driver.PageSource.Contains("Quên mật khẩu?") || driver.PageSource.Contains("Forgot password?");
            })
            {
                MaxLoop = 2,
                SleepInterval = 3
            });
            if (condition.HasError || !condition.IsComplete) throw new Exception("Timeout of getting login page");
            SwitchToEnglish();
            condition = StepLooping(new StepLoopOption((sleep) =>
            {
                var username = driver.FindElement(By.Name("username"));
                username.Clear();
                username.SendKeys(param.AccountID);
                var password = driver.FindElement(By.Name("pass"));
                password.Clear();
                password.SendKeys(param.Password);
                var code = GetCode(driver.FindElement(By.Id("captchaImage")).GetAttribute("src"), driver.GetCookies());
                var cap = driver.FindElement(By.Id("txtcapcha"));
                cap.Clear();
                cap.SendKeys(code);
                driver.FindElement(By.Id("btndangnhap")).Click();
                sleep();
                if (driver.PageSource.Contains("Quick transfer 24/7 to other banks via account") && driver.PageSource.Contains("Transfer within Vietcombank"))
                {
                    return true;
                }
                else if (driver.PageSource.Contains("Incorrect verification code! Please try again."))
                {
                    return false;
                }
                else
                {
                    string message = (string)driver.ToChromeDriver().ExecuteScript("return $('.mes_error').text()");
                    if (string.IsNullOrEmpty(message))
                    {
                        logger.Info("Have unhandle error, so the system log the page soruce to debug log");
                        logger.Debug($"Page source for account - {param.AccountNo}. {driver.PageSource}");
                    }
                    else
                    {
                        logger.Info($"Account [{param.AccountNo}] - Error occur during login. {message}");
                    }
                    throw new TransferProcessException("登录失败，请确保密码或户名正确", 403);
                }
            })
            {
                MaxLoop = 2,
                SleepInterval = 5
            });
            if (condition.HasError || !condition.IsComplete) throw new Exception(condition.Message);
        }

        private string GetCode(string url, CookieContainer cookie)
        {
            var handler = new HttpClientHandler()
            {
                AllowAutoRedirect = true,
                UseCookies = true,
                CookieContainer = cookie
            };
            http = new HttpClient(handler);
            var result = http.GetAsync(url).Result;
            DeCode decode = new DeCode();
            string code = decode.GetCode(5, JZLibraries_Bank.DeCode.CaptchaType.LetterNumber, result.Content.ReadAsByteArrayAsync().Result);
            logger.Info($"[{this.GetType().Name}]验证码 : {code}");
            if (code.Length != 5)
            {
                logger.Info($"[{this.GetType().Name}]登录失败.验证码识别失败[{decode.GetCodeName(decode.Dama.f_codeuse)}]-{code}");
                throw new Exception($"登录失败.验证码识别失败[{decode.GetCodeName(decode.Dama.f_codeuse)}]-{code}");
            }
            return code;
        }

        private void SwitchToEnglish()
        {
            string type = (string)driver.ToChromeDriver().ExecuteScript("return $('#linkLanguage').attr('language');");
            if (type.ToUpper() == "EN") driver.ToChromeDriver().ExecuteScript("$('#linkLanguage').click();");
        }

        protected override void Logout()
        {
            socket.Clients.Client(socket.ConnectionId).Receive(JsonResponse.success(null, "System logout the bank"));
            try
            {
                driver.ToChromeDriver().ExecuteScript("$('.logout-en').children()[0].click();");
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
            if (param.IsSameBank)
            {
                var result = StepLooping(new StepLoopOption((sleep) =>
                {
                    var OTPType = driver.FindElement(By.Id("otpValidType"));
                    var select = new SelectElement(OTPType);
                    select.SelectByText("SMS");
                    var code = GetCode(driver.FindElement(By.Id("captchaImage")).GetAttribute("src"), driver.GetCookies());
                    var captcha = driver.FindElement(By.Id("CaptchaText"));
                    captcha.Clear();
                    captcha.SendKeys(code);
                    driver.FindElement(By.Id("btnSubmitStep2")).Click();
                    sleep();
                    var referenceValue = driver.FindElement(By.Id("ST2_SoLenh"));
                    return !string.IsNullOrEmpty(referenceValue.Text);
                })
                {
                    MaxLoop = 2,
                    SleepInterval = 3
                });

                if (result.HasError || !result.IsComplete) throw new Exception("The previous steps have unexpected error occured so cannot proceed to waiting OTP response step");
                IsWaitingOTP = true;
                result = OTPListener((otp) =>
                {
                    driver.FindElement(By.Id("MaGiaoDich")).SendKeys(otp);
                    driver.ToChromeDriver().ExecuteScript("$('#btnSubmitStep3').click()");
                    Thread.Sleep(800);

                    IWebElement notificationBox = null;
                    try
                    {
                        notificationBox = driver.FindElement(By.ClassName("growl-message"));
                    }
                    catch
                    {
                        notificationBox = null;
                    }
                    return new Tuple<string, bool>(notificationBox?.Text, notificationBox == null);
                }, bankInfo.ReenterOTP, 2);
                if (result.HasError) throw new Exception("System have error during process the receive OTP");
                if (!result.IsComplete) throw new TransferProcessException("等待短信验证输入超时", 406);
            }
        }

        protected override void RenewOTP()
        {
            throw new NotImplementedException();
        }

        protected override void Transfer()
        {
            StepLoopResult result = null;
            if (param.IsSameBank)
            {
                result = StepLooping(new StepLoopOption((sleep) =>
                {
                    driver.FindElement(By.LinkText("Transfer within Vietcombank")).Click();
                    driver.ToChromeDriver().ExecuteScript("$('#HinhThucChuyenTien').val(1)");
                    SelectUserAccount();
                    var account = driver.FindElement(By.Id("SoTaiKhoanNguoiHuong"));
                    account.Clear();
                    account.SendKeys(param.RecipientAccount);
                    var amount = driver.FindElement(By.Id("SoTien"));
                    amount.Clear();
                    amount.SendKeys(param.Amount.ToString());
                    sleep();
                    if (string.IsNullOrEmpty((string)driver.ToChromeDriver().ExecuteScript("return $('#TenNguoiHuongText').val()"))) throw new Exception("Invalid recipient account");
                    var remark = driver.FindElement(By.Id("NoiDungThanhToan"));
                    remark.Clear();
                    remark.SendKeys(param.Remark);
                    driver.FindElement(By.Id("btnxacnhan")).Click();
                    sleep();
                    return driver.FindElement(By.Id("LB_TaiKhoanTrichNo")).Text == param.AccountNo;
                })
                {
                    MaxLoop = 3,
                    SleepInterval = 4
                });

            }
            else
            {

            }
            if (result.HasError || !result.IsComplete) throw new Exception("Transfer Step 1 have error. " + result.Message);
        }

        private void SelectUserAccount()
        {
            if (param.IsSameBank)
            {
                var account = driver.FindElement(By.Id("TaiKhoanTrichNo"));
                var select = new SelectElement(account);
                List<string> accounts = select.AllSelectedOptions.Select(c => $"(VND) - {c.Text} - ").ToList();
                socket.Clients.Client(socket.ConnectionId).Receive(JsonResponse.success(accounts, "系统正在等待您选择使用的账号", 205));
                string errorMsg = "";
                var result = SelectAccountListener((selectedAccount) =>
                {
                    param.AccountNo = Regex.Match(selectedAccount, "\\(VND\\) - (\\d*? )").Groups[1].Value.Trim();
                    if (select.AllSelectedOptions.Count(c => c.Text == param.AccountNo) > 0)
                    {
                        select.SelectByText(param.AccountNo);
                        Thread.Sleep(2000);
                        var balanceTxt = (string)driver.ToChromeDriver().ExecuteScript("return $('#LB_SoDu').text().replace('VND','').replace('','').trim(' ')");
                        double balance = double.Parse(balanceTxt);
                        if ((balance - param.Amount) < 0) errorMsg = "Insufficient amount";
                        return errorMsg;
                    }
                    else
                    {
                        errorMsg = "Your account is not match in the bank account, please make sure your account number is correct and valid";
                    }
                    return errorMsg;
                }, bankInfo.SupportReselectAccount);
                if (!result.IsComplete || result.HasError) throw new Exception(result.Message ?? "System have error occurs during select account");
            }
        }
    }
}