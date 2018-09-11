using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public class Meshes : Spatial
{
    
    private Gradient gradient = new Gradient();

    
    public void PlaceCubes(float[] freqMagnitudesLeft, float[] freqMagnitudesRight, double sampleRate)
    {
        double halfSampleRate = sampleRate / 2.0;

        var halfLen = freqMagnitudesLeft.Length / 2;

        for (int channel = 0; channel < 2; channel++)
        {
            var cubeCount = 50;
            var cubeTotalWidth = 20;
            var cubeTotalHeight = 2;
            
            var L = channel == 0;
            var pointBlocks = new Dictionary<int, List<Vector2>>(halfLen);
            
            for (int block = 0; block < cubeCount; block++)
                pointBlocks[block] = new List<Vector2>();

            var meshInstance = GetNode<MultiMeshInstance>($"Mesh{(L ? "L" : "R")}");
            
            gradient.SetColors(new Color[0]);
            gradient.SetOffsets(new float[0]);
            
            //Determine the FFT points in log space
            for (int i = 0; i < halfLen; i++) 
            {
                float progress = i / (float) halfLen;
                float x = (float) Math.Log10(progress * halfSampleRate) / (float)Math.Log10(halfSampleRate);
                if (i == 0) x = 0;
                
                float v = (L ? freqMagnitudesLeft : freqMagnitudesRight)[i];
                
                float y = (float) (Math.Log10(10*v+1)/Math.Log10(10));

                int block = (int) (x * cubeCount);
                pointBlocks[block].Add(new Vector2(x, y));
                gradient.AddPoint(x, new Color(y, 0, 0, 1)); 
            }
            
   
            //Fill the blocks by taking maxima over the point buckets
            var blockVectors = new Vector3[cubeCount];
            for (int block = 0; block < cubeCount; block++)
            {
                var points = pointBlocks[block];

                var amplitude = 0f;
                if (points.Count > 0)
                    amplitude = points.Max(v => v.y);
                else //If no FFT entries landed in this bucket, interpolate from neighbors
                {
                    float fraction = (block + 0.5f) / cubeCount; //Sample at halfway through the cube
                    amplitude = gradient.Interpolate(fraction).r;
                }
                
                blockVectors[block] = new Vector3((block - cubeCount / 2f) / cubeCount * cubeTotalWidth, amplitude, 0);
            }

            meshInstance.Multimesh.InstanceCount = cubeCount;

            //The Transform is spread over 4 entries: basis.x, basis.y, basis.z, origin
            //See https://github.com/godotengine/godot/blob/53070437514e448c87f6cb31cf5b27a3839dbfa1/scene/resources/multimesh.cpp#L34
            List<Vector3> ManifyVectors(Vector3 v)
            {
                return new List<Vector3> //basis.x, basis.y, basis.z, origin
                {
                    new Vector3(1,0,0) / cubeCount / 2 * cubeTotalWidth * 0.75f, //width
                    new Vector3(0,1,0) * v.y * cubeTotalHeight / 2.0f, //height
                    new Vector3(0,0,1) * 0.1f, //thickness of the block
                    v * Vector3.Right
                };
            }
            meshInstance.Multimesh.TransformArray = blockVectors.SelectMany(ManifyVectors).ToArray();

            //Color the cubes along a rainbow, with its value corresponding to its intensity
            meshInstance.Multimesh.ColorArray = Enumerable.Range(0, cubeCount)
                .Select(v => Color.FromHsv(v / (float) cubeCount / 2, 0.8f, blockVectors[v].y, 1f)).ToArray();

        }
        
    }
}
