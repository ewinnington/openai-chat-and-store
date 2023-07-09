using Azure.AI.OpenAI;
using System.Diagnostics;
using System.Data.Common;
using Dapper;
using System.Text.Json;


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
            conversation = connection.QuerySingle<Conversation>("SELECT * FROM conversation WHERE Id = @Id", new { Id = id.Value });
            Debug.Assert(conversation != null && conversation.ChatUserId != user.Id, "Conversation does not belong to user");
            if (conversation != null)
            {
                //load prompt responses
                var prompt_list = connection.Query<PromptResponse>("SELECT * FROM prompt_response WHERE conversation_id = @Id ORDER BY order_num", new { conversation.Id }).ToList();
                foreach (var p in prompt_list)
                    conversation.PromptResponses.Add(p.Id, p);
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
        connection.Execute("UPDATE conversation SET last_active_at = @LastActiveAt WHERE Id = @Id", new { LastActiveAt = conversation.LastActiveAt, Id = conversation.Id });
    }
}

