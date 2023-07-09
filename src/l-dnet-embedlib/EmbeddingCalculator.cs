namespace l_dnet_embedlib;

public class EmbeddingCalculator
{
    public static float Similarity(float[] v1, float[] v2)
    {
        //Note OpenAI Magnitude is already normalized to 1
        float dotProduct = EmbeddingCalculator.DotProduct(v1, v2);
        float magV1 = EmbeddingCalculator.Magnitude(v1);
        float magV2 = EmbeddingCalculator.Magnitude(v2);
        return dotProduct / (magV1 * magV2);
    }
    public static float DotProduct(float[] v1, float[] v2)
    {
        float val = 0;
        for (Int32 i = 0; i <= v1.Length - 1; i++)
            val += v1[i] * v2[i];
        return val;
    }
    public static float Magnitude(float[] v)
    {
        return (float)Math.Sqrt(EmbeddingCalculator.DotProduct(v, v));
    }
}