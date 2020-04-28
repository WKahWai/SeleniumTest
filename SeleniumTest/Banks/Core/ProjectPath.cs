using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;

namespace SeleniumTest.Banks.Core
{
    public static class ProjectPath
    {
        public static ProjectModel BIDVProject = new ProjectModel(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bin", "DF", "BIDV.df"));
    }
}