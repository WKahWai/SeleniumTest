using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;

namespace SeleniumTest.Models
{
    public class LanguageModel
    {
        public string UsernameNull { get; set; }
        public string PasswordNull { get; set; }
        public string OTPTypeWrong { get; set; }
        public string LoginTimeout { get; set; }
        public string InsufficientAmount { get; set; }

        public static LanguageModel Default()
        {
            return new LanguageModel
            {
                UsernameNull = "Username is null",
                PasswordNull = "Password is null",
                OTPTypeWrong = "Invalid OTP",
                LoginTimeout = "Login timeout",
                InsufficientAmount = "Insufficient amount"
            };
        }
    }

    public class LanguageDecider : IDisposable
    {
        private Logger logger;
        private LanguageModel model;

        public LanguageDecider(Language language, Logger logger)
        {
            this.logger = logger;
            try
            {
                string path = Path.Combine(AppContext.BaseDirectory, "Language", $"{language.ToString().ToLower()}.json");
                string json = File.ReadAllText(path);
                model = JsonConvert.DeserializeObject<LanguageModel>(json);
            }
            catch (Exception ex)
            {
                logger.Error($"Get lanugage file error. Ex - {ex.Message}");
            }
            finally
            {
                model = model ?? LanguageModel.Default();
            }
        }

        public LanguageModel GetLanguage() => model;

        public void Dispose()
        {
            model = null;
            logger = null;
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
    }
}