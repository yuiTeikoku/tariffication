using Npgsql;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PaymentsParser
{
    class Payments
    {
        public DateTime statementPaymentsDate { get; set; }

        public List<Payment> payments = new List<Payment>();


        public void ParseFromFileAndUpdateDB(string filePath, EventLog logger)
        {
            // Parse payments
            Queue<int> indexesQueue = new Queue<int>();

            string[] lines = System.IO.File.ReadAllLines(filePath, Encoding.GetEncoding(1251));
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains("^Num") || lines[i].Contains("^UNN2"))
                    indexesQueue.Enqueue(i);

                /// Parse statementPaymentsDate
                if (lines[i].Contains("^Time="))
                {
                    DateTime buffDate = DateTime.Now;
                    if(DateTime.TryParse(Payment.getValueByTypeFromString(lines[i], typeof(string)), out buffDate))
                        statementPaymentsDate = buffDate;
                }
                ///
            }

            while (indexesQueue.Count > 1)
            {
                int firstIndex = indexesQueue.Dequeue();
                int countLines = indexesQueue.Dequeue() - firstIndex + 1;

                this.payments.Add(new Payment(lines.Skip(firstIndex).Take(countLines).ToArray()));
            }

            using (NpgsqlConnection connection = new NpgsqlConnection("Host=ec2-54-247-107-109.eu-west-1.compute.amazonaws.com;Port=5432;Username=jpvkhtzpumcntk;Password=46f00d2b9d1f2ba0b171ebddc6b56868f224a6eab34d560dc7d9e90e32b08688;Database=dfe6hr1j60732b;sslmode=Require;Trust Server Certificate=true;"))
            {
                connection.Open();
                foreach (Payment payment in payments)
                {
                    if (payment.CheckDuplicateInDbPaymentsHistory(connection))
                    {
                        payment.SaveInDbPaymentsHistory(connection, statementPaymentsDate);
                        payment.IncrementDbAccumulator(connection);
                    }
                }
                connection.Close();
            }

        }



        /* старые методы
        public void IncrementDbAccumulator() // вынести логику DB и Payments
        {
            using (NpgsqlConnection connection = new NpgsqlConnection("Host=ec2-54-247-107-109.eu-west-1.compute.amazonaws.com;Port=5432;Username=jpvkhtzpumcntk;Password=46f00d2b9d1f2ba0b171ebddc6b56868f224a6eab34d560dc7d9e90e32b08688;Database=dfe6hr1j60732b;sslmode=Require;Trust Server Certificate=true;"))
            {
                connection.Open();

                //Console.WriteLine(connection.State);
                foreach(Payment payment in payments)
                {
                    if ((payment.Credit ?? 0) == 0)
                        continue;

                    string qurey = $@"update accumulator 
	                                         set balans = round((balans + {(payment.Credit ?? 0).ToString(new CultureInfo("en-US"))})::numeric, 2)
	                                         where user_id = 
                                                   (select us.id from users us where us.contract_number = '{payment.Contract_number}' and us.personal_account = '{payment.Personal_account}');";
                    
                     NpgsqlCommand incrementAccum = new NpgsqlCommand(qurey, connection);

                    int dr = incrementAccum.ExecuteNonQuery();
                    
                    if (dr == 0)
                    {
                        qurey = $@"update accumulator 
	                                     set balans = round((balans + {(payment.Credit ?? 0).ToString(new CultureInfo("en-US"))})::numeric, 2)
	                                     where user_id = uuid_nil()";
                        incrementAccum = new NpgsqlCommand(qurey, connection);

                        dr = incrementAccum.ExecuteNonQuery();
                    }
                }
                connection.Close();
            }
        }

        public void SaveInDbPaymentsHistory()// вынести логику DB и Payments
        {
            using (NpgsqlConnection connection = new NpgsqlConnection("Host=ec2-54-247-107-109.eu-west-1.compute.amazonaws.com;Port=5432;Username=jpvkhtzpumcntk;Password=46f00d2b9d1f2ba0b171ebddc6b56868f224a6eab34d560dc7d9e90e32b08688;Database=dfe6hr1j60732b;sslmode=Require;Trust Server Certificate=true;"))
            {
                connection.Open();

                foreach (Payment payment in payments)
                {
                    string query = $@"INSERT INTO public.payments_history(
	                                id, credit, date, user_id, num, acc, cod, doc_date)
	                                VALUES (
		                                uuid_generate_v4(),
		                                {(payment.Credit ?? 0).ToString(new CultureInfo("en-US"))},
		                                '{statementPaymentsDate.ToString("MM.dd.yyyy HH:mm:ss")}',
		                                COALESCE((select us.id from users us where us.contract_number = '{payment.Contract_number}' and us.personal_account = '{payment.Personal_account}'), uuid_nil()),
                                        {payment.Num},
                                        '{payment.Acc}',
                                        '{payment.Cod}',
                                        '{payment.DocDate}' )";
                    NpgsqlCommand incrementAccum = new NpgsqlCommand(query, connection);

                    int dr = incrementAccum.ExecuteNonQuery();

                }
                connection.Close();
            }
        }
        */

    }
}
