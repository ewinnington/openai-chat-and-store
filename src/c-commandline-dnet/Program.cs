using Azure;
using Azure.AI.OpenAI;
using static System.Environment;
using l_dnet_embedlib;
using System.Diagnostics;

class Program
{
    static async Task Main(string[] args)
    {
        string selector = GetEnvironmentVariable("OPENAI_OR_AZURE") ?? "OPENAI";
        string endpoint = GetEnvironmentVariable("AZURE_ENDPOINT") ?? "";
        string key = GetEnvironmentVariable("OPENAI_API_KEY") ?? "";
        string engine = GetEnvironmentVariable("OPENAI_MODEL") ?? "gpt-3.5-turbo";
        string embeddings = GetEnvironmentVariable("OPENAI_MODEL_EMBEDS") ?? "text-embedding-ada-002";

        bool useAzureOpenAI = selector == "AZURE";

        OpenAIClient client = useAzureOpenAI
            ? new OpenAIClient(
                new Uri(endpoint),
                new AzureKeyCredential(key))
            : new OpenAIClient(key);


        DirectChat(engine, client);
        Console.WriteLine("  -------  ");
        CheckEmbeds(embeddings, client);
        Console.WriteLine("  -------  ");
        await StreamingChat(engine, client);
    }

    private static void CheckEmbeds(string embeddings, OpenAIClient client)
    {
        var embeddingOptions1 = new EmbeddingsOptions("dog");
        var embeddingOptions2 = new EmbeddingsOptions("astronaut");

        var result1 = client.GetEmbeddings(embeddings, embeddingOptions1);
        var result2 = client.GetEmbeddings(embeddings, embeddingOptions2);

        float Similarity = EmbeddingCalculator.Similarity(
            result1.Value.Data[0].Embedding.ToArray(),
            result2.Value.Data[0].Embedding.ToArray());

        float Target_Value_From_Python_check_embeds_py = 0.80373316598824f;
        float precision                                = 0.000001f;
        bool isWithinTolerance = Math.Abs(Similarity - Target_Value_From_Python_check_embeds_py) < precision;

        Console.WriteLine("Similarity :" + Similarity + " absolute diff " + Math.Abs(Similarity - Target_Value_From_Python_check_embeds_py) + " is Within Tolerance " + isWithinTolerance);
        Debug.Assert(isWithinTolerance);
    }

    public static ChatCompletionsOptions getDemoChat()
    {
        return new ChatCompletionsOptions()
        {
            Messages =
            {
                new ChatMessage(ChatRole.System, "You are a helpful assistant."),
                new ChatMessage(ChatRole.User, "Please define energy trading?"),
                new ChatMessage(ChatRole.Assistant, "Energy trading is the practice of buying power from or selling power to a counter-party or a market"),
                new ChatMessage(ChatRole.User, "Please define what a base power future option is?"),
            },
            MaxTokens = 500
        };
    }

    private static void DirectChat(string engine, OpenAIClient client)
    {
        var chatCompletionsOptions = getDemoChat();

        Response<ChatCompletions> response = client.GetChatCompletions(
            deploymentOrModelName: engine,
            chatCompletionsOptions);

        Console.WriteLine(response.Value.Choices[0].Message.Content);
    }

    private static async Task StreamingChat(string engine, OpenAIClient client)
    {
        var chatCompletionsOptions = getDemoChat();

        Response<StreamingChatCompletions> response = await client.GetChatCompletionsStreamingAsync(
            deploymentOrModelName: "gpt-3.5-turbo",
            chatCompletionsOptions);
        using StreamingChatCompletions streamingChatCompletions = response.Value;

        await foreach (StreamingChatChoice choice in streamingChatCompletions.GetChoicesStreaming())
        {
            Console.WriteLine("Choice index: " + choice.Index + " -- Finish Reason: " + choice.FinishReason);
            await foreach (ChatMessage message in choice.GetMessageStreaming())
            {
                Console.WriteLine("\t\t" + message.Content);
            }
            Console.WriteLine();
        }
    }
}





