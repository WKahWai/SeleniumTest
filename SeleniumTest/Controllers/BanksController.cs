using OpenQA.Selenium;
using SeleniumTest.SeleniumHelpers;
using SeleniumTest.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Threading;
using SeleniumTest.Models;
using System.Linq;
using SeleniumTest.Banks.Core;
using System.Threading.Tasks;

namespace SeleniumTest.Controllers
{
    public class BanksController : ApiController
    {

        private IWebDriver _driver;
        private string _baseUrl;

        // GET api/values
        [HttpGet]
        public JsonResponse List()
        {
            return JsonResponse.success(BankBase.GetBankInfoList(), "Request successful");
            //return JsonResponse.success(new TransferParam(), "");
        }

        // GET api/values/5
        public string Get(int id)
        {
            //_driver = new DriverFactory().Create();
            //_driver.Url = "https://www.dropbox.com/zh_TW/";
            ////_driver.Navigate();
            //Thread.Sleep(3000);
            //_driver.FindElement(By.Id("sign-up-in")).Click();
            //_driver.FindElement(By.Name("login_email")).SendKeys("alanee1996@gmail.com");
            //_driver.FindElement(By.Name("login_password")).SendKeys("Esy101045600A");
            //var checkbox = _driver.FindElement(By.Name("remember_me"));
            //if (!checkbox.Selected) checkbox.Click();
            //_driver.FindElement(By.XPath("//button[@type='submit']")).Click();

            //_driver.Quit();

            return "yes";
        }

        // POST api/values
        public void Post([FromBody]string value)
        {
        }

        // PUT api/values/5
        public void Put(int id, [FromBody]string value)
        {
        }

        // DELETE api/values/5
        public void Delete(int id)
        {
        }
    }
}
