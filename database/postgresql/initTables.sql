CREATE SCHEMA IF NOT EXISTS chatstore AUTHORIZATION CURRENT_USER;

CREATE TABLE IF NOT EXISTS myevents ( "id" serial primary key not null, "uuid" uuid NOT NULL, "type" text NOT NULL, "body" jsonb NOT NULL, "inserted_at" timestamp(6) NOT NULL DEFAULT statement_timestamp());
CREATE TABLE IF NOT EXISTS mytable (  id SERIAL PRIMARY KEY,  name VARCHAR(255) NOT NULL);