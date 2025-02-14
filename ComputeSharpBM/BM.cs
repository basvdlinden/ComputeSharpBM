﻿using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using CommunityToolkit.HighPerformance;
using CommunityToolkit.HighPerformance.Helpers;
using ComputeSharp;
using System.Numerics;

[MemoryDiagnoser]
[HideColumns("Error", "StdDev", "RatioSD", "Alloc Ratio")]
public class BM : IDisposable {

    public float[] MyArray = Array.Empty<float>();
    private ReadWriteBuffer<float> _gpuBuffer = null!;

    [Params(1920 * 2)]
    public int Width;

    [Params(1080 * 2)]
    public int Height;

    [Params(10)]
    public int Iterations;

    [GlobalSetup]
    public void Setup() {
        MyArray = Enumerable.Range(1, Width * Height).Select(static i => (float)i / 5).ToArray();

        // Create the graphics buffer
        _gpuBuffer = GraphicsDevice.GetDefault().AllocateReadWriteBuffer<float>(Width * Height);
    }

    [Benchmark(Baseline = true)]
    public void ArrayBM() {
        var array = MyArray; // avoid bounds check

        for (int i = 0; i < Iterations; i++) {
            for (int index = 0; index < array.Length; index++) {
                array[index] *= 2.0f;
            }
        }
    }

    [Benchmark]
    public void ComputeSharpBM() {
        // Write the data in
        _gpuBuffer.CopyFrom(MyArray);

        // Run the shader
        var shader = new MultiplyByTwo(_gpuBuffer, this.Width);
        for (int i = 0; i < Iterations; i++) {
            GraphicsDevice.GetDefault().For(Width, Height, shader);
        }

        // Get the data back
        _gpuBuffer.CopyTo(MyArray);
    }

    [Benchmark]
    public void ParallelBM() {
        for (int i = 0; i < Iterations; i++) {
            Parallel.For(0, MyArray.Length, i => MyArray[i] *= 2.0f);
        }
    }

    [Benchmark]
    public void ParallelHelperBM() {
        // NuGet\Install-Package CommunityToolkit.HighPerformance
        for (int i = 0; i < Iterations; i++) {
            ParallelHelper.ForEach<float, MultiplyByTwoRefAction>(MyArray);
        }
    }

    [Benchmark]
    public void VectorBM() {
        var vector = MyArray.AsSpan().AsVector();

        for (int i = 0; i < Iterations; i++) {
            for (int vectorIndex = 0; vectorIndex < vector.Length; vectorIndex++) {
                vector[vectorIndex] *= 2.0f;
            }
        }
    }

    [Benchmark]
    public void ParallelHelperVectorBM() {
        // NuGet\Install-Package CommunityToolkit.HighPerformance
        var vector = MyArray.AsMemory().Cast<float, Vector<float>>();
        for (int i = 0; i < Iterations; i++) {
            ParallelHelper.ForEach<Vector<float>, MultiplyVectorByTwoRefAction>(vector);
        }
    }

    [GlobalCleanup]
    public void Dispose() {
        _gpuBuffer.Dispose();
    }
}
