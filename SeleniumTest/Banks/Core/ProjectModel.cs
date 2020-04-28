using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Web;
using VerificationCodeIdentification.Class;

namespace SeleniumTest.Banks.Core
{
    public class ProjectModel
    {
        public Project pro = null;
        public ProjectModel(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    using (FileStream fs = new FileStream(path, FileMode.Open))
                    {
                        BinaryFormatter formatter = new BinaryFormatter();
                        pro = (Project)formatter.Deserialize(fs);
                    }
                }
            }
            catch (Exception) { }
        }

    }
}