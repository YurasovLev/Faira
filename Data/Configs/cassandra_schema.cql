CREATE KEYSPACE IF NOT EXISTS FAIRA WITH replication = {'class': 'SimpleStrategy', 'replication_factor' : 1};

CREATE TABLE IF NOT EXISTS FAIRA.USERS (
  id text,
  name text,
  password text,
  email text,
  PRIMARY KEY (id)
);

CREATE TABLE IF NOT EXISTS FAIRA.CHANNELS (
  id text,
  name text,
  type text,
  children set<text>,
  PRIMARY KEY (id)
);

CREATE TABLE IF NOT EXISTS FAIRA.MESSAGES (
  id text,
  channel text,
  content text,
  author text,
  type text,
  PRIMARY KEY (channel, id)
);

CREATE TABLE IF NOT EXISTS FAIRA.USER_CREATION_REQUESTS (
  id text,
  name text,
  password text,
  email text,
  ticket text,
  PRIMARY KEY (ticket)
);

CREATE TABLE IF NOT EXISTS FAIRA.RESET_PASSWORD_REQUESTS (
  email text,
  code text,
  PRIMARY KEY (code)
);