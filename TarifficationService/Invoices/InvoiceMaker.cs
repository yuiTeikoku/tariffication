using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using Syncfusion.DocIO;
using Syncfusion.DocIO.DLS;
using Syncfusion.DocToPDFConverter;
using Syncfusion.Pdf;

namespace Invoice
{
    public class InvoiceMaker
    {
        private PstgrConnection pstgrConn;// = new PstgrConnection();
        private DataTable dtAccounts;

        public InvoiceMaker() { }

        public void GenerateInfoices(EventLog logger)
        {
            pstgrConn = new PstgrConnection();
            dtAccounts = pstgrConn.GetTable(@"SELECT ac.id acc_id, 
                            u.id user_id, fio, contract_number, personal_account, contract_date, company_name, company_addr, company_unp, company_phone, code_cniitu 
                            FROM Accumulator ac
							LEFT JOIN Users u ON (ac.user_id = u.id)
							WHERE ac.user_id <> uuid_nil()");
            foreach (DataRow rowAccum in dtAccounts.Rows)
            {
                GenerateOneInvoice(rowAccum);
            }

        }

        private void GenerateOneInvoice(DataRow rowAccum)
        {            
            Guid userId = (Guid)rowAccum["user_id"];
            Guid accumId = (Guid)rowAccum["acc_id"];
            string kodUser = rowAccum["code_cniitu"].ToString();

            DateTime curDate = DateTime.Today;
            DateTime firstDayOftMonth = new DateTime(curDate.AddMonths(-1).Year, curDate.AddMonths(-1).Month, 1);
            string firstDayOfMonthStr = firstDayOftMonth.ToShortDateString();
            string lastDayOfMonthStr = firstDayOftMonth.AddMonths(1).AddDays(-1).ToShortDateString();

            DataTable childrenTariffs = pstgrConn.GetTable(String.Format(@"WITH request_cnt AS 
                            (SELECT tariff_id, COUNT(*) cnt, user_id, datetime 
                             FROM requests_history  WHERE user_id = '{0}' AND date_trunc('month', datetime) = '{1}'
                             GROUP BY tariff_id, user_id, datetime),
                              statistics AS (SELECT  t.in_group,
                                    '' child_order,
		                            t.name tname, 
		                            'услуга' usluga, 
		                            cnt, 
		                            t.cost, 
		                            cnt*t.cost costwithnds, 
		                            '20%' ndsrate, 
		                            round((cnt*t.cost / 6)::numeric, 2) nds,
		                            round((cnt*t.cost / 1.2)::numeric, 2) costnonds						
                            FROM request_cnt rh
							LEFT JOIN tariffs t ON (rh.tariff_id = t.id))
                            SELECT * FROM statistics
                            UNION
                               SELECT null,  '', 'ИТОГО:', '', SUM(cnt), null, SUM(costwithnds), '', SUM(nds), SUM(costnonds)  FROM statistics",
                               userId, firstDayOftMonth.ToString("yyyy-MM-dd")));
            // Если возвращается только строка ИТОГ - нет данных - не запускать
            // формирование счет-фактур.
            if (childrenTariffs.Rows.Count <= 1) return;

            // Если данные за отчетный месяц есть, формирует таблицу для вставки в шаблон.
            DataTable parentsTariffs = pstgrConn.GetTable("SELECT id, name FROM Tariff_groups ORDER BY sort_order");
            DataTable tResult = new DataTable();
            DataRow row;
            tResult = childrenTariffs.Clone();
            int parentsCount = parentsTariffs.Rows.Count;
            for (int i = 0; i < parentsCount; i++)
            {
                string currentParent = parentsTariffs.Rows[i]["id"].ToString();
                if (childrenTariffs.Select("in_group = " + currentParent).Length > 0)
                {
                    row = tResult.NewRow();
                    // Формирование порядкового номера в пустом столбце "child_order".
                    row["child_order"] = (i + 1).ToString();
                    row["tname"] = parentsTariffs.Rows[i]["name"].ToString();
                    tResult.Rows.Add(row);
                    DataRow[] currentRows = childrenTariffs.Select("in_group = " + currentParent);
                    for (int j = 0; j < currentRows.Length; j++)
                    {
                        currentRows[j]["child_order"] = (i + 1).ToString() + "." + (j + 1).ToString();
                        tResult.Rows.Add(currentRows[j].ItemArray);
                    }
                }
            }
            tResult.Rows.Add(childrenTariffs.Rows[childrenTariffs.Rows.Count - 1].ItemArray);
            tResult.Columns.Remove("in_group");

            WordDocument wordDoc = new WordDocument("d:\\tariffication\\" + "invoice-template.doc");

            WSection section = wordDoc.Sections[2];
            WTable docTable = section.Tables[0] as WTable;
            docTable.TableFormat.Borders.LineWidth = 0.2f;
            // Добавление пустых строк в шаблонную таблицу.
            for (int i = 0; i < tResult.Rows.Count; i++)
            {
                docTable.AddRow();
            }
            Int32 rowsCount = tResult.Rows.Count;
            // Заполнение шаблонной таблицы данными из итоговой таблицы.
            for (int r = 0; r < rowsCount; r++)
            {
                docTable.Rows[r + 1].Height = 10;
                for (int c = 0; c < tResult.Columns.Count; c++)
                {
                    string Value = tResult.Rows[r][c].ToString();
                    IWTextRange theadertext = docTable.Rows[r + 1].Cells[c].AddParagraph().AppendText(Value);
                    theadertext.CharacterFormat.FontName = "Times New Roman";
                    theadertext.CharacterFormat.FontSize = 10;
                    docTable.Rows[r].Cells[c].CellFormat.VerticalAlignment = VerticalAlignment.Middle;
                    // Стиль шрифта последней строки - полужирный.
                    if (r == rowsCount - 1)
                        theadertext.CharacterFormat.Bold = true;
                }
            }
            docTable.AutoFit(AutoFitType.FitToContent);
            // Получение текстового варианта итоговых сумм.
            string costWithNdsFull = DigsConverter.CurrencyToTxt(Convert.ToDouble(tResult.Rows[rowsCount - 1]["costwithnds"]), true);
            string ndsFull = DigsConverter.CurrencyToTxt(Convert.ToDouble(tResult.Rows[rowsCount - 1]["nds"]), true);
            // Массив названий заменяемых полей шаблона.
            string[] target = new string[] { "invNo",
                "invDate",
                "contractDate",
                "invPeriod",
                "contract_number",
                "company_name",
                "company_addr",
                "company_unp",
                "company_phone",
                "cost_with_nds",
                "cost_with_nds_txt",
                "nds",
                "nds_txt"  };
            // Массив значений для заменяемых полей шаблона.
            string[] source = new string[] {rowAccum["code_cniitu"].ToString() + "-" + GetInvSeq(userId, accumId, lastDayOfMonthStr),
                lastDayOfMonthStr,
                Convert.ToDateTime(rowAccum["contract_date"]).ToShortDateString(),
                firstDayOfMonthStr + " - " + lastDayOfMonthStr,
                rowAccum["contract_number"].ToString(),
                rowAccum["contract_number"].ToString(),
                rowAccum["company_addr"].ToString(),
                rowAccum["company_unp"].ToString(),
                rowAccum["company_phone"].ToString(),
                tResult.Rows[rowsCount - 1]["costwithnds"].ToString(),
                costWithNdsFull,
                tResult.Rows[rowsCount - 1]["nds"].ToString(),
                ndsFull      };
            // Вставляет в поля "invNo", "invDate", "contractDate", "invPeriod" ... и т.д.
            // соответствующие значения из source.
            wordDoc.MailMerge.Execute(target, source);

            // Конвертация в .pdf.
            DocToPDFConverter pdfConverter = new DocToPDFConverter();
            PdfDocument pdfDoc = pdfConverter.ConvertToPDF(wordDoc);
            pdfDoc.Save("d:\\tariffication\\" + userId.ToString() + "-" + lastDayOfMonthStr + ".pdf");
            pdfDoc.Close(true);
            wordDoc.Close();
           // wordDoc.Save(userId.ToString() + "-" + lastDayOfMonthStr + ".doc", FormatType.Word2010);
        }

        public void decUsersBalans()
        {
            pstgrConn.RunProcedure("call public.\"decBalansFromAccumulator\"()");
        }

        private string GetInvSeq(Guid userId, Guid accumId, string invoiceDate)
        {
            // Добавит строку в таблицу Invoices и вернет номер счет-фактуры.
            pstgrConn.RunProcedure("call incrfilename(@uid, @accid, @dateofreport)", new string[] { "uid", "accid", "dateofreport" }, new object[] { userId, accumId, invoiceDate });
            var dtInvSeq = pstgrConn.GetTable(String.Format("SELECT file_seq FROM Invoices WHERE accum_id = '{0}' ORDER BY invoice_date DESC LIMIT 1", accumId));
            string invSeq = dtInvSeq.Rows[0]["file_seq"].ToString();
            return invSeq ;
        }
    }
}