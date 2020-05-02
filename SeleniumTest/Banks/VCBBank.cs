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
        private Language language;
        public VCBBank(SocketItem item) : base(item, DriverToUse.Chrome)
        {
#if DEBUG
            bankInfo = GetBankInfoByBank(Bank.VCBBank);
#endif
            if (param.language < 3 && param.language > 0)
            {
                language = (Language)(param.language);
            }
            else
            {
                language = Language.VN;
            }
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

        protected override void SetLanguage()
        {
            //override this and set this is because each bank support differnt type of language so
            //the langauge enum is defined in the class individually therefore override this method will
            //output the acurate langauge result
            lang = new LanguageDecider(language, logger);
        }

        protected override void Login()
        {
            //socket.Clients.Client(socket.ConnectionId).Receive(JsonResponse.success(null, "System start login the bank"));
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
            SwitchLanguage();
            IsWaitingLogin = true;
            var loginListnerResult = LoginListener(() =>
            {
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
                    if ((driver.PageSource.Contains("Quick transfer 24/7 to other banks via account") && driver.PageSource.Contains("Transfer within Vietcombank")) ||
                        (driver.PageSource.Contains("Chuyển tiền trong Vietcombank") && driver.PageSource.Contains("Chuyển tiền nhanh 24/7 tới NH khác qua tài khoản")))
                    {
                        return true;
                    }
                    else if (driver.PageSource.Contains("Incorrect verification code! Please try again.") || driver.PageSource.Contains("Mã kiểm tra không chính xác! Quý khách vui lòng kiểm tra lại!"))
                    {
                        return false;
                    }
                    else
                    {
                        string message = (string)driver.ToChromeDriver().ExecuteScript("return $('.mes_error').text()");
                        if (string.IsNullOrEmpty(message))
                        {
                            LogInfo("Have unhandle error, so the system log the page soruce to debug log");
                            logger.Debug($"Page source for account - {param.AccountID}. {driver.PageSource}");
                        }
                        else
                        {
                            LogInfo($"Error occur during login. {message}");
                        }
                        throw new TransferProcessException(message, 403);
                    }
                })
                {
                    MaxLoop = 3,
                    SleepInterval = 5
                });
                if (condition.HasError || !condition.IsComplete) throw new Exception(condition.Message);
                else return true;
            });
            if (loginListnerResult.HasError) throw new Exception(loginListnerResult.Message);
            else if (!loginListnerResult.IsComplete && !loginListnerResult.ForceStop) throw new TransferProcessException(lang.GetLanguage().LoginTimeout, 410);
            else if (loginListnerResult.ForceStop) throw new Exception("Login have error");
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
            LogInfo($"[{this.GetType().Name}]验证码 : {code}");
            if (code.Length != 5)
            {
                LogInfo($"登录失败.验证码识别失败[{decode.GetCodeName(decode.Dama.f_codeuse)}]-{code}");
                throw new Exception($"登录失败.验证码识别失败[{decode.GetCodeName(decode.Dama.f_codeuse)}]-{code}");
            }
            return code;
        }

        private void SwitchLanguage()
        {
            string type = (string)driver.ToChromeDriver().ExecuteScript("return $('#linkLanguage').attr('language');");
            if (type.ToUpper() == "VI" && language == Language.VN) driver.ToChromeDriver().ExecuteScript("$('#linkLanguage').click();");
            else if (type.ToUpper() == "EN" && language == Language.EN) driver.ToChromeDriver().ExecuteScript("$('#linkLanguage').click();");
        }

        protected override void Logout()
        {
            socket.Clients.Client(socket.ConnectionId).Receive(JsonResponse.success(null, "System logout the bank"));
            try
            {
                ExecuteBasedOnLanguage(() => driver.ToChromeDriver().ExecuteScript("$('.logout-en').children()[0].click();"),
                                       () => driver.ToChromeDriver().ExecuteScript("$('.logout').children()[0].click();"));
                LogInfo($"Logout successful");
            }
            catch (Exception ex)
            {
                LogInfo($"Logout failed");
                LogError("Logout failed", ex);
            }
        }

        private void ExecuteBasedOnLanguage(Action en, Action vn)
        {
            switch (language)
            {
                case Language.VN:
                    vn();
                    break;
                case Language.EN:
                    en();
                    break;
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
                    if (param.OTPType == 1)
                    {
                        ExecuteBasedOnLanguage(() => select.SelectByText("SMS"), () => select.SelectByText("Qua SMS"));
                        var code = GetCode(driver.FindElement(By.Id("captchaImage")).GetAttribute("src"), driver.GetCookies());
                        var captcha = driver.FindElement(By.Id("CaptchaText"));
                        captcha.Clear();
                        captcha.SendKeys(code);
                    }
                    else
                    {
                        ExecuteBasedOnLanguage(() => select.SelectByText("Smart OTP"), () => select.SelectByText("Smart OTP"));
                    }
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
                if (param.OTPType == 2)
                {
                    var secureCode = driver.FindElement(By.Id("ST2_Challenge"))?.Text;
                    if (string.IsNullOrEmpty(secureCode)) throw new Exception("Get smart otp secure code is null");
                    socket.Clients.Client(socket.ConnectionId).Receive(JsonResponse.success(secureCode, "Otp reference success", 209));
                }
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
                    ExecuteBasedOnLanguage(() => driver.FindElement(By.LinkText("Transfer within Vietcombank")).Click(), () => driver.FindElement(By.LinkText("Chuyển tiền trong Vietcombank")).Click());
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
                        if ((balance - param.Amount) < 0) errorMsg = lang.GetLanguage().InsufficientAmount;
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