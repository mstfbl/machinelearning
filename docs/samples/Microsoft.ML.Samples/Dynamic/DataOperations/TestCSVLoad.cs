using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.ML;
using Microsoft.ML.Data;

namespace Samples.Dynamic.DataOperations
{
    class TestCSVLoad
    {
        public static void Example()
        {
            //Create MLContext
            MLContext mlContext = new MLContext();

            //Load Data
            IDataView data = mlContext.Data.LoadFromTextFile<SaleData>("../../../../flipkart_com-ecommerce_sample.csv", separatorChar: ',', hasHeader: true);

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

    public class SaleData
    {
        [LoadColumn(0)]
        public string Id { get; set; }

        [LoadColumn(1)]
        public string Timestamp { get; set; }

        [LoadColumn(2)]
        public string Url { get; set; }

        [LoadColumn(3)]
        public string Name { get; set; }

        [LoadColumn(4,14)]
		[VectorType(11)]
		public string CategoryTree { get; set; }

        [LoadColumn(15)]
        public string Pid { get; set; }

        [LoadColumn(16)]
        public string RetailPrice { get; set; }

        [LoadColumn(17)]
        public string DiscountedPrice { get; set; }

        [LoadColumn(18, 24)]
        [VectorType(7)]
        public string[] ImagePath { get; set; }

        [LoadColumn(25)]
        public string IsFkAdvantagedProduct { get; set; }

        [LoadColumn(26, 36)]
        [VectorType(11)]
        public string[] Description { get; set; }

        [LoadColumn(37)]
        public string ProductRating { get; set; }

        [LoadColumn(38)]
        public string OverallRating { get; set; }

        [LoadColumn(39)]
        public string Brand { get; set; }

        [LoadColumn(40, 59)]
        [VectorType(20)]
        public string[] Specifications { get; set; }
    }
}
