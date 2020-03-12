﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Threading;
using Microsoft.ML.Data;
using Microsoft.ML.Internal.Utilities;
using Microsoft.ML.Runtime;

namespace Microsoft.ML.AutoML.Test
{
    internal static class DatasetUtil
    {
        public const string UciAdultLabel = DefaultColumnNames.Label;
        public const string TrivialMulticlassDatasetLabel = "Target";
        public const string MlNetGeneratedRegressionLabel = "target";
        public const int IrisDatasetLabelColIndex = 0;

        public static string TrivialMulticlassDatasetPath = Path.Combine("TestData", "TrivialMulticlassDataset.txt");

        private static IDataView _uciAdultDataView;

        public static IDataView GetUciAdultDataView()
        {
            if (_uciAdultDataView == null)
            {
                var context = new MLContext(1);
                var uciAdultDataFile = DownloadUciAdultDataset(context);
                var columnInferenceResult = context.Auto().InferColumns(uciAdultDataFile, UciAdultLabel);
                var textLoader = context.Data.CreateTextLoader(columnInferenceResult.TextLoaderOptions);
                _uciAdultDataView = textLoader.Load(uciAdultDataFile);
            }
            return _uciAdultDataView;
        }

        // downloads the UCI Adult dataset from the ML.Net repo
        public static string DownloadUciAdultDataset(IHostEnvironment env) =>
            DownloadIfNotExists(env, "https://raw.githubusercontent.com/dotnet/machinelearning/f0e639af5ffdc839aae8e65d19b5a9a1f0db634a/test/data/adult.tiny.with-schema.txt", "uciadult.dataset");

        public static string DownloadMlNetGeneratedRegressionDataset(IHostEnvironment env) =>
            DownloadIfNotExists(env, "https://raw.githubusercontent.com/dotnet/machinelearning/e78971ea6fd736038b4c355b840e5cbabae8cb55/test/data/generated_regression_dataset.csv", "mlnet_generated_regression.dataset");

        public static string DownloadIrisDataset(IHostEnvironment env) =>
            DownloadIfNotExists(env, "https://raw.githubusercontent.com/dotnet/machinelearning/54596ac/test/data/iris.txt", "iris.dataset");

        private static string DownloadIfNotExists(IHostEnvironment env, string baseGitPathUrl, string dataFile)
        {
            using (var ch = env.Start("Ensuring meta files are present."))
            {
                int timeout = 60 * 1000; // 1 minute timeout
                var ensureModel = ResourceManagerUtils.Instance.EnsureResourceAsync(env, ch, baseGitPathUrl, dataFile, Path.GetTempPath(), timeout);
                ensureModel.Wait();
                var errorResult = ResourceManagerUtils.GetErrorMessage(out var errorMessage, ensureModel.Result);
                if (errorResult != null)
                {
                    var directory = Path.GetDirectoryName(errorResult.FileName);
                    var name = Path.GetFileName(errorResult.FileName);
                    throw ch.Except($"{errorMessage}\n file could not be downloaded.");
                }
                else
                {
                    return Path.GetFileName(ensureModel.Result.FileName);
                }
            }
        }

        public static bool IsFileAvailableForAccess(string filePath)
        {
            FileInfo fileInfo = new FileInfo(filePath);
            try
            {
                using FileStream stream = fileInfo.Open(FileMode.Open, FileAccess.Read, FileShare.None);
                stream.Close();
            }

            catch (IOException)
            {
                return false;
            }

            return true;
        }

        public static string GetFlowersDataset()
        {
            const string datasetName = @"flowers";
            string assetsRelativePath = @"assets";
            string assetsPath = GetAbsolutePath(assetsRelativePath);
            string imagesDownloadFolderPath = Path.Combine(assetsPath, "inputs",
                "images");

            //Download the image set and unzip
            string finalImagesFolderName = DownloadImageSet(
                imagesDownloadFolderPath);

            string fullImagesetFolderPath = Path.Combine(
                imagesDownloadFolderPath, finalImagesFolderName);

            var images = LoadImagesFromDirectory(folder: fullImagesetFolderPath);

            using (StreamWriter file = new StreamWriter(datasetName))
            {
                file.WriteLine("Label,ImagePath");
                foreach (var image in images)
                    file.WriteLine(image.Label + "," + image.ImagePath);
            }

            return datasetName;
        }

        public static IEnumerable<ImageData> LoadImagesFromDirectory(string folder)
        {
            var files = Directory.GetFiles(folder, "*",
                searchOption: SearchOption.AllDirectories);
            /*
             * This is only needed as Linux can produce files in a different 
             * order than other OSes. As this is a test case we want to maintain
             * consistent accuracy across all OSes, so we sort to remove this discrepancy.
             */
            Array.Sort(files);
            foreach (var file in files)
            {
                var extension = Path.GetExtension(file).ToLower();
                if (extension != ".jpg" &&
                    extension != ".jpeg" &&
                    extension != ".png" &&
                    extension != ".gif"
                )
                    continue;

                var label = Path.GetFileName(file);
                label = Directory.GetParent(file).Name;
                yield return new ImageData()
                {
                    ImagePath = file,
                    Label = label
                };

            }
        }

        public static string DownloadImageSet(string imagesDownloadFolder)
        {
            string fileName = "flower_photos_tiny_set_for_unit_tests.zip";
            string url = $"https://aka.ms/mlnet-resources/datasets/flower_photos_tiny_set_for_unit_test.zip";

            Download(url, imagesDownloadFolder, fileName);
            UnZip(Path.Combine(imagesDownloadFolder, fileName), imagesDownloadFolder);

            return Path.GetFileNameWithoutExtension(fileName);
        }

        private static void Download(string url, string destDir, string destFileName)
        {
            if (destFileName == null)
                destFileName = Path.GetFileName(new Uri(url).AbsolutePath); ;

            Directory.CreateDirectory(destDir);

            string relativeFilePath = Path.Combine(destDir, destFileName);

            if (File.Exists(relativeFilePath))
                return;

            new WebClient().DownloadFile(url, relativeFilePath);
            return;
        }

        private static void UnZip(String gzArchiveName, String destFolder)
        {
            var flag = gzArchiveName.Split(Path.DirectorySeparatorChar)
                .Last()
                .Split('.')
                .First() + ".bin";

            if (File.Exists(Path.Combine(destFolder, flag)))
                return;

            ZipFile.ExtractToDirectory(gzArchiveName, destFolder);
            File.Create(Path.Combine(destFolder, flag));
        }

        public static string GetAbsolutePath(string relativePath) =>
            Path.Combine(new FileInfo(typeof(
                DatasetUtil).Assembly.Location).Directory.FullName, relativePath);

        public class ImageData
        {
            [LoadColumn(0)]
            public string ImagePath;

            [LoadColumn(1)]
            public string Label;
        }
    }
}
