using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using VerificationCodeIdentification.Class;
using VerificationCodeIdentification.Help;

namespace SeleniumTest.Banks.Core
{
    public class DecodeClass
    {
        private List<WordStockIndex> listWord = new List<WordStockIndex>();
        public Project pro = null;

        public DecodeClass(Project project)
        {
            try
            {
                if (project != null)
                    pro = project;
            }
            catch (Exception) { }
        }
        public string DeCode(Bitmap bmp, int Average = -1)
        {
            string code = string.Empty;

            try
            {
                if (pro == null)
                    return code;

                //Bitmap img = ImageProcessing.ClearNoise(
                //    ImageProcessing.ConvertTo1Bpp(bmp, Convert.ToInt32(pro.proSettings.Average)),
                //    Convert.ToInt32(pro.proSettings.NearPoints));

                Bitmap img = null;

                if (pro.proSettings.ClearType == "EmguCV")
                {
                    img = ImageProcessing.EmguCVClearNoise(bmp, pro.proSettings.Average, pro.proSettings.NearPoints);
                }
                else
                {
                    //img = ImageProcessing.ConvertTo1Bpp(bmp.Clone(new Rectangle(0, 0, bmp.Width, bmp.Height), PixelFormat.Format24bppRgb), Convert.ToInt32(nbAverage.Value));
                    //newBmp = ImageProcessing.ClearNoise(newBmp, Convert.ToInt32(nbNearPoints.Value));
                    Average = Average != -1 ? Average : pro.proSettings.Average;
                    img = ImageProcessing.ClearNoise(ImageProcessing.ConvertTo1Bpp(bmp, Average), pro.proSettings.NearPoints);
                }

                Bitmap[] arr = null;
                switch (pro.proSettings.Split)
                {
                    case "平均分割":
                        arr = ImageProcessing.GetSplitPics(img, pro.proSettings.CharCount, 1);
                        break;
                    case "扫描分割":
                        arr = ImageProcessing.GetSplitPics(img, pro.proSettings.nbBorderType);
                        break;
                    case "智能分割":
                        arr = ImageProcessing.GetSplitPicsAI(img, pro.proSettings.nbBorderType, pro.proSettings.nbPicHeight, pro.proSettings.nbPicWidth);
                        break;
                    default:
                        break;
                }

                if (arr == null || arr.Length != pro.proSettings.CharCount)
                    return code;

                listWord.Clear();


                Task[] tasks = new Task[arr.Length];

                for (int i = 0; i < arr.Length; i++)
                {
                    BitmapIndex bi = new BitmapIndex()
                    {
                        Index = i,
                        Bmp = arr[i]
                    };
                    tasks[i] = Task.Factory.StartNew(GetCode, bi);
                }

                Task.WaitAll(tasks, 30000);//等待三十秒
                //释放资源
                for (int i = 0; i < tasks.Length; i++)
                {
                    tasks[i].Dispose();
                    arr[i].Dispose();
                }
                img.Dispose();

                code = string.Join("", listWord.OrderBy(x => x.Index).Select(x => x.Word.Key));
            }
            catch (Exception ex) { }
            return code;
        }

        private void GetCode(object obj)
        {
            WordStockIndex ws = Compar.GetCode(pro.wordStocks, (BitmapIndex)obj, Convert.ToDecimal(pro.proSettings.Similarity / 100.0));
            if (ws != null)
                listWord.Add(ws);
        }
    }
}