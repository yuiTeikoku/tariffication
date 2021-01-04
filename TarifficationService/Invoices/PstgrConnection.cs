using System;
using Npgsql;
using System.Data;

namespace Invoice
{
    class PstgrConnection
    {        
        private readonly String connectionString = @"Host=ec2-54-247-107-109.eu-west-1.compute.amazonaws.com; Port=5432; 
                                            Username=jpvkhtzpumcntk; Password=46f00d2b9d1f2ba0b171ebddc6b56868f224a6eab34d560dc7d9e90e32b08688;
                                            Database=dfe6hr1j60732b; sslmode=Require; Trust Server Certificate=true;";

        public DataTable GetTable(string queryString)
        {
            DataTable table = new DataTable();
            NpgsqlConnection pstgrConnection = new NpgsqlConnection(connectionString);
            NpgsqlDataAdapter pstgr = new NpgsqlDataAdapter(queryString, pstgrConnection);
            pstgr.Fill(table);
            return table;
        }

        public void RunProcedure(String procedureName)
        {
            NpgsqlConnection pstgrConnection = new NpgsqlConnection(connectionString);
            pstgrConnection.Open();
            NpgsqlCommand pstgrCommand = new NpgsqlCommand(procedureName, pstgrConnection);

            //using (var reader = pstgrCommand.ExecuteReader());   
            pstgrCommand.ExecuteNonQuery();
            pstgrConnection.Close();
        }


        public void RunProcedure(String procedureName, String[] paramsNames, object[] paramsValues)
        {
            NpgsqlConnection pstgrConnection = new NpgsqlConnection(connectionString);
            pstgrConnection.Open();
            NpgsqlCommand pstgrCommand = new NpgsqlCommand(procedureName, pstgrConnection);
            
            for (int i = 0; i<paramsNames.Length; i++)                
            {
                pstgrCommand.Parameters.Add(new NpgsqlParameter(paramsNames[i], paramsValues[i]));
            }
            //using (var reader = pstgrCommand.ExecuteReader());   
            pstgrCommand.ExecuteNonQuery();
            pstgrConnection.Close();
        }


        /*
        public void RunQuery(string queryString)
        {
            NpgsqlConnection pstgrConnection = new NpgsqlConnection(connectionString);
            NpgsqlCommand pstgrCommand = new NpgsqlCommand(queryString, pstgrConnection);
            pstgrCommand.ExecuteNonQuery();
        }*/
    }
}