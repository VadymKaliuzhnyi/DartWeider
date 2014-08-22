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
using System.Web;

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

    public partial class MainWindow : Window
    {

        // Объявление переменных
        private QuikTrader _trader;   // квик трейдер

        private TimeSpan _timeFrame;  // таймфрейм
        private TimeSpan _timeRefreshStrategy = TimeSpan.FromSeconds(1);  // частота обновления стратегии

        public DartWeiderStrategy _strategy;
        
        private Security _security; // инструмент
        private Portfolio _portfolio; // портфель

        private CandleManager _candleManager; // менеджер свечек

        private CandleSeries _series; // поток свечей

        private ChartArea _area; // область графика

        private ChartCandleElement _candlesElem;  // элемент области графика

        public MainWindow()
        {
            InitializeComponent();
                                    
            _area = new ChartArea(); // cоздание области графика
            _chart.Areas.Add(_area); // и добавление ее в _chart
            
            quikPath.Text = QuikTerminal.GetDefaultPath(); // находим путь к квику и вписываем его в поле

        }

        // Кнопка "Подключение" - Метод подключения к квику
        private void connButton_Click(object sender, RoutedEventArgs e)
        {
            // если трейдер не подключен
            if (_trader == null)
            {
                //создаем нового трейдера
                _trader = new QuikTrader(quikPath.Text);

                //Подписываемся на событие появления новых портфелей и добавляем их в ComboBox Portfolios
                _trader.NewPortfolios += portfolios => this.GuiAsync(() => { userPortfolios.ItemsSource = _trader.Portfolios; });

                //Подписываемся на событие появления новых инстументов и добавляем их в ComboBox Securities
                _trader.NewSecurities += securities => this.GuiAsync(() => { userSecurities.ItemsSource = _trader.Securities; });

                // Создание элемента графика представляющего свечки и добавление его в область графика
                _candlesElem = new ChartCandleElement();
                _area.Elements.Add(_candlesElem);
            }
            
            // подключаем квик
            _trader.Connect();

            //Начинаем Экспорт данных
            _trader.StartExport(); //получение он-лайн данных из квика Инструменты, Заявки, Портфели и так далее

            this.connButton.Background = Brushes.LightGoldenrodYellow; // меняем цвет кнопки Connect

            _candleManager = new CandleManager(_trader); // создаем менеджер свечек

            StorageRegistry storageRegistry = new StorageRegistry(); // создаем экземпляр класа-источника данных

            ((LocalMarketDataDrive)storageRegistry.DefaultDrive).Path = histPath.Text; // присваиваем ему по умолчанию путь где хранятся наши тиковые данные

            TradeStorageCandleBuilderSource cbs = new TradeStorageCandleBuilderSource { StorageRegistry = storageRegistry }; // создаем новый источник данных и присваиваем ему хранилище

            _candleManager.Sources.OfType<TimeFrameCandleBuilder>().Single().Sources.Add(cbs); // добавляем в менеджер свечек наш источник

            _candleManager.Processing += DrawCandle; // подписываемся на событие отрисовки свечей
            
        }

        // Кнопка "Запуск робота" - метод запуск робота
        private void secStartButton_Click(object sender, RoutedEventArgs e)
        {

            _timeFrame = TimeSpan.FromMinutes(Convert.ToInt32(userTimeFrame.Text));

            // проверяем подключен ли квик трейдер, задан ли портфель, инструмент
            if (_trader == null)
            {
                MessageBox.Show("Терминал не задан");
                return;
            }

            if (_trader.Portfolios == null)
            {
                MessageBox.Show("Портфель не задан");
                return;
            }

            if (_trader.Securities == null)
            {
                MessageBox.Show("Инструмент не задан");
                return;
            }

            _series = new CandleSeries(typeof(TimeFrameCandle), _security, _timeFrame); // создаем поток свечей и указываем что тип TimeFrameCandle

            _candleManager.Start(_series, DateTime.Today.AddDays(-1), DateTime.Today.AddDays(+1));  // запускаем CandleManager

            this.secStartButton.Background = Brushes.LightGoldenrodYellow; // меняем цвет кнопки StartRobo

        }

        private void StrategyStartButton_Click(object sender, RoutedEventArgs e)
        {
            _strategy = new DartWeiderStrategy(_series, this)
            {
                Security = _security,
                Portfolio = _portfolio,
                Connector = _trader,
                Volume = Convert.ToInt32(userContracts.Text),
            };

            _trader.RegisterMarketDepth(_security);
            _strategy.Start();
            this.StrategyStartButton.Background = Brushes.LightGoldenrodYellow;
        }


        // метод отрисовки свечей
        private void DrawCandle(CandleSeries series, Candle candle)
        {
            if (series == _series)
            {
                this.GuiAsync(() =>
                {
                    _chart.ProcessCandle(_candlesElem, candle);
                });
            }
        }

        // метод измеенеия инструмента в ComboBox
        private void userSecurities_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _security = _trader.Securities.First(s => s == userSecurities.SelectedItem); // присваеваем переменной _security имя  выбраного инструмента
        }

        // метод измеенеия портфеля в ComboBox
        private void userPortfolios_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _portfolio = _trader.Portfolios.FirstOrDefault(p => p == userPortfolios.SelectedItem); // присваеваем переменной _portfolio имя  выбраного портфеля
        }

        private void checkButt_Click(object sender, RoutedEventArgs e)
        {
            if (_strategy!=null)
            _strategy.checkMeth();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            FileInfo profitTXT = new FileInfo("profitTXT.ini");
            if (profitTXT.Exists)
            {
                profitTXT.Delete();
            }
            if (_strategy != null)
            {
                using (StreamWriter sw = File.CreateText("profitTXT.ini"))
                {
                    sw.Write(_strategy.profit.ToString());
                }
            }
            if (_trader!=null)
            _trader.StopExport();
            if(_trader!=null)
            _trader.Disconnect();
        }
      

    }

}
