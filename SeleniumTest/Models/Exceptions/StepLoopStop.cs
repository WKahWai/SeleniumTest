using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace SeleniumTest.Models.Exceptions
{
    public class StepLoopStop : Exception
    {
        public StepLoopStop() : base("回旋终止")
        {

        }
    }
}