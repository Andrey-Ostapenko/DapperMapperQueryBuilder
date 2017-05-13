using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SqlClient;
using Dapper;
using System.IO;
using OfficeOpenXml;
using System.Diagnostics;

namespace Mapper.Tests
{
    [TestClass()]
    public class DapperMapperTests
    {
        public string conString = @"Data Source = (LocalDB)\MSSQLLocalDB; AttachDbFilename = e:\AdConta\PruebaDapper\PruebaDapper\PruebaDapperDB.mdf; Integrated Security = True";

        public void DeleteDB()
        {
            using (SqlConnection con = new SqlConnection(conString))
            {
                con.Open();
                con.QueryMultiple(@"
DELETE FROM Persons
DELETE FROM Person
DELETE FROM Acc
DELETE FROM pruebaif
DBCC CHECKIDENT (Persons, RESEED, 0)
DBCC CHECKIDENT (Person, RESEED, 0)
DBCC CHECKIDENT (Acc, RESEED, 0)
DBCC CHECKIDENT (pruebaif, RESEED, 0)");
                con.Close();
            }
        }

        public void InsertDB()
        {
            string[] accs = { "uno", "dos", "tres", "cuatro", "cinco", "seis", "siete", "ocho", "nueve" };

            using (SqlConnection con = new SqlConnection(conString))
            {
                con.Open();
                StringBuilder sb = new StringBuilder();
                //sb.Append("START TRANSACTION;");

                for (int i = 1; i < 9; i++)
                {
                    for (int j = 0; j < accs.Count(); j++)
                    {
                        sb.Append(Environment.NewLine);
                        sb.Append($"INSERT INTO Acc (Acc,Persona) VALUES ('{accs[j]}',{i});");
                    }

                    for (int k = 0; k < 9; k++)
                    {
                        sb.Append(Environment.NewLine);
                        sb.Append($"INSERT INTO pruebaif (Persona,pruebaI, otro) VALUES ({i}, {k}, {k});");
                    }

                    sb.Append(Environment.NewLine);
                    sb.Append($"INSERT INTO Persons (Nombre) VALUES ('n{i}');");
                }

                //sb.Append("COMMIT;");
                con.QueryMultiple(sb.ToString());
                con.Close();
            }
        }

        public void MapperTest<T>(int testsTotal, int from, int every, int idAlias = 0, bool deleteFirst = true)
        {
            //if (deleteFirst)
            //{
            //    using (SqlConnection con = new SqlConnection(conString))//(MySqlConnection con = new MySqlConnection(conString))
            //    {
            //        con.Open();
            //        var result = con.Query("DELETE FROM MapperResults");
            //        con.Close();
            //    }
            //}
            Type t = typeof(T);
            string filepath = @"E:\TESTMAPPER." + t.Name + ".xlsx";
            if (deleteFirst && File.Exists(filepath))
                File.Delete(filepath);

            var file = new FileInfo(filepath);
            var package = new ExcelPackage(file);
            ExcelWorksheet wSheet = package.Workbook.Worksheets.Add("prueba1");

            wSheet.Cells[1, 1].Value = $"MAPPERTEST:  MAP {t.Name} ; Tests: { testsTotal.ToString()}. Desde: { from.ToString()}. Cada: { every.ToString()}.";
            wSheet.Cells[2, 1].Value = "ID";
            wSheet.Cells[2, 2].Value = "TYPE";
            wSheet.Cells[2, 3].Value = "OBJECTS CREATED";
            wSheet.Cells[2, 4].Value = "SECONDS";
            wSheet.Cells[2, 5].Value = "MILISECONDS";
            wSheet.Cells[2, 6].Value = "SECONDS/OBJECTS";

            Console.WriteLine("MAPPERTEST:");
            Console.WriteLine($@"    1.- MAP {t.Name}
        tests: {testsTotal.ToString()}.
        desde: {from.ToString()}.
        cada: {every.ToString()}.");
            Console.WriteLine();
            Console.WriteLine("    -WAITING-");
            Console.ReadLine();
            Console.WriteLine("------------------");
            Console.WriteLine();

            for (int j = from, x = 0; j < (testsTotal * every); j += every, x++)
            {
                Console.WriteLine("...");
                Console.WriteLine($"    Test Nº {x.ToString()}.");
                Console.WriteLine($"    Nº objetos a crear: {j.ToString()}");
                Console.WriteLine();
                Console.WriteLine("...");

                using (SqlConnection con = new SqlConnection(conString))//(MySqlConnection con = new MySqlConnection(conString))
                {
                    con.Open();
                    var result = con.Query(@"SELECT p.Id,p.Nombre,pif.pruebaI,pif.otro,a.Id aId,a.Acc aAcc,a.Persona aPersona 
FROM Persons p 
RIGHT JOIN Acc a ON p.Id=a.Persona 
RIGHT JOIN pruebaif pif ON p.Id=pif.Persona
WHERE p.Id=1");

                    Stopwatch watch = new Stopwatch();
                    watch.Reset();
                    watch.Start();

                    for (int i = 0; i < j; i++)
                    {
                        T p;
                        MapperStore store = new MapperStore();
                        DapperMapper<T> mapper = (DapperMapper<T>)store.GetMapper(t);
                        p = mapper.Map(result);
                    }

                    watch.Stop();
                    Console.WriteLine($"    +++++++++TEST {t.Name} - {x.ToString()} DONE+++++++++");
                    Console.WriteLine();
                    Console.WriteLine($"    TIME WATCH: {watch.Elapsed.ToString()}");
                    Console.WriteLine($"    TIME WATCH SECONDS: {watch.Elapsed.Seconds.ToString()}");
                    Console.WriteLine($"    TIME WATCH MILISECONDS: {watch.ElapsedMilliseconds.ToString()}");
                    Console.WriteLine();

                    wSheet.Cells[x + 3, 1].Value = x + idAlias;
                    wSheet.Cells[x + 3, 2].Value = t.Name;
                    wSheet.Cells[x + 3, 3].Value = j;
                    wSheet.Cells[x + 3, 4].Value = watch.Elapsed.Seconds;
                    wSheet.Cells[x + 3, 5].Value = watch.ElapsedMilliseconds;
                    wSheet.Cells[x + 3, 6].Formula = "D3/C3";

                    //result = con.Query("INSERT INTO MapperResults VALUES (@Id, @TYPE, @N, @SECONDS, @MILISECONDS)",
                    //    new { Id = x + idAlias, TYPE = t.Name, N = j, SECONDS = watch.Elapsed.Seconds, MILISECONDS = watch.ElapsedMilliseconds });
                    con.Close();
                    /*
                    [TYPE] VARCHAR(MAX) NOT NULL, 
    [N] BIGINT NOT NULL, 
    [SECONDS] BIGINT NOT NULL, 
    [MILISECONDS] BIGINT NOT NULL
                    */
                }
                Console.WriteLine("------------------");
                Console.WriteLine("DB OK");
                Console.WriteLine();
                //Console.WriteLine("    RESULTS:");
                //Console.ReadLine();
                //Console.WriteLine(SQLResults.FlattenStringsList());
            }

            package.Save();
            Console.WriteLine("    +++++++++ALL TESTS DONE+++++++++");
            Console.WriteLine();
            Console.ReadLine();
        }

        [TestMethod()]
        public void MapTest()
        {
            DeleteDB();
            InsertDB();


        }
    }
}