using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Configuration;
using System.IO;

// using S#
using Ecng.Common;
using Ecng.Collections;
using Ecng.ComponentModel;
using Ecng.Configuration;
using Ecng.Data;
using Ecng.Data.Providers;
using Ecng.Interop;
using Ecng.Localization;
using Ecng.Net;
using Ecng.Reflection;
using Ecng.Reflection.Aspects;
using Ecng.Security;
using Ecng.Serialization;
using Ecng.Transactions;
using Ecng.UnitTesting;
using Ecng.Web;
using Ecng.Xaml;
using System.Web;

using StockSharp.Alerts;
using StockSharp.Algo;
using StockSharp.Licensing;
using StockSharp.Algo.Indicators;
using StockSharp.Algo.Indicators.Trend;
using StockSharp.Algo.Candles;
using StockSharp.Algo.Candles.Compression;
using StockSharp.Algo.Strategies;
using StockSharp.Algo.Storages;
using StockSharp.BusinessEntities;
using StockSharp.Logging;
using StockSharp.Xaml;
using StockSharp.Xaml.Charting;
using StockSharp.Xaml.Diagram;
using StockSharp.Quik;
using StockSharp.Messages;



namespace WpfApplication1
{
    public class DartWeiderStrategy : Strategy
    {
        CandleSeries _series;
        MainWindow _MainWnd;
        int _curcandle = 1; // текущая (отсчетная) свеча
        Order _order = null;
        Order _stopOrder = null;
        Order _sellOrder = null;
        public int profit = 0;
        decimal startPrice = 0;
        decimal endPrice = 0;


        public DartWeiderStrategy(CandleSeries series, MainWindow MainWnd)
        {
            _series = series;
            _MainWnd = MainWnd;

            FileInfo profitTXT = new FileInfo("profitTXT.ini");
            if (profitTXT.Exists)
            {
                using (StreamReader sr = profitTXT.OpenText())
                {
                    profit = Convert.ToInt32(sr.ReadLine());
                    _MainWnd.Dispatcher.BeginInvoke(new Action(delegate()
                    {
                        _MainWnd.profitBox.Text = profit.ToString();
                    }));
                }
            }
        }

        public void checkMeth()
        {
            string checkLine = "";

            if (_order != null)
            {
                checkLine += " _order ID: " + _order.Id.ToString() + " _order.Price: " + _order.Price.ToString()
                    + " _order.State: " + _order.State.ToString() + " _order.Time: " + _order.Time.ToString();
            }
            if (_order == null)
                checkLine += " _order == null ";

            if (_stopOrder != null)
            {
                checkLine += " _stopOrder ID: " + _stopOrder.Id.ToString() + " _stopOrder.Price: " + _stopOrder.Price.ToString()
                    + " _stopOrder.State: " + _stopOrder.State.ToString() + " _stopOrder.Time: " + _stopOrder.Time.ToString();
            }
            if (_stopOrder == null)
                checkLine += " _stopOrder == null ";

            if (_sellOrder != null)
            {
                checkLine += " _sellOrder ID: " + _sellOrder.Id.ToString() + " _sellOrder.Price: " + _sellOrder.Price.ToString()
                    + " _sellOrder.State: " + _sellOrder.State.ToString() + " _sellOrder.Time: " + _sellOrder.Time.ToString();
            }
            if (_sellOrder == null)
                checkLine += " _sellOrder == null ";

            checkLine += " int profit = : " + profit.ToString() + " startPrice: " + startPrice.ToString()
                + " endPrice: " + endPrice.ToString();

            MessageBox.Show(checkLine);
        }

        protected override void OnStarted()
        {
            // создаем правило для события - завершение каждой свечи
            _series
                .WhenCandlesFinished()
                .Do(() =>
                {
                    #region если условия входа в позицию есть

                    if (_order == null && _stopOrder == null && _sellOrder == null
                        /*
                        && DartWeiderIndicators.MACDHistExp(_series, _curcandle) > DartWeiderIndicators.MACDHistExp(_series, (_curcandle + 1)) // если MACDHistExp текущей свечи меньше чем у свечи перед ней

                        && DartWeiderIndicators.MACDHistExp(_series, _curcandle) < 0 // если MACDHistExp текущей свечи < 0

                        && DartWeiderIndicators.stohKfull(_series, _curcandle, 5, 3) > DartWeiderIndicators.stohDfull(_series, _curcandle, 5, 3, 3) // если полный стохастик %K > %D

                        && DartWeiderIndicators.stohKfull(_series, _curcandle, 5, 3) > 20 && DartWeiderIndicators.stohDfull(_series, _curcandle, 5, 3, 3) > 20 // если полные стохастики %K и %D > 20
                        */
                        )

                    #endregion

                    {
                        #region создаем заявку на покупку

                        _order = new Order
                        {
                            Portfolio = Portfolio,
                            Price = Security.BestAsk.Price + 500,
                            Security = Security,
                            Volume = Volume,
                            Direction = OrderDirections.Buy,
                        };

                        #endregion

                        #region создаем правило на случай ошибки регистрации заявки

                        _order.WhenRegisterFailed()
                            .Do(() =>
                            {
                                _MainWnd.Dispatcher.BeginInvoke(new Action(delegate()
                                    {
                                        _MainWnd._strategy.CancelActiveOrders();
                                        _MainWnd._strategy.Stop();
                                        _MainWnd.LogWind.AppendText(System.Environment.NewLine + "Не могу зарегистрировать ордер на покупку. Стратегия отключена. Все заявки отменены");
                                        _MainWnd.LogWind.ScrollToEnd();
                                    }));
                            })
                            .Apply(this);

                        #endregion

                        #region создаем правило на случай когда ордер исполнится

                        _order
                            .WhenMatched()
                            .Do(() =>
                            {

                                // пытаемся и находим цену последней сделки ордера
                                do
                                {
                                    startPrice = 0;
                                    try
                                    {
                                        startPrice = _order.GetTrades().FirstOrDefault().Trade.Price;
                                    }
                                    catch (Exception)
                                    {

                                    }
                                } while (startPrice == 0);


                                _MainWnd.Dispatcher.BeginInvoke(new Action(delegate()
                                {
                                    _MainWnd.LogWind.AppendText(System.Environment.NewLine + "Заявка на покупку исполнена: " + DateTime.Now.ToString() + " по цене: " + Convert.ToInt32(startPrice).ToString());
                                    _MainWnd.LogWind.ScrollToEnd();
                                }));

                                decimal stopPrice = startPrice - 110; // создаем стоп-цену

                                // создаем стоп-лимит ордер
                                _stopOrder = new Order
                                {
                                    Type = OrderTypes.Conditional,
                                    Volume = base.Volume,
                                    Price = Security.BestBid.Price - 500,
                                    Security = base.Security,
                                    Portfolio = base.Portfolio,
                                    Direction = OrderDirections.Sell,
                                    Condition = new QuikOrderCondition
                                    {
                                        Type = QuikOrderConditionTypes.StopLimit,
                                        StopPrice = stopPrice,
                                    },
                                };

                                #region правило на случай ошибки регистрации стоп-заявки по ордеру
                                _stopOrder.WhenRegisterFailed()
                                    .Do(() =>
                                    {
                                        _MainWnd.Dispatcher.BeginInvoke(new Action(delegate()
                                            {
                                                CancelActiveOrders();
                                                _MainWnd._strategy.Stop();
                                                _MainWnd.LogWind.AppendText(System.Environment.NewLine + "Не могу зарегистрировать стоп-ордер - выход по стопу. Стратегия отключена. Все заявки отменены");
                                                _MainWnd.LogWind.ScrollToEnd();
                                            }));
                                    })
                                    .Apply(this);
                                #endregion

                                #region правило на случай упешной регистрации заявки
                                _stopOrder
                                    .WhenRegistered()
                                    .Do(() =>
                                    {
                                        _MainWnd.Dispatcher.BeginInvoke(new Action(delegate()
                                            {
                                                _MainWnd.LogWind.AppendText(System.Environment.NewLine + "Стоп-ордер зарегистрирован: " + DateTime.Now.ToString() + ", стоп-цена: " + (stopPrice).ToString());
                                                _MainWnd.LogWind.ScrollToEnd();
                                            }));
                                    }
                                    )
                                    .Apply(this);
                                #endregion

                                #region правило на случай срабатывания стоп-ордера
                                _stopOrder
                                .WhenMatched()
                                .Do(() =>
                                {
                                    // пытаемся и находим цену срабатывания стоп-ордера
                                    endPrice = 0;
                                    do
                                    {
                                        try
                                        {
                                            endPrice = _stopOrder.DerivedOrder.GetTrades().FirstOrDefault().Trade.Price;
                                        }
                                        catch (Exception)
                                        {

                                        }
                                    } while (endPrice == 0);

                                    profit += Convert.ToInt32(endPrice - startPrice); // считаем профит по сделке

                                    _MainWnd.Dispatcher.BeginInvoke(new Action(delegate()
                                    {
                                        _MainWnd.profitBox.Text = profit.ToString();
                                        _MainWnd.LogWind.AppendText(System.Environment.NewLine + "Выход по стопу: " + DateTime.Now.ToString() + " Цена: " + Convert.ToInt32(endPrice).ToString() + " Профит по сделке: " + Convert.ToInt32(endPrice - startPrice).ToString());
                                        _MainWnd.LogWind.ScrollToEnd();
                                    }));

                                    _order = null;
                                    _stopOrder = null;

                                })
                                .Apply(this);
                                #endregion

                                // регистрируем стоп-лимит ордер
                                RegisterOrder(_stopOrder);
                                
                            })
                            .Apply(this);
                        #endregion

                        // регистрируем Ордер
                        RegisterOrder(_order);

                    }

                    #region если мы уже в позе и рынок говорит что ее нужно закрывать

                    else if (_order != null && _stopOrder != null && _sellOrder == null

                && DartWeiderIndicators.stohKfull(_series, _curcandle, 5, 3) < DartWeiderIndicators.stohDfull(_series, _curcandle, 5, 3, 3) // если полный стохастик %K < %D

                && DartWeiderIndicators.stohKfull(_series, _curcandle + 1, 5, 3) > DartWeiderIndicators.stohDfull(_series, _curcandle + 1, 5, 3, 3) // если полный стохастик %K + 1 < %D + 1

                && DartWeiderIndicators.stohKfull(_series, _curcandle, 5, 3) < 50 && DartWeiderIndicators.stohDfull(_series, _curcandle, 5, 3, 3) < 50 // если полные стохастики %K и %D < 50

                && DartWeiderIndicators.stohKfull(_series, _curcandle + 1, 5, 3) < 50 && DartWeiderIndicators.stohDfull(_series, _curcandle + 1, 5, 3, 3) < 50 // если полные стохастики %K + 1 и %D + 1 < 50

                        )

                    #endregion

                    {

                        #region создаем правило - ошибка снятия стоп заявки - выход по рынку

                        _stopOrder.WhenCancelFailed()
                         .Do(() =>
                         {
                         _MainWnd.Dispatcher.BeginInvoke(new Action(delegate()
                         {
                          _MainWnd._strategy.Stop();
                          _MainWnd.LogWind.AppendText(System.Environment.NewLine + "НЕ МОГУ снять стоп-заявку - выход по рынку. Стратегия отключена.");
                         _MainWnd.LogWind.ScrollToEnd();
                         }));
                         })
                            .Apply(this);

                        #endregion

                        CancelOrder(_stopOrder); // отменяем стоп-заявку

                        #region создаем заявку на продажу
                        _sellOrder = new Order
                        {
                            Portfolio = Portfolio,
                            Price = Security.BestBid.Price - 500,
                            Security = Security,
                            Volume = Volume,
                            Direction = OrderDirections.Sell,
                        };
                        #endregion

                        #region создаем правило - ошибка регистрации заявки на продажу
                        _sellOrder.WhenRegisterFailed()
                            .Do(() =>
                            {
                                _MainWnd.Dispatcher.BeginInvoke(new Action(delegate()
                                    {
                                        _MainWnd._strategy.CancelActiveOrders();
                                        _MainWnd._strategy.Stop();
                                        _MainWnd.LogWind.AppendText(System.Environment.NewLine + "Не могу зарегистрировать ордер на продажу - выход по рынку. Стратегия отключена. Все заявки отменены");
                                        _MainWnd.LogWind.ScrollToEnd();
                                    }));
                            })
                            .Apply(this);
                        #endregion

                        #region создаем правило успешной регистрации заявки на продажу - выход по рынку
                        _sellOrder
                            .WhenMatched()
                            .Do(() =>
                            {
                                endPrice = 0;
                                
                                // пытаемся и находим последнюю цену сделки на продажу
                                try
                                {
                                    endPrice = _sellOrder.GetTrades().FirstOrDefault().Trade.Price;
                                }
                                catch (Exception)
                                {

                                } while (endPrice == 0) ;

                                profit += Convert.ToInt32(endPrice - startPrice);

                                _MainWnd.Dispatcher.BeginInvoke(new Action(delegate()
                                {
                                    _MainWnd.profitBox.Text = profit.ToString();
                                    _MainWnd.LogWind.AppendText(System.Environment.NewLine + "Выход по рынку: " + DateTime.Now.ToString() + " Цена: " + Convert.ToInt32(endPrice).ToString() + " Профит по сделке: " + Convert.ToInt32(endPrice - startPrice).ToString());
                                    _MainWnd.LogWind.ScrollToEnd();
                                }));

                                _order = null;
                                _stopOrder = null;
                                _sellOrder = null;

                            })
                            .Apply(this);
                        #endregion

                        RegisterOrder(_sellOrder); // регистрируем заявку на продажу

                    }

                })
                .Apply(this);


            #region правило - завершение каждой свечи - индикаторы внизу
            _series
            .WhenCandlesFinished()
            .Do(() =>
            {

                _MainWnd.Dispatcher.BeginInvoke(new Action(delegate()
                {
                    _MainWnd.MACDOldlabel.Content = "MACD Old: " + Math.Round(DartWeiderIndicators.MACDHistExp(_series, _curcandle + 1), 2).ToString();
                    _MainWnd.MACDlabel.Content = "MACD: " + Math.Round(DartWeiderIndicators.MACDHistExp(_series, _curcandle), 2).ToString();
                    _MainWnd.StohKlabel.Content = "StohK: " + Math.Round(DartWeiderIndicators.stohKfull(_series, _curcandle, 5, 3), 2).ToString();
                    _MainWnd.StohDlabel.Content = "StohD: " + Math.Round(DartWeiderIndicators.stohDfull(_series, _curcandle, 5, 3, 3), 2).ToString();
                }));

            })
            .Apply(this);
            #endregion


            base.OnStarted();
        }


    }
}
