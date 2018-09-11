using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using MathNet.Numerics;
using MathNet.Numerics.IntegralTransforms;

public class Player : AudioStreamPlayer
{
    private Vector2[] samplesFrames; //Contains ALL samples
    private Queue<Vector2> frameBuffer = new Queue<Vector2>();    //Contains only up to at most a certain number of samples
    public const int MaxFFTSize = 4096 * 2;
    
    private long framePointer;
    private int sampleRate;
    private double[] hann = Window.Hann(MaxFFTSize);
    private Stopwatch stopwatch = new Stopwatch();


    public override void _Ready()
    {
        AudioStreamSample stream = Stream as AudioStreamSample;
        byte[] samplesBytes = stream?.Data;
        sampleRate = stream.MixRate;
        
        //Convert bytes to signed 16-bit shorts
        var samplesShorts = BytesToShorts(samplesBytes);

        //Convert signed 16-bit shorts to floats
        var samplesFloats = ShortsToFloats(samplesShorts);

        //Convert floats to frames
        samplesFrames = FloatsToFrames(samplesFloats);
        
        Play(); //Don't autoplay - causes desync
        stopwatch.Start();
        _Process(0);
        
        //this works but it's SLOW
        //((GradientTexture) GetNode<TextureRect>("debugRect").Texture).Gradient = gradient;

    }

    public override void _Process(float delta)
    {
        long amountOfFramesPassed = (long)(stopwatch.Elapsed.TotalSeconds * sampleRate);
        stopwatch.Restart();
        
        //Delta time might not be entirely accurate. Using stopwatch instead.

        for (int i = 0; i < amountOfFramesPassed; i++)
        {
            frameBuffer.Enqueue(samplesFrames[framePointer]);
            framePointer = (framePointer + 1) % samplesFrames.LongLength;

            if (frameBuffer.Count > MaxFFTSize)
                frameBuffer.Dequeue();
        }

        if (frameBuffer.Count == 0)
            return;
        
        //Apply Hann window on the frames
        var frameBufferArray = frameBuffer.ToArray();
        for (int i = 0; i < frameBufferArray.Length; i++)
            frameBufferArray[i] *= (float)hann[i];
        
        //Convert frames to complex numbers 
        var framesComplex = FramesToComplex(frameBufferArray);
        Complex[] samplesComplexLeft = framesComplex.Item1; 
        Complex[] samplesComplexRight = framesComplex.Item2; 
        
        //Do FFT
        Fourier.Forward(samplesComplexLeft);
        Fourier.Forward(samplesComplexRight);
        
        //Convert to magnitudes
        float[] freqMagnitudesLeft  = Array.ConvertAll(samplesComplexLeft, x => (float) x.Magnitude);
        float[] freqMagnitudesRight = Array.ConvertAll(samplesComplexRight, x => (float) x.Magnitude);

        //Convert to cubes
        GetNode<Meshes>("Meshes").PlaceCubes(freqMagnitudesLeft, freqMagnitudesRight, sampleRate);
    }
    
    private Tuple<Complex[], Complex[]> FramesToComplex(Vector2[] frames)
    {
        Complex[] l = Array.ConvertAll(frames, v => new Complex(v.x, 0));
        Complex[] r = Array.ConvertAll(frames, v => new Complex(v.y, 0));
        
        return new Tuple<Complex[], Complex[]>(l, r);
    }

    private static float[] ShortsToFloats(short[] shorts)
    {
        float[] samplesFloats = Array.ConvertAll(shorts, x => (float) x / short.MaxValue);
        return samplesFloats;
    }

    private static Vector2[] FloatsToFrames(float[] floats)
    {
        Vector2[] samplesFrames = new Vector2[floats.Length / 2];
        for (int i = 0; i < floats.Length; i += 2)
            samplesFrames[i / 2] = new Vector2(floats[i], floats[i + 1]);
        return samplesFrames;
    }

    private static short[] BytesToShorts(byte[] bytes)
    {
        short[] samplesShorts = new short[bytes.Length / sizeof(short)];
        Buffer.BlockCopy(bytes, 0, samplesShorts, 0, bytes.Length);
        return samplesShorts;
    }
}
