-- DROP TABLE IF EXISTS "subscriptions";
-- DROP TYPE IF EXISTS "subscription_status";

CREATE TYPE "subscription_status" AS ENUM (
    'pending', 
    'active', 
    'deleted'
);

CREATE TABLE IF NOT EXISTS "subscriptions" (
    "id" bigserial NOT NULL,
    "identifier" uuid NOT NULL UNIQUE,
    "topic" varchar(255) NOT NULL,
    "callback" varchar(255) NOT NULL,
    "requested_lease_seconds" bigint NULL,
    "granted_lease_seconds" bigint NOT NULL,
    "secret" bytea NULL,
    "status" subscription_status NOT NULL,
    "created_at" timestamp with time zone NOT NULL,
    "expires_at" timestamp with time zone NOT NULL,
    PRIMARY KEY ("id")
);
