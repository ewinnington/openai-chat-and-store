using Azure.AI.OpenAI;
using System.Diagnostics;
using System.Data.Common;
using Dapper;
using System.Text.Json;
using Microsoft.Data.Sqlite;

public class DBSQLiteConversationStore : IConversationStore
{
    DbConnection connection;

    public DBSQLiteConversationStore(DbConnection connection)
    {
        this.connection = connection;

        ensure_tables(); 
    }

    private void ensure_tables()
    {
        string sqldatamodel = @"
CREATE TABLE IF NOT EXISTS system_prompt (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    prompt_name TEXT NOT NULL,
    system_prompt_text TEXT NOT NULL
);
INSERT INTO system_prompt (prompt_name, system_prompt_text) VALUES ('default', 'Hello, I am a chatbot. I am here to help you with your questions. What would you like to know?');

CREATE TABLE IF NOT EXISTS chat_user (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT NOT NULL,
    default_prompt_id INTEGER,
    input_tokens_total INTEGER NOT NULL,
    output_tokens_total INTEGER NOT NULL,
    FOREIGN KEY (default_prompt_id) REFERENCES system_prompt(id)
);

CREATE TABLE IF NOT EXISTS conversation (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    chatuser_id INTEGER,
    title TEXT NOT NULL,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    last_active_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (chatuser_id) REFERENCES chat_user(id)
);

CREATE TABLE IF NOT EXISTS prompt_response (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    conversation_id INTEGER,
    order_num INTEGER NOT NULL,
    prompt TEXT NOT NULL,
    response TEXT,
    FOREIGN KEY (conversation_id) REFERENCES conversation(id)
);";

        //apply this datamodel to the database
        connection.Execute(sqldatamodel);
    }

    public ChatUser CreateOrAquireChatUser(string Name)
    {
        ChatUser user = null;

        user = connection.QuerySingleOrDefault<ChatUser>("SELECT * FROM chat_user WHERE Name = @Name", new { Name });

        if (user == null)
        {
            int id = connection.QuerySingle<int>("INSERT INTO chat_user (Name, input_tokens_total, output_tokens_total) VALUES (@Name, 0, 0); SELECT last_insert_rowid()", new { Name });
            user = connection.QuerySingleOrDefault<ChatUser>("SELECT * FROM chat_user WHERE Id = @Id", new { Id = id });
        }

        Debug.Assert(user != null);
        user.Id = (int)(long)user.Id;
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
        {
            id = connection.QuerySingle<int>("INSERT INTO conversation (chatuser_id, title, created_at, last_active_at) VALUES (@ChatUserId, @Title, @CreatedAt, @LastActiveAt); SELECT last_insert_rowid()", new { ChatUserId = user.Id, Title = "New Conversation", CreatedAt = DateTime.UtcNow, LastActiveAt = DateTime.UtcNow });
            conversation = connection.QuerySingleOrDefault<Conversation>("SELECT * FROM conversation WHERE Id = @Id", new { Id = id });
        }
        Debug.Assert(conversation != null);
        conversation.Id = (int)(long)conversation.Id;
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


        int id = connection.QuerySingle<int>("INSERT INTO prompt_response (conversation_id, order_num, prompt, response) VALUES (@ConversationId, @OrderNum, @Prompt, @Response); SELECT last_insert_rowid()", promptResponse);
        promptResponse = connection.QuerySingleOrDefault<PromptResponse>("SELECT * FROM prompt_response WHERE Id = @Id", new { Id = id });
        
        conversation.PromptResponses.Add(promptResponse.Id, promptResponse);
        return promptResponse;
    }

    public void UpdateResponse(ChatUser user, Conversation conversation, PromptResponse promptResponse, ChatCompletions response)
    {
        promptResponse.Response = JsonSerializer.Serialize(response);
        connection.Execute("UPDATE prompt_response SET response = @Response WHERE Id = @Id", promptResponse);

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