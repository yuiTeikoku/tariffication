using Npgsql;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PaymentsParser
{
    //for Belinvest
    public class Payment       
    {

        public Payment(string[] paymentLinesArray) // оработать ошибки
        {
            PropertyInfo[] fields = typeof(Payment).GetProperties();
            foreach (PropertyInfo i in fields)
            {
                foreach(string paymentString in paymentLinesArray)
                {
                    if (paymentString.Contains(string.Format($"^{i.Name}=")))
                    {
                        i.SetValue(this, getValueByTypeFromString(paymentString, i.PropertyType));
                        break;
                    }
                }
               
            }
            if (Nazn != null)
            {
                Contract_number = ParseContractNumber(Nazn);
                Personal_account = ParsePersonalAccount(Nazn);
            }

        }
        public Payment()
        {

        }

        public string Contract_number { get; set; }          //=Номер договора
        public string Personal_account { get; set; }          //=Лицевой счет


        public int?   Num {get; set;}              //=5236283^
        public string DocDate {get; set;}          //=04.12.2018^
        public string KorName {get; set;}          //=ЗАО МТБАНК ДЕБИТ,КРЕДИТ.ЗАДОЛЖЕН. (ЧАСТ.Ф.С.)^
        public string Nazn {get; set;}             //=ОПЛАТА ЗА АРЕНДУ СКЛАДА(ПР.ПАРТИЗАНСКИЙ,2) ЗА ДЕКАБРЬ 2018  ПО ДОГ. 1 А/11 ОТ 01.03.11^   !!!
        public string Cod {get; set;}              //= MTBKBY22 ^
        public string BankName {get; set;}         //= Г.МИНСК, ЗАО "МТБАНК"^
        public string Acc {get; set;}              //= BY44MTBK65300000000000000443 ^
        public double? Db {get; set;}              //= 0 ^
        public double? DbS {get; set;}             //= 0.00 ^
        public double? Credit {get; set;}          //= 214.26 ^ !!!
        public double? CreditS {get; set;}         //= 214.26 ^
        public double? VidDoc {get; set;}          //= 1 ^
        public string QueuePay {get; set;}         //= 00 ^
        public string StrVneshOper {get; set;}     //= ^
        public string CodPayInBudget {get; set;}   //= ^
        public string CodReportDoc {get; set;}     //= ^
        public string UNNRec {get; set;}           //= 100394906 ^
        public string UNN2 {get; set;}             //= ^


        // not used
        Payment PaymentFrom_MultilineText(string paymentText)
        {
            return new Payment();
        }
        // not used
        Payment PaymentFrom_StringsArray(string[] paymentLinesArray)
        {
            return new Payment();
        }

        public static string ParsePersonalAccount(string nazn)
        {
            if(Regex.IsMatch(nazn, @"\d{13}"))
            {
                return Regex.Match(nazn, @"\d{13}").Value;
            }

            return null;
        }
        public static string ParseContractNumber(string nazn)
        {
            if (Regex.IsMatch(nazn, @"\d{8}\s?_\s?\d{4}"))
            {
                return Regex.Match(nazn, @"\d{8}\s?_\s?\d{4}").Value;
            }

            return null;
        }



        public static dynamic getValueByTypeFromString(string input, Type type)
        {


            string replacement1 = "$1";

            input = Regex.Replace(input, @"\^\w+=([^\^]*)\^", replacement1);
            //try
            //{
                if (type == typeof(int?))
                    return Convert.ToInt32(input); // проверить на возможны ошибки. TryParse мб

                else if (type == typeof(double?))
                {
                    IFormatProvider formatter = new NumberFormatInfo { NumberDecimalSeparator = "." };
                    return double.Parse(input, formatter); // проверить на возможны ошибки. TryParse мб
                }
                   
                else
                    return input;
            //}
            //catch (Exception e)
            //{
            //    return null;
            //}
        }



        public void IncrementDbAccumulator(NpgsqlConnection connection)
        {

            if ((this.Credit ?? 0) == 0)
                return;

            string qurey = $@"update accumulator 
	                                 set balans = round((balans + {(this.Credit ?? 0).ToString(new CultureInfo("en-US"))})::numeric, 2)
	                                 where user_id = 
                                           (select us.id from users us where us.contract_number = '{this.Contract_number}' and us.personal_account = '{this.Personal_account}');";

            NpgsqlCommand incrementAccum = new NpgsqlCommand(qurey, connection);

            int dr = incrementAccum.ExecuteNonQuery();

            if (dr == 0)
            {
                qurey = $@"update accumulator 
	                             set balans = round((balans + {(this.Credit ?? 0).ToString(new CultureInfo("en-US"))})::numeric, 2)
	                             where user_id = uuid_nil()";
                incrementAccum = new NpgsqlCommand(qurey, connection);

                dr = incrementAccum.ExecuteNonQuery();
            }
        }

        public void SaveInDbPaymentsHistory(NpgsqlConnection connection, DateTime statementPaymentsDate)// вынести логику DB и Payments
        {

            string query = $@"INSERT INTO public.payments_history(
	                        id, credit, date, user_id, num, acc, cod, doc_date)
	                        VALUES (
		                        uuid_generate_v4(),
		                        {(this.Credit ?? 0).ToString(new CultureInfo("en-US"))},
		                        '{statementPaymentsDate.ToString("MM.dd.yyyy HH:mm:ss")}',
		                        COALESCE((select us.id from users us where us.contract_number = '{this.Contract_number}' and us.personal_account = '{this.Personal_account}'), uuid_nil()),
                                {this.Num},
                                '{this.Acc}',
                                '{this.Cod}',
                                '{this.DocDate}' )";
            NpgsqlCommand incrementAccum = new NpgsqlCommand(query, connection);

            int dr = incrementAccum.ExecuteNonQuery();

                
        }


        public bool CheckDuplicateInDbPaymentsHistory(NpgsqlConnection connection)// вынести логику DB и Payments
        {

            string query = $@"select count(*) from payments_history where 
                                            num = {Num} and
                                            doc_date = '{DocDate}' and
                                            acc = '{Acc}' and
                                            cod = '{Cod}' and
                                            credit = {(this.Credit ?? 0).ToString(new CultureInfo("en-US"))};";
            NpgsqlCommand incrementAccum = new NpgsqlCommand(query, connection);

            int dr = Convert.ToInt32(incrementAccum.ExecuteScalar());

            return !(dr > 0);

        }
    }
}
