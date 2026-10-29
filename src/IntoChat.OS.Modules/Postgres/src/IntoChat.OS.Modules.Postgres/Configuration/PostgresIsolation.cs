namespace IntoChat.OS.Modules.Postgres;

// Where a deployment places tables that no app slot redirects.
public enum PostgresIsolation { Shared, DatabasePerBrain }
