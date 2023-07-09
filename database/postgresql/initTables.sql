CREATE SCHEMA IF NOT EXISTS chatstore AUTHORIZATION CURRENT_USER;

CREATE TABLE system_prompt (id INTEGER PRIMARY KEY GENERATED ALWAYS AS IDENTITY, prompt_name TEXT NOT NULL, system_prompt_text TEXT NOT NULL);
INSERT INTO  system_prompt (prompt_name, system_prompt_text) VALUES ('default', 'Hello, I am a chatbot. I am here to help you with your questions. What would you like to know?');
CREATE TABLE chat_user (id INTEGER PRIMARY KEY GENERATED ALWAYS AS IDENTITY, name TEXT NOT NULL, default_prompt_id INTEGER REFERENCES system_prompt(id) DEFAULT 1, input_tokens_total BIGINT NOT NULL, output_tokens_total BIGINT NOT NULL);
CREATE TABLE conversation (id INTEGER PRIMARY KEY GENERATED ALWAYS AS IDENTITY, chatuser_id INTEGER REFERENCES chat_user(id), title TEXT NOT NULL, created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,  last_active_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP );
CREATE TABLE prompt_response (id INTEGER PRIMARY KEY GENERATED ALWAYS AS IDENTITY, conversation_id INTEGER REFERENCES conversation(id), order_num INTEGER NOT NULL,  prompt JSONB NOT NULL,  response JSONB);