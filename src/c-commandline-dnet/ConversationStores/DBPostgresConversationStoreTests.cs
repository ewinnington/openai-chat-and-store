using System;
using System.IO;
using Azure.AI.OpenAI;
using Xunit;
using Npgsql;
using static System.Environment;

namespace c_commandline_dnet.Tests
{
    public class DBPostgresConversationStoreTests : IDisposable
    {
        private NpgsqlConnection _connection;

        public DBPostgresConversationStoreTests()
        {
            _connection = new NpgsqlConnection(GetEnvironmentVariable("DATABASE_CONNECTION_STRING"));
            _connection.Open();
        }

        public void Dispose()
        {
            // Clean up the test database
            _connection.Close();
        }

        [Fact]
        public void FullTest()
        {
            // Arrange
            var store = new DBPostgresConversationStore(_connection);

            //a GUID User name should be unique enough to test
            string usr = Guid.NewGuid().ToString(); 
            var user1 = store.CreateOrAquireChatUser(usr);
            var user2 = store.CreateOrAquireChatUser(usr);
            Assert.Equal(user1.Id, user2.Id);

            // Insert and retrieve same conversation
            var conversation1 = store.CreateOrAquireConversation(null, user1);
            
            //On the Postgresql Can't assume that I am the first conversation id
            //Assert.Equal(1, conversation1.Id);
            
            var conversation2 = store.CreateOrAquireConversation(conversation1.Id, user1);
            Assert.Equal(conversation1.Id, conversation2.Id);

            //add suprious conversations
            store.CreateOrAquireConversation(null, user1);
            store.CreateOrAquireConversation(null, user1);
            store.CreateOrAquireConversation(null, user1);

            //does it still get the first converation?
            var conversation3 = store.CreateOrAquireConversation(null, user1);
            conversation2 = store.CreateOrAquireConversation(conversation1.Id, user1);
            Assert.Equal(conversation1.Id, conversation2.Id);

            //Create request
            var promptResponse = store.CreateRequest(conversation1, "model", new ChatCompletionsOptions() 
            {  Messages =
            {
                new ChatMessage(ChatRole.System, "You are a helpful assistant."),
                new ChatMessage(ChatRole.User, "Please define energy trading?")
            }, MaxTokens = 5 
            });

            //is the request in the database?
            var conversation_from_db = store.CreateOrAquireConversation(conversation1.Id, user1);
            //the in-memory conversation and the conversation from DB should have same ID in same slot
            Assert.Equal(conversation1.PromptResponses[promptResponse.Id].Id, conversation_from_db.PromptResponses[promptResponse.Id].Id);

            //Update response
            /*Cannot test the update responses because I can't create a response object
            
            store.UpdateResponse(user1, conversation1, promptResponse, new ChatCompletions() { Usage = new CompletionsUsage() {completionTokens = 20, promptTokens = 50, totalTokens = 70 },   });
            //check memory
            Assert.Equal(20, user1.OutputTokensTotal);
            Assert.Equal(50, user1.OutputTokensTotal);

            //check db
            var user1 = store.CreateOrAquireChatUser("testcase");
            Assert.Equal(20, user1.OutputTokensTotal);
            Assert.Equal(50, user1.OutputTokensTotal);
            */
        }
    }
}