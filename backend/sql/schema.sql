-- Users table
CREATE TABLE users (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    email TEXT UNIQUE NOT NULL,
    full_name TEXT,
    created_at TIMESTAMP DEFAULT now()
);

-- Terms of Service table
CREATE TABLE tos_versions (
    id SERIAL PRIMARY KEY,
    version TEXT NOT NULL,
    content TEXT NOT NULL,
    published_at TIMESTAMP NOT NULL
);

-- ToS acceptance log
CREATE TABLE tos_acceptance (
    id SERIAL PRIMARY KEY,
    user_id UUID REFERENCES users(id) ON DELETE CASCADE,
    tos_id INTEGER REFERENCES tos_versions(id) ON DELETE CASCADE,
    accepted_at TIMESTAMP NOT NULL DEFAULT now(),
    user_ip TEXT,
    user_agent TEXT
);
