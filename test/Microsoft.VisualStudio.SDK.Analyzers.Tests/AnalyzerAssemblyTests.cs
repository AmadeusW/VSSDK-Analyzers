﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using Microsoft.VisualStudio.SDK.Analyzers;
using Xunit;

public class AnalyzerAssemblyTests
{
    private readonly ITestOutputHelper logger;

    public AnalyzerAssemblyTests(ITestOutputHelper logger)
    {
        this.logger = logger;
    }

    /// <summary>
    /// Verifies that although we compile against MPF for convenience in referencing its types at compile-time,
    /// we maintain that we never compile into an assembly that still references it, since it may not be around at runtime for the CLR to find.
    /// </summary>
    [Fact]
    public void AssemblyHasNoReferenceToMpf()
    {
        System.Reflection.AssemblyName[] refAssemblies = typeof(VSSDK001DeriveFromAsyncPackageAnalyzer).Assembly.GetReferencedAssemblies();
        this.logger.WriteLine("Referenced assemblies: {0}", string.Join(";", refAssemblies.Select(n => n.Name)));

        // Ban all Microsoft.VisualStudio.* assembly dependencies. But make a special allowance for when tests are running with
        // code coverage, in which case a ref assembly is injected.
        Assert.DoesNotContain(
            refAssemblies,
            a => a.Name.StartsWith("Microsoft.VisualStudio", StringComparison.OrdinalIgnoreCase) &&
                !a.Name.Equals("Microsoft.VisualStudio.CodeCoverage.Shim", StringComparison.OrdinalIgnoreCase));
    }
}
