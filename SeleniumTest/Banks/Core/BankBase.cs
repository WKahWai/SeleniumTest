using System;
using SeleniumTest.Models;
using NLog;
using OpenQA.Selenium;
using SeleniumTest.SeleniumHelpers;
using BankAPI.Exceptions;
using BankAPI.Model;
using System.Threading;
using System.Configuration;
using System.Net.Http;

namespace SeleniumTest.Banks.Core
{
    public abstract class BankBase : IDisposable
    {
        protected Logger logger = LogManager.GetCurrentClassLogger();
        protected TransferParam param;
        //protected readonly bool HaveMultipleAccount;
        private readonly DriverToUse driverType;
        protected IWebDriver driver;
        protected HttpClient http;
        protected HttpClientHandler defaultHandler;
        private bool disposeStatus = false;

        public bool isDisposed() => disposeStatus;

        public static BankBase GetBank(TransferParam param)
        {
            string assembily = ConfigurationManager.AppSettings["BankSurname"];
            return (BankBase)Activator.CreateInstance(Type.GetType($"{assembily}.{param.GetBankName().ToString()}"), new object[] { param });

        }

        public void Dispose()
        {
            if (driver != null)
            {
                Logout();
                driver.Dispose();
                driver.Quit();
                driver = null;
            }
            if (http != null)
            {
                http.CancelPendingRequests();
                http.Dispose();
                http = null;
            }
            defaultHandler = null;
            GC.SuppressFinalize(this);
            disposeStatus = true;
        }

        public BankBase(TransferParam param, DriverToUse driverType = DriverToUse.HTTP)
        {
            driver = new DriverFactory().Create(driverType);
            this.param = param;
            this.driverType = driverType;
            defaultHandler = new HttpClientHandler
            {
                AllowAutoRedirect = true,
                UseProxy = false,
                UseCookies = true,
            };
            http = new HttpClient(defaultHandler);
            http.Timeout = TimeSpan.FromMinutes(5);
            logger.Info($"Received account info : {param.ToJson()}");
        }

        protected virtual void Validation()
        {
            //todo
        }

        public TransactionResult Start()
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            TransactionResult tranResult = null;
            logger.Info("Transaction process being complete via chorme driver");
            tranResult = BeginStep();
            watch.Stop();
            var elapsedMs = watch.ElapsedMilliseconds;
            logger.Info($"Transfer time taken for account - {param.AccountNo} is {elapsedMs} ms");
            return tranResult;
        }

        private TransactionResult BeginStep()
        {
            try
            {
                logger.Info("Login begin");
                Login();
                logger.Info("Login OK");
                logger.Info("Entering bank transfer process");
                TransferProcess();
                logger.Info($"Bank transfer process ended. Transfer Status - {param.TransferOK}");
                Logout();
                logger.Info($"Bank is logout");
                return TransactionResult.Success($"Transaction process complete", param);
            }
            catch (TransferProcessException ex)
            {
                return TransactionResult.Failed(ex.Message, 1);
            }
            catch (Exception ex)
            {
                if (disposeStatus) logger.Info("Object have been terminated");
                return TransactionResult.Failed($"转账中发生未处理到的错误", 500);
            }
        }

        private void TransferProcess()
        {
            Transfer();
            OTP();
            CheckTransferStatus();
        }

        protected abstract void Login();
        protected abstract void Logout();
        protected abstract void Transfer();
        protected abstract void RenewOTP();
        protected abstract void OTP();
        protected abstract void CheckTransferStatus();

        protected StepLoopResult StepLooping(StepLoopOption option)
        {
            for (int i = 0; i < option.MaxLoop; i++)
            {
                try
                {
                    if (option.ActionTask(() => Thread.Sleep(option.SleepInterval)))
                    {
                        return StepLoopResult.Complete();
                    }
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