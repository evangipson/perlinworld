using System.Linq;
using UnityEngine;

public class GeneratePerlinTerrainScript : MonoBehaviour {

    // you can modify these variables via the inspector in unity
    public float amplitude = 60.0f;
    public float frequency = 2.0f;
    public int width = 200;
    public int height = 200;
    public float textureScale = 1.0f;
    // private variables used by this file not referenced or open in unity
    private float[,] heights;
    // Splatmap data is stored internally as a 3d array of floats, so declare a new empty array ready for your custom splatmap data:
    private float[,,] splatmapData;
    private Terrain terrain;
    private System.Random random = new System.Random();
    private Renderer rend;
    // to keep track if we have changes
    private float _oldAmplitude;
    private float _oldWidth;
    private float _oldHeight;
    private float _oldFrequency;
    private float _oldTextureScale;

    void Start ()
    {
        terrain = GetComponent<Terrain>();
        // terraindata is a collection of floats that determine height
        terrain.terrainData = GeneratePerlinTerrain(terrain.terrainData);
        RecolorMap();
        CaptureUnityFacingVariables();
    }
    void FixedUpdate()
    {
        if (UnityVariableChanged())
        {
            CaptureUnityFacingVariables();
            terrain.terrainData = GeneratePerlinTerrain(terrain.terrainData);
            RecolorMap();
        }
    }
    void CaptureUnityFacingVariables()
    {
        _oldAmplitude = amplitude;
        _oldWidth = width;
        _oldHeight = height;
        _oldFrequency = frequency;
        _oldTextureScale = textureScale;
    }

    bool UnityVariableChanged()
    {
        return _oldAmplitude != amplitude ||
             _oldWidth != width ||
             _oldHeight != height ||
             _oldAmplitude != frequency ||
             _oldTextureScale != textureScale;
    }

    TerrainData GeneratePerlinTerrain(TerrainData terrainData)
    {
        terrainData.size = new Vector3(width, amplitude, height);
        terrainData.SetHeights(0, 0, GenerateHeights());
        return terrainData;
    }

    float[,] GenerateHeights()
    {
        // a grid of floats, so each point on our map has a float associated to it.
        heights = new float[terrain.terrainData.heightmapWidth, terrain.terrainData.heightmapHeight];
        // how many layers of "detail" will we have in the world
        int octaves = random.Next(2, 5);
        for (int x = 0; x < terrain.terrainData.heightmapWidth; x++)
        {
            for (int y = 0; y < terrain.terrainData.heightmapHeight; y++)
            {
                heights[x, y] = CalculateHeight(x, y, octaves);
            }
        }
        return heights;
    }
	
    void RecolorMap()
    {
        splatmapData = new float[terrain.terrainData.alphamapWidth, terrain.terrainData.alphamapHeight, terrain.terrainData.alphamapLayers];
        // now handle assigning textures based on height
        for (int y = 0; y < terrain.terrainData.alphamapHeight; y++)
        {
            for (int x = 0; x < terrain.terrainData.alphamapWidth; x++)
            {
                // Normalise x/y coordinates to range 0-1 
                float y_01 = y / (float)terrain.terrainData.alphamapHeight;
                float x_01 = x / (float)terrain.terrainData.alphamapWidth;

                // Sample the height at this location (note GetHeight expects int coordinates corresponding to locations in the heightmap array)
                float fHeight = terrain.terrainData.GetHeight(Mathf.RoundToInt(y_01 * terrain.terrainData.heightmapHeight), Mathf.RoundToInt(x_01 * terrain.terrainData.heightmapWidth));

                // Calculate the normal of the terrain (note this is in normalised coordinates relative to the overall terrain dimensions)
                Vector3 normal = terrain.terrainData.GetInterpolatedNormal(y_01, x_01);

                // Calculate the steepness of the terrain
                float steepness = terrain.terrainData.GetSteepness(y_01, x_01);

                // Setup an array to record the mix of texture weights at this point
                float[] splatWeights = new float[terrain.terrainData.alphamapLayers];

                // CHANGE THE RULES BELOW TO SET THE WEIGHTS OF EACH TEXTURE ON WHATEVER RULES YOU WANT

                // reset our texture weights
                splatWeights[0] = 0;
                splatWeights[1] = 0;
                // water has constant influence
                splatWeights[2] = 0.5f;

                fHeight *= textureScale;

                // if we're above water, start drawing grass
                if (fHeight > (amplitude * 0.15))
                {
                    splatWeights[2] = 0.25f;
                    splatWeights[0] = 0.25f;
                }
                // now we're fully above water
                if (fHeight > (amplitude * 0.25))
                {
                    splatWeights[2] = 0;
                    splatWeights[0] = 0.5f;
                }
                // now we're getting to the mountains
                if (fHeight > (amplitude * 0.6))
                {
                    // no water influence
                    splatWeights[2] = 0;
                    // just a little grass influence
                    splatWeights[0] = 0.25f;
                    // mostly mountain influence
                    splatWeights[1] = 0.5f;
                }
                // now we're in the mountains
                if (fHeight > (amplitude * 0.7))
                {
                    // no water influence
                    splatWeights[2] = 0;
                    // grass has no influence up here
                    splatWeights[0] = 0;
                    // now we're all mountain influence
                    splatWeights[1] = 0.5f;
                }

                // Sum of all textures weights must add to 1, so calculate normalization factor from sum of weights
                float z = splatWeights.Sum();

                // Loop through each terrain texture
                for (int i = 0; i < terrain.terrainData.alphamapLayers; i++)
                {
                    // Normalize so that sum of all texture weights = 1
                    splatWeights[i] /= z;

                    // Assign this point to the splatmap array
                    splatmapData[x, y, i] = splatWeights[i];
                }
            }
        }
        // Finally assign the new splatmap to the terrainData:
        terrain.terrainData.SetAlphamaps(0, 0, splatmapData);
    }

    float CalculateHeight(int x, int y, int octaves)
    {
        float persistence = (float)(random.Next(25, 75) * .01); // pick a random persistence, 0-100%
        float noise = 0f;
        float maxValue = 0; // used for clamping final resultz
        float localFrequency = frequency;
        float localAmplitude = amplitude;
        float xCoord;
        float yCoord;
        for (int i = 0; i < octaves; i++)
        {
            xCoord = (float)x / terrain.terrainData.heightmapWidth * localFrequency;
            yCoord = (float)y / terrain.terrainData.heightmapHeight * localFrequency;
            noise += Mathf.PerlinNoise(xCoord, yCoord) * localAmplitude;
            // now change the frequency and amplitude for the next octave
            maxValue += localAmplitude;
            localAmplitude *= persistence;
            localFrequency *= 2;
        }
        return noise / maxValue;
    }
}
