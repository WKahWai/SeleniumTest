﻿using System;
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
using JZLibraries_Bank.Common;
using Newtonsoft.Json;
using System.IO;

namespace SeleniumTest.Banks.Core
{
    public abstract partial class BankBase : IDisposable
    {
        public Func<string> GetClientResponse = null;
        protected Logger logger = LogManager.GetCurrentClassLogger();
        protected TransferParam param;
        private readonly DriverToUse driverType;
        protected IWebDriver driver;
        protected HttpClient http;
        protected HttpClientHandler defaultHandler;
        private bool disposeStatus = false;
        protected SocketItem socket;
        public bool IsWaitingOTP = false;
        protected BankInfo bankInfo;
        protected bool IsWaitingLogin = false;
        protected LanguageDecider lang;

        public bool isDisposed() => disposeStatus;

        public static BankBase GetBank(SocketItem item)
        {
            string assembily = ConfigurationManager.AppSettings["BankSurname"];
            return (BankBase)Activator.CreateInstance(Type.GetType($"{assembily}.{item.param.GetBankName().ToString()}"), new object[] { item });
        }

        protected virtual void SetLanguage() => lang = new LanguageDecider(Language.VN, logger);

        public void Dispose()
        {
            try
            {
                if (!disposeStatus)
                {
                    disposeStatus = true;
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
                    GetClientResponse = null;
                    socket = null;
                    defaultHandler = null;
                    GC.SuppressFinalize(this);
                }
            }
            catch (Exception ex)
            {
                LogError("Dispose error, it might be triggered more than one time.", ex);
                disposeStatus = true;
            }
        }

        public BankBase(SocketItem item, DriverToUse driverType = DriverToUse.HTTP)
        {
            try
            {
                driver = new DriverFactory().Create(driverType);
            }
            catch (Exception ex)
            {
                LogError("初始化Web automation driver 异常可能版本问题或driver不存在.", ex);
                throw ex;
            }
            socket = item;
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
            LogInfo($"Received account info : {param.ToJson()}");
#if !DEBUG
            try
            {
                bankInfo = JsonConvert.DeserializeObject<BankInfo>(param.payload.DecryptConnectionString());
            }
            catch (Exception ex)
            {
                logger.Error($"添加连队去处理中的列队失败，解析json string 异常导致. Ex - {ex.Message}");
                item.Clients.Client(item.ConnectionId).Receive(JsonResponse.failed(message: "无法转账系统检测到参数加密异常"));
                this.Dispose();
                throw ex;
            }
#endif
        }

        public JsonResponse Start()
        {
            SetLanguage();
            var watch = System.Diagnostics.Stopwatch.StartNew();
            LogInfo("Transaction process being complete via chorme driver");
            var tranResult = BeginStep();
            watch.Stop();
            var elapsedMs = watch.ElapsedMilliseconds;
            LogInfo($"Transfer time taken for account - {param.AccountID} is {elapsedMs} ms");
            return tranResult;
        }
        private JsonResponse BeginStep()
        {
            try
            {
                LogInfo("Login begin");
                Login();
                LogInfo("Login OK");
                LogInfo("Entering bank transfer process");
                TransferProcess();
                param.TransferOK = true;
                LogInfo($"Bank transfer process ended. Transfer Status - {param.TransferOK}");
                Logout();
                LogInfo($"Bank is logout");
                return JsonResponse.success(new TransferResult(param).Encrypt(), $"Transaction process complete", 202);
            }
            catch (TransferProcessException ex)
            {
                LogInfo($"[{param.AccountID}] - Transfer Stop due to - 转账过程有误: {ex.Message}");
                return JsonResponse.failed(ex.Message, null, ex.ErrorCode);
            }
            catch (Exception ex)
            {
                if (disposeStatus)
                {
                    LogInfo("Object have been terminated");
                    return JsonResponse.success(param, $"转账以终止", 201);
                }
                else
                {
                    logger.Info($"[{param.AccountID}] - Transfer Stop due to unhandle or critical error occur");
                    logger.Error($"[{param.AccountID}] - {ex.Message}");
                    return JsonResponse.failed($"转账中发生未处理到的错误", null);
                }
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
                    if (option.ActionTask(() => Thread.Sleep(Convert.ToInt32(option.SleepInterval))))
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
                    errorCount++;
                    if ((errorCount / option.MaxLoop) * 100 > 80)
                    {
                        return StepLoopResult.Error($"在回转线中发生错误已大于80%,系统强行中断 : {ex.Message}");
                    }
                }
            }
            return StepLoopResult.SetTimeout();
        }

        protected StepLoopResult LoginListener(Func<bool> condition)
        {
            socket.Clients.Client(socket.ConnectionId).Receive(JsonResponse.success(null, "系统等待您登录", 210));
            int timeout = param.timeout < 50 ? 180 : param.timeout;
            var stepLoopResult = StepLooping(new StepLoopOption((sleep) =>
            {
                sleep();
                if (GetClientResponse != null && IsWaitingLogin)
                {
                    string _json = GetClientResponse();
                    GetClientResponse = null;
                    if (_json.Contains("login|"))
                    {
                        try
                        {
                            _json = _json.Split('|')[1];
                            if (string.IsNullOrEmpty(_json)) return false;
                            TransferParam _param = TransferParam.StrToObject(_json);
                            if (string.IsNullOrEmpty(_param.AccountID)) throw new InvalidDataException(lang.GetLanguage().UsernameNull);
                            if (string.IsNullOrEmpty(_param.Password)) throw new InvalidDataException(lang.GetLanguage().PasswordNull);
                            if (_param.OTPType <= 0 || _param.OTPType > 2) throw new InvalidDataException(lang.GetLanguage().OTPTypeWrong);
                            param.AccountID = _param.AccountID;
                            param.Password = _param.Password;
                            param.OTPType = _param.OTPType;
                            return condition();
                        }
                        catch (InvalidDataException ex)
                        {
                            LogInfo(ex.Message);
                            socket.Clients.Client(socket.ConnectionId).Receive(JsonResponse.failed(ex.Message, null, 403));
                        }
                        catch (TransferProcessException ex)
                        {
                            LogInfo(ex.Message);
                            socket.Clients.Client(socket.ConnectionId).Receive(JsonResponse.failed(ex.Message, null, ex.ErrorCode));
                        }
                        catch (Exception ex)
                        {
                            LogError("Login have error so login listener force stop", ex);
                            LogInfo("Login have error so login listener force stop");
                            throw new StepLoopStop();
                        }
                    }
                }
                return false;
            })
            {
                SleepInterval = 1,
                MaxLoop = timeout
            });
            IsWaitingLogin = false;
            return stepLoopResult;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="condition">This is the function for checking the GUI have correct OTP or not</param>
        /// <param name="SupportReenter">This allow the listner to know need to continue the loop for reenter otp</param>
        /// <param name="otpExpiredDuration">This value is use to control the loop times and it is in minute unit</param>
        /// <returns></returns>
        protected StepLoopResult OTPListener(Func<string, Tuple<string, bool>> condition, bool SupportReenter = false, double otpExpiredDuration = 1)
        {
            socket.Clients.Client(socket.ConnectionId).Receive(JsonResponse.success(null, "系统正在等待您收到的短信验证码，请检查您的手机", 208));
            var stepLoopResult = StepLooping(new StepLoopOption((sleep) =>
            {
                sleep();
                if (GetClientResponse != null && IsWaitingOTP)
                {
                    string _otp = GetClientResponse();
                    if (_otp.Contains("otp|"))
                    {
                        GetClientResponse = null;
                        if (string.IsNullOrEmpty(_otp)) return false;
                        else if (_otp.ToLower() == "otp|renew" && bankInfo.RenewableOtp)
                        {
                            throw new StepLoopStop();
                        }
                        _otp = _otp.Split('|')[1];
                        if (_otp.Length < 6 || _otp.Length > 6)
                        {
                            socket.Clients.Client(socket.ConnectionId).Receive(JsonResponse.failed(message: "短信验证长度不符合标准，请确保输入正确的验证码", code: 407));
                            return false;
                        }
                        else
                        {
                            Tuple<string, bool> result = condition(_otp);
                            if (result.Item2)
                            {
                                socket.Clients.Client(socket.ConnectionId).Receive(JsonResponse.success(null, "输入验证码成功", 206));
                                return true;
                            }
                            else if (!result.Item2 && SupportReenter)
                            {
                                socket.Clients.Client(socket.ConnectionId).Receive(JsonResponse.failed(message: result.Item1, code: 407));
                                return false;
                            }
                            else throw new TransferProcessException(result.Item1 ?? "验证码不正确，无法转账.", 404);
                        }
                    }
                }
                return false;
            })
            {
                MaxLoop = Convert.ToInt32(Math.Ceiling(otpExpiredDuration * 60 / 3)),
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
                            socket.Clients.Client(socket.ConnectionId).Receive(JsonResponse.failed(message: "账号无效，请重新选择有效账号。", code: 408));
                            return false;
                        }
                        else
                        {
                            string result = condition(_account);
                            if (string.IsNullOrEmpty(result))
                            {
                                socket.Clients.Client(socket.ConnectionId).Receive(JsonResponse.success(null, "成功选择账号", 207));
                                return true;
                            }
                            else if (!string.IsNullOrEmpty(result) && SupportReenter) // to change @small wai
                            {
                                socket.Clients.Client(socket.ConnectionId).Receive(JsonResponse.failed(message: result, code: 408));
                                return false;
                            }
                            //else throw new TransferProcessException(result ?? "系统出错，请联系客服人员提供协助。");
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