using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using SeleniumTest.Models;
using CsharpHttpHelper;
using NLog;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.IE;
using SeleniumTest.SeleniumHelpers;
using BankAPI.Exceptions;
using BankAPI.Model;
using System.Threading;
using System.Configuration;

namespace SeleniumTest.Banks.Core
{
    public abstract class BankBase : IDisposable
    {
        public static BankBase GetBank(TransferParam param)
        {
            string assembily = ConfigurationManager.AppSettings["BankSurname"];
            return (BankBase)Activator.CreateInstance(Type.GetType($"{assembily}.{param.GetBankName().ToString()}"), new object[] { param });

        }

        public virtual void Dispose()
        {
            GC.SuppressFinalize(this);
        }

        protected Logger logger = LogManager.GetCurrentClassLogger();
        protected TransferParam param;
        //protected readonly bool HaveMultipleAccount;
        private readonly DriverToUse driverType;
        protected HttpHelper http;
        protected HttpItem item;
        protected HttpResult result;

        public BankBase(TransferParam param, DriverToUse driverType = DriverToUse.HTTP)
        {
            this.param = param;
            this.driverType = driverType;
            http = new HttpHelper();
            item = new HttpItem();
            logger.Info($"Received account info : {param.ToJson()}");
        }

        protected virtual void Validation()
        {

        }

        public TransactionResult Start()
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            TransactionResult tranResult = null;
            using (var driver = new DriverFactory().Create(driverType))
            {
                logger.Info("Transaction process being complete via chorme driver");
                tranResult = BeginStep(driver);
                driver.Quit();
                driver.Dispose();
                watch.Stop();
                var elapsedMs = watch.ElapsedMilliseconds;
                logger.Info($"Transfer time taken for account - {param.AccountNo} is {elapsedMs} ms");
                return tranResult;
            }
        }

        private TransactionResult BeginStep(IWebDriver driver)
        {
            try
            {
                logger.Info("Login begin");
                Login(driver);
                logger.Info("Login OK");
                logger.Info("Entering bank transfer process");
                TransferProcess(driver);
                logger.Info($"Bank transfer process ended. Transfer Status - {param.TransferOK}");
                Logout(driver);
                logger.Info($"Bank is logout");
                return TransactionResult.Success($"Transaction process complete", param);
            }
            catch (TransferProcessException ex)
            {
                Logout(driver);
                return TransactionResult.Failed(ex.Message, 1);
            }
            catch (Exception ex)
            {
                Logout(driver);
                return TransactionResult.Failed($"转账中发生未处理到的错误", 500);
            }
        }

        private void TransferProcess(IWebDriver driver)
        {
            Transfer(driver);
            OTP(driver);
            CheckTransferStatus(driver);
        }

        protected abstract void Login(IWebDriver driver);
        protected abstract void Logout(IWebDriver driver);
        protected abstract void Transfer(IWebDriver driver);
        protected abstract void RenewOTP(IWebDriver driver);
        protected abstract void OTP(IWebDriver driver);
        protected abstract void CheckTransferStatus(IWebDriver driver);

        protected StepLoopResult StepLooping(StepLoopOption option)
        {
            for (int i = 0; i < option.MaxLoop; i++)
            {
                try
                {
                    if (option.ActionTask())
                    {
                        return StepLoopResult.Complete();
                    }
                    Thread.Sleep(option.SleepInterval);
                }
                catch (TransferProcessException ex)
                {
                    throw ex;
                }
                catch (Exception ex)
                {
                    return StepLoopResult.Error($"在回转线中发生错误 : {ex.Message}");
                }
            }
            return StepLoopResult.SetTimeout();
        }
    }
}