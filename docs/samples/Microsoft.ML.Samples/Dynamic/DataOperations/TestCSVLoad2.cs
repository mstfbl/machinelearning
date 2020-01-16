using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.ML;
using Microsoft.ML.Data;

namespace Samples.Dynamic.DataOperations
{
    class TestCSVLoad2
    {
        public static void Example()
        {
            //Create MLContext
            MLContext mlContext = new MLContext();

            //Load Data
            IDataView data = mlContext.Data.LoadFromTextFile<SaleData2>("../../../../sample.csv", separatorChar: ',', hasHeader: true, allowSparse: true);

            // Create an IEnumerable of SaleData objects from IDataView
            /*IEnumerable<SaleData> housingDataEnumerable =
                mlContext.Data.CreateEnumerable<SaleData>(data, reuseRowObject: true);

            // Iterate over each row
            foreach (SaleData row in housingDataEnumerable)
            {
                // Do something (print out Size property) with current Housing Data object being evaluated
                Console.WriteLine(row.ToString());
            }*/

			DataDebuggerPreview preview = data.Preview();
			Console.WriteLine("done");

		}
    }

    public class SaleData2
    {
        [LoadColumn(0)]
        public int Id { get; set; }

        [LoadColumn(0)]
        public string Name { get; set; }

        [LoadColumn(0)]
        public float Price { get; set; }

        [LoadColumn(0)]
        public bool IisSoldOutd { get; set; }


    }
}
