// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Timers;
using Microsoft.ML.Data;
using Microsoft.ML.Runtime;

namespace Microsoft.ML.AutoML
{
    internal class Experiment<TRunDetail, TMetrics> where TRunDetail : RunDetail
    {
        private readonly MLContext _context;
        private readonly OptimizingMetricInfo _optimizingMetricInfo;
        private readonly TaskKind _task;
        private readonly IProgress<TRunDetail> _progressCallback;
        private readonly ExperimentSettings _experimentSettings;
        private readonly IMetricsAgent<TMetrics> _metricsAgent;
        private readonly IEnumerable<TrainerName> _trainerAllowList;
        private readonly DirectoryInfo _modelDirectory;
        private readonly DatasetColumnInfo[] _datasetColumnInfo;
        private readonly IRunner<TRunDetail> _runner;
        private readonly IList<SuggestedPipelineRunDetail> _history;
        private readonly IChannel _logger;
        private bool _experimentTimerExpired;
        private HashSet<MLContext> _activeMLContexts;

        public Experiment(MLContext context,
            TaskKind task,
            OptimizingMetricInfo metricInfo,
            IProgress<TRunDetail> progressCallback,
            ExperimentSettings experimentSettings,
            IMetricsAgent<TMetrics> metricsAgent,
            IEnumerable<TrainerName> trainerAllowList,
            DatasetColumnInfo[] datasetColumnInfo,
            IRunner<TRunDetail> runner,
            IChannel logger)
        {
            _context = context;
            _history = new List<SuggestedPipelineRunDetail>();
            _optimizingMetricInfo = metricInfo;
            _task = task;
            _progressCallback = progressCallback;
            _experimentSettings = experimentSettings;
            _metricsAgent = metricsAgent;
            _trainerAllowList = trainerAllowList;
            _modelDirectory = GetModelDirectory(_experimentSettings.CacheDirectory);
            _datasetColumnInfo = datasetColumnInfo;
            _runner = runner;
            _logger = logger;
            _experimentTimerExpired = false;
            _activeMLContexts = new HashSet<MLContext>();
        }

        private void MaxExperimentTimeExpiredEvent(object sender, EventArgs e)
        {
            // If at least one model was run, end experiment immediately.
            // Else, wait for first model to run before experiment is concluded.
            _experimentTimerExpired = true;
            if (_history.Any(r => r.RunSucceeded))
            {
                _logger.Warning("Allocated time for Experiment of {0} seconds has elapsed with {1} models run. Ending experiment...",
                    _experimentSettings.MaxExperimentTimeInSeconds, _history.Count());
                foreach(MLContext c in _activeMLContexts)
                    c.CancelExecution();
            }
            _activeMLContexts.Clear();
        }

        public IList<TRunDetail> Execute()
        {
            var iterationResults = new List<TRunDetail>();
            // Create a timer for the max duration of experiment. When given time has
            // elapsed, MaxExperimentTimeExpiredEvent is called to interrupt training
            // of current model. Timer is not used if no experiment time is given, or
            // is not a positive number.
            if (_experimentSettings.MaxExperimentTimeInSeconds > 0)
            {
                Timer timer = new Timer(_experimentSettings.MaxExperimentTimeInSeconds * 1000);
                timer.Elapsed += MaxExperimentTimeExpiredEvent;
                timer.AutoReset = false;
                timer.Enabled = true;
            }
            // If given max duration of experiment is 0, only 1 model will be trained.
            // _experimentSettings.MaxExperimentTimeInSeconds is of type uint, it is
            // either 0 or >0.
            else
                _experimentTimerExpired = true;

            do
            {
                var iterationStopwatch = Stopwatch.StartNew();

                // get next pipeline
                var getPipelineStopwatch = Stopwatch.StartNew();

                // A new MLContext is needed per model run. When max experiment time is reached, each used
                // context is canceled to stop further model training. The cancellation of the main MLContext
                // a user has instantiated is not desirable, thus additional MLContexts are used.
                var activeMLContext = new MLContext(((ISeededEnvironment)_context.Model.GetEnvironment()).Seed);
                _activeMLContexts.Add(activeMLContext);
                var pipeline = PipelineSuggester.GetNextInferredPipeline(activeMLContext, _history, _datasetColumnInfo, _task,
                    _optimizingMetricInfo.IsMaximizing, _experimentSettings.CacheBeforeTrainer, _trainerAllowList);

                var pipelineInferenceTimeInSeconds = getPipelineStopwatch.Elapsed.TotalSeconds;

                // break if no candidates returned, means no valid pipeline available
                if (pipeline == null)
                {
                    break;
                }

                // evaluate pipeline
                _logger.Trace($"Evaluating pipeline {pipeline.ToString()}");
                (SuggestedPipelineRunDetail suggestedPipelineRunDetail, TRunDetail runDetail)
                    = _runner.Run(pipeline, _modelDirectory, _history.Count + 1);

                _history.Add(suggestedPipelineRunDetail);
                WriteIterationLog(pipeline, suggestedPipelineRunDetail, iterationStopwatch);

                runDetail.RuntimeInSeconds = iterationStopwatch.Elapsed.TotalSeconds;
                runDetail.PipelineInferenceTimeInSeconds = getPipelineStopwatch.Elapsed.TotalSeconds;

                ReportProgress(runDetail);
                iterationResults.Add(runDetail);

                // if model is perfect, break
                if (_metricsAgent.IsModelPerfect(suggestedPipelineRunDetail.Score))
                {
                    break;
                }

                // If after third run, all runs have failed so far, throw exception
                if (_history.Count() == 3 && _history.All(r => !r.RunSucceeded))
                {
                    throw new InvalidOperationException($"Training failed with the exception: {_history.Last().Exception}");
                }

            } while (_history.Count < _experimentSettings.MaxModels &&
                    !_experimentSettings.CancellationToken.IsCancellationRequested &&
                    !_experimentTimerExpired);
            return iterationResults;
        }

        private static DirectoryInfo GetModelDirectory(DirectoryInfo rootDir)
        {
            if (rootDir == null)
            {
                return null;
            }

            var experimentDirFullPath = Path.Combine(rootDir.FullName, $"experiment_{Path.GetRandomFileName()}");
            var experimentDirInfo = new DirectoryInfo(experimentDirFullPath);
            if (!experimentDirInfo.Exists)
            {
                experimentDirInfo.Create();
            }
            return experimentDirInfo;
        }

        private void ReportProgress(TRunDetail iterationResult)
        {
            try
            {
                _progressCallback?.Report(iterationResult);
            }
            catch (Exception ex)
            {
                _logger.Error($"Progress report callback reported exception {ex}");
            }
        }

        private void WriteIterationLog(SuggestedPipeline pipeline, SuggestedPipelineRunDetail runResult, Stopwatch stopwatch)
        {
            _logger.Trace($"{_history.Count}\t{runResult.Score}\t{stopwatch.Elapsed}\t{pipeline.ToString()}");
        }
    }
}
