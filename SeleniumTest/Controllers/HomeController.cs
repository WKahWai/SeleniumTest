using OpenQA.Selenium;
using SeleniumTest.SeleniumHelpers;
using SeleniumTest.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace SeleniumTest.Controllers
{
    public class HomeController : Controller
    {
        private IWebDriver _driver;
        private string _baseUrl;

        public ActionResult Index()
        {
            ViewBag.Title = "Home Page";

            //_driver = new DriverFactory().Create();
            //_baseUrl = ConfigurationHelper.Get<string>("TargetUrl");

            //_driver.Navigate().GoToUrl(_baseUrl);

            //var data = _driver.PageSource;
            //var data2 = _driver.FindElement(By.ClassName("gLFyf"));
            //Random Rnd = new Random();
            //data2.SendKeys("Test" + char.ConvertFromUtf32(Rnd.Next(65, 90)));
            //var data3 = _driver.FindElement(By.CssSelector("input[aria-label=\"Google Search\"]"));
            //data3.Click();
            //var data4 = "Rnd" + Environment.NewLine + _driver.PageSource;

            //System.IO.File.WriteAllText(@"C:\Users\Kah Wai\Desktop\" + char.ConvertFromUtf32(Rnd.Next(65, 90)) + ".txt", data4);

            //_driver.Quit();

            return View();
        }
    }
}
