using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using BitmapProcessing;

namespace Aurora.Framework
{
    /// <summary>
    /// Keeps track of data and builds a graph out of the given data
    /// </summary>
    public class Profiler
    {
        private Dictionary<string, ProfilerValueManager> Stats = new Dictionary<string, ProfilerValueManager>();
        private int[] GraphBarsStart = new int[] { 25, 40, 55, 70, 85, 100, 115, 130, 145, 160 };
        private int[] GraphBarsEnd = new int[] { 35, 50, 65, 80, 95, 110, 125, 140, 155, 170 };
        private Color LineColor = Color.DarkGray;
        private Color BackgroundColor = Color.LightGray;
        private Color BarColor = Color.Aqua;

        public void AddStat(string Name, double value)
        {
            if (!Stats.ContainsKey(Name))
                Stats[Name] = new ProfilerValueManager();
            Stats[Name].AddStat(value);
        }

        public ProfilerValueManager GetStat(string Name)
        {
            ProfilerValueManager manager = null;
            Stats.TryGetValue(Name, out manager);
            return manager;
        }

        public FastBitmap DrawGraph(string StatName)
        {
            Bitmap bitmap = new Bitmap(200, 200, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            FastBitmap bmp = new FastBitmap(bitmap);
            bmp.LockBitmap();

            ProfilerValueManager statManager = GetStat(StatName);
            double MaxVal = statManager.GetMaxValue();

            double ScaleFactor = 1 / (MaxVal / 200); //We multiply by this so that the graph uses the full space

            double[] Stats = statManager.GetInfos();

            for (int i = 0; i < Stats.Length; i++)
            {
                //Update the scales
                Stats[i] = Stats[i] * ScaleFactor;
            }

            for (int x = 200; x > 0; x--)
            {
                for (int y = 200; y > 0; y--)
                {
                    //Note: we do 200-y to flip the graph on the Y axis
                    if (IsInGraphBar(x, y, Stats, ScaleFactor))
                        bmp.SetPixel(x, 200 - y, BarColor);
                    else
                    {
                        //Check whether the line needs drawn
                        if (DrawLine(y, ScaleFactor))
                            bmp.SetPixel(x, 200 - y, LineColor);
                        else
                            bmp.SetPixel(x, 200 - y, BackgroundColor);
                    }
                }
            }
            bmp.UnlockBitmap();

            return bmp;
        }

        public FastBitmap DrawGraph(string StatName, double MaxVal)
        {
            Bitmap bitmap = new Bitmap(200, 200, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            FastBitmap bmp = new FastBitmap(bitmap);
            bmp.LockBitmap();

            ProfilerValueManager statManager = GetStat(StatName);

            double ScaleFactor = 1 / (MaxVal / 200); //We multiply by this so that the graph uses the full space

            double[] Stats = statManager.GetInfos();

            for (int x = 200; x > 0; x--)
            {
                for (int y = 200; y > 0; y--)
                {
                    //Note: we do 200-y to flip the graph on the Y axis
                    if (IsInGraphBar(x, y, Stats, ScaleFactor))
                        bmp.SetPixel(x, 200 - y, BarColor);
                    else
                    {
                        //Check whether the line needs drawn
                        if (DrawLine(y, ScaleFactor))
                            bmp.SetPixel(x, 200 - y, LineColor);
                        else
                            bmp.SetPixel(x, 200 - y, BackgroundColor);
                    }
                }
            }
            bmp.UnlockBitmap();

            return bmp;
        }

        private bool DrawLine(double y, double ScaleFactor)
        {
            return (y % 10) == 0;
        }

        private bool IsInGraphBar(int x, int y, double[] Stats, double scaleFactor)
        {
            for (int i = Math.Min(GraphBarsStart.Length - 1, Stats.Length - 1); i >= 0; i--)
            {
                //Check whether it is between both the start and end
                if (x > GraphBarsStart[i] && x < GraphBarsEnd[i])
                {
                    if (Stats[i] >= (y / scaleFactor))
                        return true;
                }
            }
            return false;
        }
    }

    public class ProfilerValueManager
    {
        private double[] infos = new double[10];
        private int lastSet = 0;
        private int zero = 0;

        public void AddStat(double value)
        {
            lock (infos)
            {
                if (lastSet != 10)
                {
                    infos[lastSet] = value;
                    lastSet++;
                }
                else
                {
                    //Move the 0 value around
                    infos[zero] = value;
                    //Now increment 0 as it isn't where it was before
                    zero++;
                    if (zero == 10)
                        zero = 0;
                }
            }
        }

        public double[] GetInfos()
        {
            lock (infos)
            {
                double[] copy = new double[lastSet];
                int ii = zero;
                for (int i = 0; i < lastSet; i++)
                {
                    copy[i] = infos[ii];
                    ii++;
                    if (ii > lastSet)
                        ii = 0;
                }
                return copy;
            }
        }

        public double GetMaxValue()
        {
            double MaxVal = 0;
            lock (infos)
            {
                for (int i = 0; i < lastSet; i++)
                {
                    if (infos[i] > MaxVal)
                        MaxVal = infos[i];
                }
            }
            return MaxVal;
        }
    }

    public class ProfilerManager
    {
        private static Profiler profiler = null;
        public static Profiler GetProfiler()
        {
            if (profiler == null)
                profiler = new Profiler();
            return profiler;
        }
    }
}
