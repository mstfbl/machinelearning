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

            DataDebuggerPreview preview = data.Preview();

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

        [LoadColumn(8)]
        public string ImagePath { get; set; }

        [LoadColumn(9)]
        public Boolean IsFkAdvantagedProduct { get; set; }

        [LoadColumn(10)]
        public string Description { get; set; }

        [LoadColumn(11)]
        public string ProductRating { get; set; }

        [LoadColumn(12)]
        public string OverallRating { get; set; }

        [LoadColumn(13)]
        public string Brand { get; set; }

        [LoadColumn(14)]
        public string Specifications { get; set; }
    }
}
