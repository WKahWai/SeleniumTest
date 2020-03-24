using OpenQA.Selenium;
using SeleniumTest.SeleniumHelpers;
using SeleniumTest.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace SeleniumTest.Controllers
{
    public class BanksController : ApiController
    {

        private IWebDriver _driver;
        private string _baseUrl;

        // GET api/values
        public IEnumerable<string> Get()
        {
            return new string[] { "value1", "value2" };
        }

        // GET api/values/5
        public string Get(int id)
        {
            _driver = new DriverFactory().Create();
            _baseUrl = ConfigurationHelper.Get<string>("TargetUrl");

            _driver.Navigate().GoToUrl(_baseUrl);

            var data = _driver.PageSource;
            var data2 = _driver.FindElement(By.ClassName("gLFyf"));
            Random Rnd = new Random();
            data2.SendKeys("Test" + char.ConvertFromUtf32(Rnd.Next(65, 90)));
            var data3 = _driver.FindElement(By.CssSelector("input[aria-label=\"Google Search\"]"));
            data3.Click();
            var data4 = "Rnd" + Environment.NewLine + _driver.PageSource;

            System.IO.File.WriteAllText(@"C:\Users\Kah Wai\Desktop\" + char.ConvertFromUtf32(Rnd.Next(65, 90)) + ".txt", data4);

            _driver.Quit();

            return data4;
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
