using BankAPI.Exceptions;
using BankAPI.Model;
using JZLibraries_Bank.DeCode;
using OpenQA.Selenium;
using SeleniumTest.Banks.Core;
using SeleniumTest.Models;
using SeleniumTest.SeleniumHelpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web;

namespace SeleniumTest.Banks
{
    public class BIDVBank : BankBase
    {

        private string diness = "";
        private string Script = "";
        private StepLoopResult OTPResult = null;
        private bool IsOTPSubmit = false;

        public BIDVBank(SocketItem item) : base(item, DriverToUse.Chrome)
        {
            bankInfo = GetBankInfoByBank(Bank.BIDVBank);
            string path = AppDomain.CurrentDomain.BaseDirectory + @"\Banks\Scripts\" + GetType().Name + ".js";
            Script = File.ReadAllText(path);
        }

        protected override void CheckTransferStatus()
        {
            Thread.Sleep(3000);
            StepLoopResult result = null;
            if (param.IsSameBank)
            {
                result = StepLooping(new StepLoopOption((sleep) =>
                {
                    logger.Debug("TransferOk" + driver.PageSource);
                    sleep();
                    var time = DateTime.Now.ToString("dd/MM/yyyy");
                    return ((driver.PageSource.Contains("Số tham chiếu") || driver.PageSource.ToLower().Contains("reference number")) && driver.PageSource.Contains("class=\" CONF_OD_SUCCESS_IMG\"") && driver.PageSource.Contains("class=\" CONF_OD_FAILURE_IMG x-hide-display\""));
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
            socket.Clients.Client(socket.ConnectionId).Receive(JsonResponse.success(null, $"[{param.AccountNo}] - 系统开始登陆网银"));
            var result = StepLooping(new StepLoopOption((sleep) =>
            {
                driver.Url = "https://www.bidv.vn:81/iportalweb/iRetail@1";
                driver.Navigate();
                sleep();
                return driver.PageSource.Contains("Đăng nhập<span> BIDV Online</span>") || driver.PageSource.Contains("Login<span> BIDV Online</span>");
            })
            {
                MaxLoop = 3,
                SleepInterval = 8
            });
            if (result.HasError || !result.IsComplete) throw new Exception($"[{param.AccountNo}] - 获取首页失败");
            SwitchToEnglish();
            result = StepLooping(new StepLoopOption((sleep) =>
            {
                Thread.Sleep(3000);
                var userId = driver.FindElement(By.Id("userNo"));
                userId.Clear();
                userId.SendKeys(param.AccountID);
                var pass = driver.FindElement(By.Id("userPin"));
                pass.Clear();
                pass.SendKeys(param.Password);

                string code = GetCode(driver.FindElement(By.Id("captcha")).GetAttribute("src"), driver.GetCookies());
                if (string.IsNullOrEmpty(code)) return false;
                var cap = driver.FindElement(By.Id("cap1"));
                cap.Clear();
                cap.SendKeys(code);
                driver.ToChromeDriver().ExecuteScript("SubmitForm();");
                sleep();
                Regex Logoutregex = new Regex("(<a ?.*><span>(?<logout>Logout)</span>?.*</a>)");
                if (Logoutregex.Match(driver.PageSource).Groups["logout"].Value.ToLower() == "logout") return true;
                else
                {
                    string error = (string)driver.ToChromeDriver().ExecuteScript("return $('#errid1').text();");
                    if (!string.IsNullOrEmpty(error)) throw new TransferProcessException(error, 403);
                }
                return false;
            })
            {
                MaxLoop = 5,
                SleepInterval = 12
            });
            if (result.HasError || !result.IsComplete) throw new Exception($"[{param.AccountNo}] - 登录失败");
            //RetriveDiness();
        }

        private void SwitchToEnglish()
        {
            driver.ToChromeDriver().ExecuteScript("javascript:localization('en_US');");
        }

        //private void RetriveDiness()
        //{
        //    var regex = Regex.Match(driver.PageSource, "\"CSRF_ID_LIST\":\"(.+?)\"");
        //    if (regex.Success)
        //    {
        //        diness = regex.Result("$1");
        //        diness = Regex.Replace(diness, @"(.{32})", "$1-").Trim('-');
        //        string[] dinessList = diness.Split('-');
        //        diness = dinessList[dinessList.Length - 1];
        //        logger.Info($"[{param.AccountNo}] - Retrive diness from page success. Diness Key - {diness}");
        //    }
        //    else
        //    {
        //        logger.Error($"[{param.AccountNo}] - Unable to retrive diness from page");
        //    }
        //}

        private string GetCode(string url, CookieContainer cookie)
        {
            var handler = new HttpClientHandler()
            {
                AllowAutoRedirect = true,
                UseCookies = true,
                CookieContainer = cookie
            };
            http = new HttpClient(handler);
            var result = http.GetAsync(url).Result.Content.ReadAsByteArrayAsync().Result;
            DeCode decode = new DeCode();
            string code = decode.GetCode(5, JZLibraries_Bank.DeCode.CaptchaType.LetterNumber, result);
            logger.Info($"[{this.GetType().Name}]验证码 : {code}");
            if (code.Length != 5)
            {
                logger.Info($"[{this.GetType().Name}]登录失败.验证码识别失败[{decode.GetCodeName(decode.Dama.f_codeuse)}]-{code}");
                throw new Exception($"登录失败.验证码识别失败[{decode.GetCodeName(decode.Dama.f_codeuse)}]-{code}");
            }
            return code;
        }

        protected override void Logout()
        {
            socket.Clients.Client(socket.ConnectionId).Receive(JsonResponse.success(null, $"登出银行"));
            try
            {
                driver.ToChromeDriver().ExecuteScript("iportal.logoutUser();");
                Thread.Sleep(1000);
                Regex regex = new Regex("(<em ?.*><button type=\"button\" id=\"(?<btnId>.*)\" class=\" x-btn-text\">Yes</button></em>)");
                if (!regex.IsMatch(driver.PageSource)) throw new Exception();
                string logoutBtnId = regex.Match(driver.PageSource).Groups["btnId"].Value;
                driver.ToChromeDriver().ExecuteScript($"$('#{logoutBtnId}').click();");
                Thread.Sleep(2000);
                logger.Info($"[{param.AccountNo}] - Logout success");
            }
            catch (Exception ex)
            {
                logger.Error($"[{param.AccountNo}] - Logout failed. {ex.Message}");
            }
        }

        protected override void OTP()
        {
            //socket.Clients.Client(socket.ConnectionId).Receive(JsonResponse.success(null, "系统正在等待您收到的短信验证码，请检查您的手机"));
            IsWaitingOTP = true;
            if (param.IsSameBank)
            {
                string btnId = "";
                while (string.IsNullOrEmpty(btnId))
                {
                    btnId = new Regex("(<button type=\"button\" id=\"(?<id>[a-z0-9\\-]+)\" class=\" x-btn-text\">Submit</button>)").Match(driver.PageSource).Groups["id"].Value;
                    Thread.Sleep(200);
                }

                if (param.OTPType == 2)//smart otp
                {
                    //renew the smart otp to avoid expired too fast
                    var refNo = driver.ToChromeDriver().ExecuteScript("return $(\"input[name='']\")");
                    driver.ToChromeDriver().ExecuteScript("$('button')[22].click()");
                    Thread.Sleep(600);
                    socket.Clients.Client(socket.ConnectionId).Receive(JsonResponse.success(null, "Otp reference success", 209));
                }

                OTPResult = OTPListener((otp) =>
                {
                    var submitBtn = driver.FindElement(By.Id(btnId));
                    var otpInput = driver.FindElement(By.Name("KEY_OTP"));
                    otpInput.Clear();
                    otpInput.SendKeys(Keys.NumberPad0);
                    otpInput.SendKeys(Keys.Backspace);
                    otpInput.SendKeys(otp);
                    Thread.Sleep(1000);
                    submitBtn.Click();
                    //driver.ToChromeDriver().ExecuteScript("$('button')[24].click()");
                    Thread.Sleep(800);
                    IsOTPSubmit = true;
                    string invalidMessage = "OTP entered is incorrect. Please try again";
                    if (driver.PageSource.Contains("Please enter the OTP"))
                    {
                        return new Tuple<string, bool>("Please enter the OTP, OTP cannot be null", !driver.PageSource.Contains("Please enter the OTP"));
                    }
                    logger.Debug(driver.PageSource);
                    return new Tuple<string, bool>(invalidMessage, !driver.PageSource.Contains(invalidMessage));
                }, otpExpiredDuration: 3, SupportReenter: bankInfo.ReenterOTP);
                if (OTPResult.ForceStop) RenewOTP();
                if (!IsOTPSubmit)
                {
                    if (OTPResult.HasError) throw new Exception($"[{param.AccountNo}] - Fail during OTP request. EX :  {OTPResult.Message}");
                    else if (!OTPResult.IsComplete) throw new TransferProcessException("等待短信验证码超时", 406);
                    //todo if need extra logic to verify is stuck in the same page
                }
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        protected override void RenewOTP()
        {
            driver.ToChromeDriver().ExecuteScript("$('button')[22].click()");
            OTP();
        }

        protected override void Transfer()
        {
            var result = StepLooping(new StepLoopOption((sleep) =>
            {
                driver.FindElement(By.XPath("//div[@class='excardwsicon_holder RETAIL_TRANSFERSMaster']//div[@class='excardwsicon']")).Click();
                sleep();
                return driver.PageSource.Contains("Transfer to Accounts");
            })
            {
                SleepInterval = 10,
                MaxLoop = 5
            });
            if (result.HasError || !result.IsComplete) throw new Exception("Cannot get the transfer page");
            driver.ToChromeDriver().ExecuteScript("$('button')[20].click()");
            Thread.Sleep(3000);
            if (param.IsSameBank)
            {
                result = SelectTransferType("1", "Within BIDV");
                if (result.HasError || !result.IsComplete) throw new Exception("Cannot get the transfer type");

                var field = driver.FindElement(By.Name("BENN6_ACCTNO"));
                field.Clear();
                field.SendKeys(param.RecipientAccount);
                driver.ToChromeDriver().ExecuteScript("$('img')[2].click()");
                Thread.Sleep(1000);
                if (driver.PageSource.Contains("The Account Number you have entered is Invalid")) throw new Exception("The Account Number you have entered is Invalid");
                var rname = driver.FindElement(By.Name("BENN4_BENENAME"));
                //if (!string.IsNullOrEmpty(param.RecipientName))
                //{
                //    if (!rname.Text.ToLower().Equals(param.RecipientName.ToLower())) throw new TransferProcessException("The recipient name is not same as the name that you enter");
                //}
                param.RecipientName = rname.Text.ToUpper();
                driver.ToChromeDriver().ExecuteScript("$('button')[20].click()");
                result = StepLooping(new StepLoopOption((sleep) =>
                {
                    SelectUserAccount();
                    var payment = driver.FindElement(By.Name("PAYMENT_AMOUNT"));
                    payment.Clear();
                    payment.SendKeys(param.Amount.ToString());
                    driver.ToChromeDriver().ExecuteScript("$(\"input[name='CHARGES_TAB']\")[0].click()");
                    var remark = driver.FindElement(By.Name("REMARKS_WITHIN_BANK"));
                    remark.Clear();
                    remark.SendKeys(param.Remark);
                    driver.ToChromeDriver().ExecuteScript("$($('button')[22]).click()");
                    sleep();
                    return driver.PageSource.Contains("Review Details") && driver.PageSource.Contains("Provide your Authentication Code to proceed");
                })
                {
                    MaxLoop = 10,
                    SleepInterval = 3
                });

                if (result.HasError || !result.IsComplete) throw new Exception($"[{param.AccountNo}] - Cannot go to OTP page. EX : {result.Message}");
            }
            else
            {
                result = SelectTransferType("1", "Inter");
                if (result.HasError || !result.IsComplete) throw new Exception($"[{param.AccountNo}] - Cannot get the transfer type. EX : {result.Message}");
            }

        }

        private void SelectUserAccount()
        {
            Thread.Sleep(2000);
            driver.ToChromeDriver().ExecuteScript("$('img')[4].click();");
            Thread.Sleep(1000);
            var accounts = driver.ToChromeDriver().ExecuteScript(Script + "return getUserAccounts()");
            socket.Clients.Client(socket.ConnectionId).Receive(JsonResponse.success(accounts, "系统正在等待您选择使用的账号", 205));
            string errorMsg = "";
            var result = SelectAccountListener((selectedAccount) =>
            {
                param.AccountNo = Regex.Match(selectedAccount, "\\(VND\\) - (\\d*? )").Groups[1].Value.Trim();
                driver.ToChromeDriver().ExecuteScript(Script + $"selectUserAccount({ param.AccountNo})");
                Thread.Sleep(2000);
                bool isSelected = (bool)driver.ToChromeDriver().ExecuteScript($"return $('input[type=hidden]').eq(2).val() == {param.AccountNo};");
                return isSelected ? errorMsg : "Unable to selected user account";
            }, bankInfo.SupportReselectAccount);

            if (result.HasError || !result.IsComplete) throw new Exception(result.Message ?? "Unable to select user account");
        }

        private StepLoopResult SelectTransferType(string key, string value)
        {
            return StepLooping(new StepLoopOption((sleep) =>
            {
                driver.ToChromeDriver().ExecuteScript("$('img')[2].click()");
                sleep();
                driver.ToChromeDriver().ExecuteScript($"$('.x-combo-list-inner').find('div')['{key}'].click()");
                sleep();
                string type = driver.ToChromeDriver().ExecuteScript("return $('input[type=hidden]').eq(0).val()").ToString();
                return type == value;
            })
            {
                MaxLoop = 6,
                SleepInterval = 3
            });

        }
    }
}