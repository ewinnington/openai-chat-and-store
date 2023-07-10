using Azure.AI.OpenAI;

public interface IConversationStore
{
    ChatUser CreateOrAquireChatUser(string Name);
    Conversation CreateOrAquireConversation(int? id, ChatUser user);
    PromptResponse CreateRequest(Conversation conversation, string engine, ChatCompletionsOptions chatCompletionsOptions);
    void UpdateResponse(ChatUser user, Conversation conversation, PromptResponse promptResponse, ChatCompletions response);
    void UpdateResponse(ChatUser user, Conversation conversation, PromptResponse promptResponse, string response);
}

