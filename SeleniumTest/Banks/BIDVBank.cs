﻿using BankAPI.Exceptions;
using BankAPI.Model;
using JZLibraries_Bank.DeCode;
using OpenQA.Selenium;
using SeleniumTest.Banks.Core;
using SeleniumTest.Models;
using SeleniumTest.SeleniumHelpers;
using System;
using System.Collections.Generic;
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

        public BIDVBank(SocketItem item) : base(item, DriverToUse.Chrome)
        {

        }

        protected override void CheckTransferStatus()
        {
            throw new NotImplementedException();
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
                SleepInterval = 3
            });
            if (result.HasError || !result.IsComplete) throw new Exception($"[{param.AccountNo}] - 获取首页失败");
            SwitchToEnglish();
            result = StepLooping(new StepLoopOption((sleep) =>
            {
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
                    if (!string.IsNullOrEmpty(error)) throw new TransferProcessException(error);
                }
                return false;
            })
            {
                MaxLoop = 2,
                SleepInterval = 6
            });
            if (result.HasError || !result.IsComplete) throw new Exception($"[{param.AccountNo}] - 登录失败");
            RetriveDiness();
        }

        private void SwitchToEnglish()
        {
            driver.ToChromeDriver().ExecuteScript("javascript:localization('en_US');");
        }

        private void RetriveDiness()
        {
            var regex = Regex.Match(driver.PageSource, "\"CSRF_ID_LIST\":\"(.+?)\"");
            if (regex.Success)
            {
                diness = regex.Result("$1");
                diness = Regex.Replace(diness, @"(.{32})", "$1-").Trim('-');
                string[] dinessList = diness.Split('-');
                diness = dinessList[dinessList.Length - 1];
                logger.Info($"[{param.AccountNo}] - Retrive diness from page success. Diness Key - {diness}");
            }
            else
            {
                logger.Error($"[{param.AccountNo}] - Unable to retrive diness from page");
            }
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
            throw new NotImplementedException();
        }

        protected override void RenewOTP()
        {
            throw new NotImplementedException();
        }

        protected override void Transfer()
        {
            throw new NotImplementedException();
        }
    }
}