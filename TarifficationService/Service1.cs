using System;
using System.ServiceProcess;
using System.Diagnostics;
using System.Threading;
using Invoice;
using PaymentsParser;

namespace TarifficationService
{
    public partial class Service1 : ServiceBase
    {
        private EventLog logger;
        private Timer timer_task1;
        private Timer timer_task2;
        private Timer timer_task3;

        public Service1()
        {
            InitializeComponent();
            this.AutoLog = false;

            logger = new EventLog();
            if (!EventLog.SourceExists("TarifficationLoggs"))
            {
                EventLog.CreateEventSource("TarifficationLoggs", "TarifficationLogger");
            }
            logger.Source = "TarifficationLoggs";
            logger.Log = "TarifficationLogger";

            timer_task1 = new Timer(new TimerCallback(OnTimer_Task1));
            timer_task2 = new Timer(new TimerCallback(OnTimer_Task2));
            timer_task3 = new Timer(new TimerCallback(OnTimer_Task3));

            timer_task1.Change(dueTimer(nextDay()), -1); // раз в день
            timer_task2.Change(dueTimer(nextHour()), -1); // каждый день в 12 и 23
            timer_task3.Change(dueTimer(nextDay()), -1); // раз в день
        }

        protected override void OnStart(string[] args)
        {
            logger.WriteEntry("Начало " + DateTime.Now.ToString());
        }

        private void OnTimer_Task1(object state)
        {
            if (DateTime.Now.Day == 1)
            {
                logger.WriteEntry($"Вызова события 1.");
                InvoiceMaker(logger);
                logger.WriteEntry($"Событие успешно завершено.");
            }
            timer_task1.Change(dueTimer(nextDay()), -1);
        }

        private void OnTimer_Task2(object state)
        {
            logger.WriteEntry($"Вызова события 2.");
            PaymentParser(logger);
            logger.WriteEntry($"Событие успешно завершено.");
            timer_task2.Change(dueTimer(nextHour()), -1);
        }
        
        private void OnTimer_Task3(object state)
        {
            if (DateTime.Now.Day > 10)
            {
                logger.WriteEntry($"Вызова события 3.");
                // селект всех у кого баланс меньше 0
                // отдать на API (?)
                logger.WriteEntry($"Событие успешно завершено.");
            }
            timer_task3.Change(dueTimer(nextDay()), -1);
        }

        public void InvoiceMaker(EventLog logger)
        {
            InvoiceMaker invMaker = new InvoiceMaker();
            invMaker.GenerateInfoices(logger);
            logger.WriteEntry("Счет-фактуры сгенерированы.");

            invMaker.decUsersBalans();
            logger.WriteEntry("Вызов процедуры вычета стоимости из аккумулятора пользователей.");
        }

        public void PaymentParser(EventLog logger)
        {
            Payments payments = new Payments();
            payments.ParseFromFileAndUpdateDB(
                @"d:\\tariffication\statement_of_payments\16_export_Белинвест_with_Conract_and_PepsonalNum.txt",
                logger);
            logger.WriteEntry("Выписка от " + payments.statementPaymentsDate.ToString());
        }

        public DateTime nextHour()
        {
            DateTime now = DateTime.Now;
            int hh = now.Hour;

            if (hh < 12)
                return new DateTime(now.Year, now.Month, now.Day, 12, 0, 0);
            else if (hh < 23)
                return new DateTime(now.Year, now.Month, now.Day, 23, 0, 0);    
            else
                return nextDay().AddHours(12);
        }

        public DateTime nextPerMinute(int mm) // x э (0, 60]
        {
            DateTime now = DateTime.Now;
            return now.AddMinutes(mm - now.Minute % mm).AddSeconds(-now.Second);
        }

        public DateTime nextDay()
        {
            DateTime res = DateTime.Now.AddDays(1);
            return new DateTime(res.Year, res.Month, res.Day, 0, 0, 0);
        }

        public int dueTimer(DateTime eventTime)
        {
            DateTime now = DateTime.Now;
            TimeSpan relativeTime = eventTime.Subtract(now);
            return (int) relativeTime.TotalMilliseconds;
        }

        protected override void OnStop()
        {
            logger.WriteEntry($"Сервис был остановлен: {DateTime.Now.ToString()}");
            timer_task1.Dispose();
            timer_task2.Dispose();
            timer_task3.Dispose();
        }
    }
}
