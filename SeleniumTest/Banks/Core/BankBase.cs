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
using SeleniumTest.Models.Exceptions;

namespace SeleniumTest.Banks.Core
{
    public abstract class BankBase : IDisposable
    {
        public Func<string> GetClientResponse = null;
        protected Logger logger = LogManager.GetCurrentClassLogger();
        protected TransferParam param;
        //protected readonly bool HaveMultipleAccount;
        private readonly DriverToUse driverType;
        protected IWebDriver driver;
        protected HttpClient http;
        protected HttpClientHandler defaultHandler;
        private bool disposeStatus = false;
        protected SocketItem socket;
        public bool IsWaitingOTP = false;
        protected readonly bool SupportOTPRenew;

        public bool isDisposed() => disposeStatus;

        public static BankBase GetBank(SocketItem item)
        {
            string assembily = ConfigurationManager.AppSettings["BankSurname"];
            return (BankBase)Activator.CreateInstance(Type.GetType($"{assembily}.{item.param.GetBankName().ToString()}"), new object[] { item });
        }

        public void Dispose()
        {
            if (!disposeStatus)
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
            }
            GetClientResponse = null;
            socket = null;
            defaultHandler = null;
            GC.SuppressFinalize(this);
            disposeStatus = true;
        }

        public BankBase(SocketItem item, DriverToUse driverType = DriverToUse.HTTP, bool SupportOTPRenew = false)
        {
            driver = new DriverFactory().Create(driverType);
            socket = item;
            this.SupportOTPRenew = SupportOTPRenew;
            param = item.param;
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
                logger.Info($"[{param.AccountNo}] - Transfer Stop due to - {ex.Message}");
                return TransactionResult.Failed(ex.Message, 1);
            }
            catch (Exception ex)
            {
                if (disposeStatus)
                {
                    logger.Info("Object have been terminated");
                    return TransactionResult.Success($"转账以终止", param);
                }
                else
                {
                    logger.Info($"[{param.AccountNo}] - Error occur");
                    logger.Error($"[{param.AccountNo}] - {ex.Message}");
                }
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
            int errorCount = 0;
            for (int i = 0; i < option.MaxLoop; i++)
            {
                try
                {
                    if (option.ActionTask(() => Thread.Sleep(option.SleepInterval)))
                    {
                        return StepLoopResult.Complete();
                    }
                }
                catch (StepLoopStop ex)
                {
                    return StepLoopResult.ForceBreak();
                }
                catch (TransferProcessException ex)
                {
                    throw ex;
                }
                catch (Exception ex)
                {
                    if ((errorCount / option.MaxLoop) > 80)
                    {
                        return StepLoopResult.Error($"在回转线中发生错误已大于80%,系统强行中断 : {ex.Message}");
                    }
                    errorCount++;
                }
            }
            return StepLoopResult.SetTimeout();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="condition">This is the function for checking the GUI have correct OTP or not</param>
        /// <param name="SupportReenter">This allow the listner to know need to continue the loop for reenter otp</param>
        /// <param name="otpExpiredDuration">This value is use to control the loop times and it is in minute unit</param>
        /// <returns></returns>
        protected StepLoopResult OTPListener(Func<string, Tuple<string, bool>> condition, bool SupportReenter = false, int otpExpiredDuration = 1)
        {
            var stepLoopResult = StepLooping(new StepLoopOption((sleep) =>
            {
                sleep();
                if (GetClientResponse != null)
                {
                    string _otp = GetClientResponse();
                    if (_otp.Contains("otp|"))
                    {
                        GetClientResponse = null;
                        if (string.IsNullOrEmpty(_otp)) return false;
                        else if (_otp.ToLower() == "otp|renew" && SupportOTPRenew)
                        {
                            throw new StepLoopStop();
                        }
                        _otp = _otp.Split('|')[1];
                        if (_otp.Length < 6 || _otp.Length > 6)
                        {
                            socket.Clients.Client(socket.ConnectionId).Receive(JsonResponse.failed("短信验证长度不符合标准，请确保输入正确的验证码"));
                            return false;
                        }
                        else
                        {
                            Tuple<string, bool> result = condition(_otp);
                            if (result.Item2)
                            {
                                return true;
                            }
                            else if (!result.Item2 && SupportReenter)
                            {
                                socket.Clients.Client(socket.ConnectionId).Receive(JsonResponse.failed(result.Item1));
                                return false;
                            }
                            else throw new TransferProcessException(result.Item1 ?? "验证码不正确，无法转账。");
                        }
                    }
                }
                return false;
            })
            {
                MaxLoop = otpExpiredDuration * 60 / 3,
                SleepInterval = 3
            });
            IsWaitingOTP = false;
            return stepLoopResult;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="condition">This is the function for checking the GUI have correct selected account</param>
        /// <param name="SupportReenter">This allow the listner to know need to continue the loop for re-select account</param>
        /// <returns></returns>
        protected StepLoopResult SelectAccountListener(Func<string, string> condition, bool SupportReenter = false)
        {
            var stepLoopResult = StepLooping(new StepLoopOption((sleep) =>
            {
                sleep();
                if (GetClientResponse != null)
                {
                    string _account = GetClientResponse();
                    if (_account.Contains("selectAccount|"))
                    {
                        GetClientResponse = null;
                        if (string.IsNullOrEmpty(_account)) return false;

                        _account = _account.Split('|')[1];
                        if (_account.Length <= 0)
                        {
                            socket.Clients.Client(socket.ConnectionId).Receive(JsonResponse.failed("账号无效，请重新选择有效账号。"));
                            return false;
                        }
                        else
                        {
                            string result = condition(_account);
                            if (string.IsNullOrEmpty(result))
                            {
                                return true;
                            }
                            else if (!string.IsNullOrEmpty(result) && SupportReenter)
                            {
                                socket.Clients.Client(socket.ConnectionId).Receive(JsonResponse.failed(result));
                                return false;
                            }
                            else throw new TransferProcessException(result ?? "系统出错，请联系客服人员提供协助。");
                        }
                    }
                }
                return false;
            })
            {
                MaxLoop = 80,
                SleepInterval = 3
            });

            return stepLoopResult;
        }
    }
}