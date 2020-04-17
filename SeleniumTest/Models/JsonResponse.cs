using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace SeleniumTest.Models
{
    public class JsonResponse
    {
        /// <summary>
        /// 返回的数据
        /// </summary>
        public object data { get; set; }
        /// <summary>
        /// 是否有误 true = 有误 false = 正常
        /// </summary>
        public bool hasError { get; set; }
        /// <summary>
        /// 反应的信息
        /// </summary>
        public string message { get; set; }
        /// <summary>
        /// 0 = 成功，1 = Critcal error
        /// </summary>
        public int code { get; set; }

        /// <summary>
        /// 返回成功的JSON包装
        /// </summary>
        /// <param name="data">返回的数据</param>
        /// <param name="message">反应的信息</param>
        /// <returns>成功的JSON 格式包装</returns>
        public static JsonResponse success(object data, string message, int code = 204)
        {
            return new JsonResponse { data = data, hasError = false, message = message, code = code };
        }
        /// <summary>
        /// 返回成功的JSON包装
        /// </summary>
        /// <param name="data">返回的数据</param>
        /// <param name="message">反应的信息</param>
        /// <returns>失败的JSON 格式包装</returns>
        public static JsonResponse failed(string message, object data = null, int code = 500)
        {
            return new JsonResponse { data = data, hasError = true, message = message, code = code };
        }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}