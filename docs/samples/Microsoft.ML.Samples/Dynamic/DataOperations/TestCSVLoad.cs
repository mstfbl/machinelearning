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
            IEnumerable<SaleData> housingDataEnumerable =
                mlContext.Data.CreateEnumerable<SaleData>(data, reuseRowObject: true);

            // Iterate over each row
            foreach (SaleData row in housingDataEnumerable)
            {
                // Do something (print out Size property) with current Housing Data object being evaluated
                Console.WriteLine(row.ToString());
            }
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

        [LoadColumn(4)]
        public string CategoryTree { get; set; }

        [LoadColumn(5)]
        public string Pid { get; set; }

        [LoadColumn(6)]
        public string RetailPrice { get; set; }

        [LoadColumn(7)]
        public string DiscountedPrice { get; set; }

        [LoadColumn(8, 14)]
        [VectorType(7)]
        public string[] ImagePath { get; set; }

        [LoadColumn(15)]
        public bool IsFkAdvantagedProduct { get; set; }

        [LoadColumn(16, 26)]
        [VectorType(11)]
        public string[] Description { get; set; }

        [LoadColumn(27)]
        public string ProductRating { get; set; }

        [LoadColumn(28)]
        public string OverallRating { get; set; }

        [LoadColumn(29)]
        public string Brand { get; set; }

        [LoadColumn(30, 49)]
        [VectorType(20)]
        public string[] Specifications { get; set; }
    }
}
