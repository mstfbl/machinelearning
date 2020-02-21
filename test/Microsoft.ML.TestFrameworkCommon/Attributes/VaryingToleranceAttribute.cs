﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Reflection;
using Xunit.Sdk;
namespace Microsoft.ML.TestFrameworkCommon.Attributes
{
    /// <summary>
    /// A fact for tests with varying tolerance levels
    /// <code>
    /// [Theory, VaryingTolerance(50)]
    /// public void VaryingToleranceTest(double tolerance)
    /// {
    /// }
    /// </code>
    /// </summary>
    public sealed class VaryingToleranceAttribute : DataAttribute
    {
        public VaryingToleranceAttribute(int tolerance)
        {
            Tolerance = tolerance;
        }

        public int Tolerance { get; }
        public override IEnumerable<object[]> GetData(MethodInfo testMethod)
        {
            Console.WriteLine("This test utilizes varying tolerances.");
            yield return new object[] { Math.Pow(10, -1 * Tolerance) };
        }
    }
}