using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using StockSharp.Algo.Candles;

namespace WpfApplication1
{
    public static class DartWeiderIndicators
    {

        // Метод получения MACD Histogram
        public static decimal macdHist(CandleSeries series, int curcand)
        {
            decimal macdHist = 0;

            decimal macdSig = 0;

            for (int i = curcand; i < (curcand + 9); i++)
            {

                macdSig += MACD(series, i);

            }

            macdSig = macdSig / 9;

            macdHist = MACD(series, curcand) - macdSig;

            return macdHist;
        }

        // Метод получения MACD
        public static decimal MACD(CandleSeries series, int curcand)
        {

            decimal MALong = 0;

            for (int i = curcand; i < (curcand + 26); i++)
            {
                MALong += series.GetCandle<TimeFrameCandle>(i).ClosePrice;
            }

            MALong = MALong / 26;

            decimal MAShort = 0;

            for (int i = curcand; i < (curcand + 12); i++)
            {
                MAShort += series.GetCandle<TimeFrameCandle>(i).ClosePrice;
            }

            MAShort = MAShort / 12;

            decimal MACD = MAShort - MALong;

            return MACD;

        }

        //Метод получения Stohastic %K
        public static decimal stohK(CandleSeries series, int curcand, int kPer)
        {
            decimal stohK = 0;

            decimal close = series.GetCandle<TimeFrameCandle>(curcand).ClosePrice;

            decimal low = series.GetCandle<TimeFrameCandle>(curcand).HighPrice;

            for (int z = Convert.ToInt32(curcand); z < Convert.ToInt32(curcand) + kPer; z++)
            {
                if (series.GetCandle<TimeFrameCandle>(z).LowPrice < low)
                    low = series.GetCandle<TimeFrameCandle>(z).LowPrice;
            }

            decimal high = series.GetCandle<TimeFrameCandle>(curcand).LowPrice;

            for (int x = Convert.ToInt32(curcand); x < Convert.ToInt32(curcand) + kPer; x++)
            {
                if (series.GetCandle<TimeFrameCandle>(x).HighPrice > high)
                    high = series.GetCandle<TimeFrameCandle>(x).HighPrice;
            }

            stohK = 100 * ((close - low) / (high - low));

            return stohK;
        }

        //Метод получения Stohastic %D
        public static decimal stohD(CandleSeries series, int curcand, int kPer, int dPer)
        {
            decimal stohD = 0;

            decimal total = 0;

            for (int c = Convert.ToInt32(curcand); c < Convert.ToInt32(curcand) + dPer; c++)
            {
                total += stohK(series, c, kPer);
            }

            stohD = total / dPer;

            return stohD;
        }

        // Метод получения MACD Histogram Exponential
        public static decimal MACDHistExp(CandleSeries series, int curcand)
        {
            decimal MACDHistExp = 0;

            decimal MACDSig = 0;

            for (int i = curcand + 8; i >= curcand; i--)
            {
                MACDSig += MACDExp(series, i);
            }

            MACDSig = MACDSig / 9;

            MACDHistExp = MACDExp(series, curcand) - MACDSig;

            return MACDHistExp;
        }

        // Метод получения MACD Exponential
        public static decimal MACDExp(CandleSeries series, int curcand)
        {
            decimal MACDExp = EmaExp(series, 12, curcand) - EmaExp(series, 26, curcand);

            return MACDExp;
        }

        // Метод получения EMA Exponential
        public static decimal EmaExp(CandleSeries series, int per, int curcand)
        {
            decimal EmaExp = 0;

            decimal[] EMA = new decimal[per];

            decimal SMAsum = 0;

            for (int x = (curcand + per * 2); x > curcand + per; x--)
            {
                SMAsum += series.GetCandle<TimeFrameCandle>(x).ClosePrice;
            }

            EMA[per - 1] = SMAsum / per; // series.GetCandle<TimeFrameCandle>(curcand+per-1).ClosePrice;

            for (int i = curcand + per - 2; i >= curcand; i--)
            {
                EMA[i - curcand] = (EMA[i - curcand + 1] * (per - 1) + 2 * series.GetCandle<TimeFrameCandle>(i).ClosePrice) / (per + 1);
            }

            EmaExp = EMA[0];

            return EmaExp;

        }

        //Метод получения Stohastic %K full with smoothing
        public static decimal stohKfull(CandleSeries series, int curcand, int kPer, int smooth)
        {
            decimal stohKfull = 1;

            for (int i = curcand; i < curcand + smooth; i++)
            {
                stohKfull += stohK(series, i, kPer);
            }

            stohKfull /= smooth;

            return stohKfull;

        }

        // Метод получения Stohastic %D Exponential full
        public static decimal stohDfull(CandleSeries series, int curcand, int kPer, int smooth, int dPer)
        {
            decimal stohDfull = 0;

            for (int i = curcand; i < curcand + dPer; i++)
            {
                stohDfull += stohKfull(series, i, kPer, smooth);
            }

            stohDfull /= dPer;

            return stohDfull;
        }

    }
}
