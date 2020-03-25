﻿using System;
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
        public VCBBank(TransferParam param) : base(param, DriverToUse.Chrome)
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
                try
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
                SleepInterval = 5
            });
            if (condition.HasError && !condition.IsComplete) throw new Exception(condition.Message);
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
            logger.Info($"[{this.GetType().Name}][LoginBank]验证码 : {code}");
            if (code.Length != 5)
            {
                logger.Info($"[{this.GetType().Name}][LoginBank]登录失败.验证码识别失败[{decode.GetCodeName(decode.Dama.f_codeuse)}]-{code}");
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
            throw new NotImplementedException();
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
                    sleep();
                    if (string.IsNullOrEmpty(driver.FindElement(By.Id("TenNguoiHuongText")).Text)) throw new TransferProcessException("Invalid recipient account");
                    amount.SendKeys(param.Amount.ToString());
                    var remark = driver.FindElement(By.Id("NoiDungThanhToan"));
                    remark.Clear();
                    remark.SendKeys(param.Remark);
                    driver.FindElement(By.Id("btnxacnhan")).Click();
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

        private void SelectUserAccount()
        {
            var account = driver.FindElement(By.Id("TaiKhoanTrichNo"));
            var select = new SelectElement(account);
            if (select.AllSelectedOptions.Count(c => c.Text == param.AccountNo) > 0)
            {
                select.SelectByText(param.AccountNo);
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