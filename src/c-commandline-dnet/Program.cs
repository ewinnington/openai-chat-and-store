using Azure;
using Azure.AI.OpenAI;
using static System.Environment;
using l_dnet_embedlib;
using System.Diagnostics;
using System.Data.Common;
using Npgsql;
using Dapper;
using System.Text.Json;

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

        using (DbConnection connection = GetDbConnection())
        {
            connection.Open();
            //Console.WriteLine("Database Version: " + connection.ServerVersion);
            Console.WriteLine("Database State: " + connection.State);

            IConversationStore store = new DBPostgresConversationStore(connection);
            ChatUser user = store.CreateOrAquireChatUser("testuser");

            DirectChat(engine, client, store, user);

        }

        /*
        DirectChat(engine, client);
        Console.WriteLine("  -------  ");
        CheckEmbeds(embeddings, client);
        Console.WriteLine("  -------  ");
        await StreamingChat(engine, client);
        */
    }

    private static DbConnection GetDbConnection()
    {
        string dbSelected = (GetEnvironmentVariable("DATABASE_TYPE") ?? "postgres").ToUpperInvariant();
        DbConnection connection = null;
        switch (dbSelected)
        {
            case "POSTGRES":
                connection = new NpgsqlConnection(GetEnvironmentVariable("DATABASE_CONNECTION_STRING"));
                break;
            case "SQLITE":
                //connection = new SqliteConnection("Data Source=:memory:");
                //SqlMapper.AddTypeHandler(new FloatArrayTypeHandler());
                break;
            case "MSSQL":
                //connection = new SqlConnection("Data Source=your-server;Initial Catalog=your-database;User ID=your-username;Password=your-password;");
                break;
            default:
                throw new Exception("Database type not supported");
        }
        return connection;
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
        float precision = 0.000001f;
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

    private static void DirectChat(string engine, OpenAIClient client, IConversationStore conversationStore, ChatUser user, Conversation conversation = null)
    {
        var chatCompletionsOptions = getDemoChat();

        conversation = conversationStore.CreateOrAquireConversation(conversation == null ? null : conversation.Id, user);
        PromptResponse promptResponse = conversationStore.CreateRequest(conversation, engine, chatCompletionsOptions);

        Response<ChatCompletions> response = client.GetChatCompletions(
            deploymentOrModelName: engine,
            chatCompletionsOptions);

        Console.WriteLine(response.Value.Choices[0].Message.Content);
        conversationStore.UpdateResponse(user, conversation, promptResponse, response.Value);
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



public class SystemPrompt
{
    public int Id { get; set; }
    public string PromptName { get; set; }
    public string SystemPromptText { get; set; }
}

public class ChatUser
{
    public int Id { get; set; }
    public string Name { get; set; }
    public int DefaultPromptId { get; set; }
    public long InputTokensTotal { get; set; }
    public long OutputTokensTotal { get; set; }
}

public class Conversation
{
    public int Id { get; set; }
    public int ChatUserId { get; set; }
    public string Title { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastActiveAt { get; set; }
    public Dictionary<int, PromptResponse> PromptResponses { get; set; } = new Dictionary<int, PromptResponse>();
}

public class PromptResponse
{
    public int Id { get; set; }
    public int ConversationId { get; set; }
    public int OrderNum { get; set; }
    public string Prompt { get; set; }
    public string Response { get; set; }
}

public interface IConversationStore
{
    ChatUser CreateOrAquireChatUser(string Name);
    Conversation CreateOrAquireConversation(int? id, ChatUser user);
    PromptResponse CreateRequest(Conversation conversation, string engine, ChatCompletionsOptions chatCompletionsOptions);
    void UpdateResponse(ChatUser user, Conversation conversation, PromptResponse promptResponse, ChatCompletions response);
}

public class DBPostgresConversationStore : IConversationStore
{
    DbConnection connection;

    public DBPostgresConversationStore(DbConnection connection)
    {
        this.connection = connection;
    }

    public ChatUser CreateOrAquireChatUser(string Name)
    {
        ChatUser user = null;

        user = connection.QuerySingleOrDefault<ChatUser>("SELECT * FROM chat_user WHERE Name = @Name", new { Name });

        if (user == null)
            user = connection.QuerySingleOrDefault<ChatUser>("INSERT INTO chat_user (Name, input_tokens_total, output_tokens_total) VALUES (@Name, 0, 0) RETURNING *", new { Name });

        Debug.Assert(user != null);
        return user;
    }

    public Conversation CreateOrAquireConversation(int? id, ChatUser user)
    {
        Conversation conversation = null;

        if (id != null)
        {
            conversation = connection.QuerySingle<Conversation>("SELECT * FROM conversation WHERE Id = @Id", new { id });
            Debug.Assert(conversation != null && conversation.ChatUserId != user.Id, "Conversation does not belong to user");
            if (conversation != null)
            {
                //load prompt responses
                conversation.PromptResponses = connection.Query<PromptResponse>("SELECT * FROM PromptResponse WHERE ConversationId = @Id ORDER BY order_num", new { conversation.Id }).ToDictionary(x => x.Id);
            }
        }

        if (conversation == null)
            conversation = connection.QuerySingle<Conversation>("INSERT INTO conversation (chatuser_id, title, created_at, last_active_at) VALUES (@ChatUserId, @Title, @CreatedAt, @LastActiveAt) RETURNING *", new { ChatUserId = user.Id, Title = "New Conversation", CreatedAt = DateTime.UtcNow, LastActiveAt = DateTime.UtcNow });

        Debug.Assert(conversation != null);
        return conversation;
    }

    public PromptResponse CreateRequest(Conversation conversation, string engine, ChatCompletionsOptions chatCompletionsOptions)
    {
        PromptResponse promptResponse = new PromptResponse()
        {
            ConversationId = conversation.Id,
            OrderNum = conversation.PromptResponses?.Count ?? 0,
            Prompt = JsonSerializer.Serialize(chatCompletionsOptions)
        };

        promptResponse = connection.QuerySingle<PromptResponse>("INSERT INTO prompt_response (conversation_id, order_num, Prompt) VALUES (@ConversationId, @OrderNum, @Prompt::jsonb) RETURNING *", promptResponse);
        conversation.PromptResponses.Add(promptResponse.Id, promptResponse);
        return promptResponse;
    }

    public void UpdateResponse(ChatUser user, Conversation conversation, PromptResponse promptResponse, ChatCompletions response)
    {
        promptResponse.Response = JsonSerializer.Serialize(response);
        connection.Execute("UPDATE prompt_response SET response = @Response::jsonb WHERE Id = @Id", promptResponse);

        //update user stats from the response json "Usage": {"TotalTokens": 160, "PromptTokens": 59, "CompletionTokens": 101}
        var usage = response.Usage;
        user.InputTokensTotal += usage.TotalTokens;
        user.OutputTokensTotal += usage.TotalTokens;
        connection.Execute("UPDATE chat_user SET input_tokens_total = @InputTokensTotal, output_tokens_total = @OutputTokensTotal WHERE Id = @Id", user);

        //update conversation last active
        conversation.LastActiveAt = DateTime.UtcNow; 
        connection.Execute("UPDATE conversation SET last_active_at = @LastActiveAt WHERE Id = @Id", new { LastActiveAt = conversation.LastActiveAt, conversation.Id });
    }
}

