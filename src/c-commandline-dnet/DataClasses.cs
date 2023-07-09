using Azure.AI.OpenAI;

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

