using Azure.AI.OpenAI;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

public class FileBasedConversationStore : IConversationStore
{
    private readonly string _basePath;
    private Random random = new Random();

    public FileBasedConversationStore(string basePath)
    {
        _basePath = basePath;
    }

    public ChatUser CreateOrAquireChatUser(string name)
    {
        ChatUser user = null; 
        var path = Path.Combine(_basePath, "chat_user", $"{name}.json");
        if (File.Exists(path))
        {
            return JsonSerializer.Deserialize<ChatUser>(File.ReadAllText(path));
        }
        else
        {
            user = new ChatUser { Name = name, InputTokensTotal = 0, OutputTokensTotal = 0, Id = random.Next(100000, 999999) };
        };
        var json = JsonSerializer.Serialize(user);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, json);
        return user;
    }

    public Conversation CreateOrAquireConversation(int? id, ChatUser user)
    {
        if (id == null)
        {
            id = random.Next(100000, 999999);
        }
        var path = Path.Combine(_basePath, "conversation", $"{id}.json");
        if (File.Exists(path))
        {
            var json = File.ReadAllText(path);
            var conversation = JsonSerializer.Deserialize<Conversation>(json);
            Debug.Assert(conversation != null && conversation.ChatUserId == user.Id, "Conversation does not belong to user");
            if (conversation != null)
            {
                //load prompt responses
                conversation.PromptResponses = new Dictionary<int, PromptResponse>();
                var promptResponsePath = Path.Combine(_basePath, "prompt_response", $"{conversation.Id}");
                if (Directory.Exists(promptResponsePath))
                {
                    foreach (var promptResponseFile in Directory.GetFiles(promptResponsePath, "*.json"))
                    {
                        var promptResponseJson = File.ReadAllText(promptResponseFile);
                        var promptResponse = JsonSerializer.Deserialize<PromptResponse>(promptResponseJson);
                        conversation.PromptResponses.Add(promptResponse.Id, promptResponse);
                    }
                }
            }
            return conversation;
        }
        else
        {
            var conversation = new Conversation
            {
                Id = id.Value,
                ChatUserId = user.Id,
                Title = "New Conversation",
                CreatedAt = DateTime.UtcNow,
                LastActiveAt = DateTime.UtcNow,
                PromptResponses = new Dictionary<int, PromptResponse>()
            };
            var json = JsonSerializer.Serialize(conversation);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, json);
            return conversation;
        }
    }

    public PromptResponse CreateRequest(Conversation conversation, string engine, ChatCompletionsOptions chatCompletionsOptions)
    {
        var promptResponse = new PromptResponse()
        {
            Id =  DateTime.Now.Month           * 1_000_000
                    + DateTime.Now.Day            * 10_000
                    + DateTime.Now.Hour              * 100
                    + DateTime.Now.Minute,  
            ConversationId = conversation.Id,
            OrderNum = conversation.PromptResponses?.Count ?? 0,
            Prompt = JsonSerializer.Serialize(chatCompletionsOptions)
        };

        var path = Path.Combine(_basePath, "prompt_response", $"{conversation.Id}", $"{promptResponse.OrderNum}-{promptResponse.Id}.json");
        var json = JsonSerializer.Serialize(promptResponse);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, json);
        conversation.PromptResponses.Add(promptResponse.Id, promptResponse);
        return promptResponse;
    }

    public void UpdateResponse(ChatUser user, Conversation conversation, PromptResponse promptResponse, ChatCompletions response)
    {
        //Warning - needs a lock because doesn't re-read from disk the previous state and might be invalid
        promptResponse.Response = JsonSerializer.Serialize(response);

        var path = Path.Combine(_basePath, "prompt_response", $"{conversation.Id}", $"{promptResponse.Id}.json");
        var json = JsonSerializer.Serialize(promptResponse);
        File.WriteAllText(path, json);

        //update user stats from the response json "Usage": {"TotalTokens": 160, "PromptTokens": 59, "CompletionTokens": 101}
        var usage = response.Usage;
        user.InputTokensTotal += usage.PromptTokens;
        user.OutputTokensTotal += usage.CompletionTokens;
        var userPath = Path.Combine(_basePath, "chat_user", $"{user.Name}.json");
        json = JsonSerializer.Serialize(user);
        File.WriteAllText(userPath, json);

        //update conversation last active
        conversation.LastActiveAt = DateTime.UtcNow;
        path = Path.Combine(_basePath, "conversation", $"{conversation.Id}.json");
        json = JsonSerializer.Serialize(conversation);
        File.WriteAllText(path, json);
    }
}
