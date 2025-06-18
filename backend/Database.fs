module Database

open System
open Microsoft.Data.Sqlite

let initDb path =
    use conn = new SqliteConnection $"Data Source={path}"
    conn.Open()
    let cmd = conn.CreateCommand()
    cmd.CommandText <- """
    CREATE TABLE IF NOT EXISTS users (
        id TEXT PRIMARY KEY,
        email TEXT NOT NULL,
        name TEXT
    );
    CREATE TABLE IF NOT EXISTS tos_versions (
        id INTEGER PRIMARY KEY AUTOINCREMENT,
        version TEXT NOT NULL,
        content TEXT NOT NULL,
        published_at TEXT NOT NULL
    );
    CREATE TABLE IF NOT EXISTS tos_acceptance (
        id INTEGER PRIMARY KEY AUTOINCREMENT,
        user_id TEXT NOT NULL,
        tos_id INTEGER NOT NULL,
        accepted_at TEXT NOT NULL,
        user_ip TEXT,
        user_agent TEXT,
        FOREIGN KEY(user_id) REFERENCES users(id),
        FOREIGN KEY(tos_id) REFERENCES tos_versions(id)
    );
    """
    cmd.ExecuteNonQuery() |> ignore

let addUser connectionString email name =
    use conn = new SqliteConnection $"Data Source={connectionString}"
    conn.Open()
    let id = Guid.NewGuid()
    let cmd = conn.CreateCommand()
    cmd.CommandText <- "INSERT INTO users (id, email, name) VALUES ($id, $email, $name)"
    cmd.Parameters.AddWithValue("$id", id.ToString()) |> ignore
    cmd.Parameters.AddWithValue("$email", email) |> ignore
    cmd.Parameters.AddWithValue("$name", name) |> ignore
    cmd.ExecuteNonQuery() |> ignore
    id

let publishTos connectionString version content =
    use conn = new SqliteConnection $"Data Source={connectionString}"
    conn.Open()
    let cmd = conn.CreateCommand()
    cmd.CommandText <- "INSERT INTO tos_versions (version, content, published_at) VALUES ($version, $content, $published_at)"
    cmd.Parameters.AddWithValue("$version", version) |> ignore
    cmd.Parameters.AddWithValue("$content", content) |> ignore
    cmd.Parameters.AddWithValue("$published_at", DateTime.UtcNow.ToString("o")) |> ignore
    cmd.ExecuteNonQuery() |> ignore

let acceptTos (connectionString: string) (userId: Guid) (tosId: int) (userIp: string) (userAgent: string) : Result<unit, string> =
    try
        use conn = new SqliteConnection($"Data Source={connectionString}")
        conn.Open()

        use cmd = conn.CreateCommand()
        cmd.CommandText <- """
            INSERT INTO tos_acceptance (user_id, tos_id, accepted_at, user_ip, user_agent)
            VALUES ($user_id, $tos_id, $accepted_at, $ip, $ua)
        """
        cmd.Parameters.AddWithValue("$user_id", userId.ToString()) |> ignore
        cmd.Parameters.AddWithValue("$tos_id", tosId) |> ignore
        cmd.Parameters.AddWithValue("$accepted_at", DateTime.UtcNow.ToString("o")) |> ignore
        cmd.Parameters.AddWithValue("$ip", userIp) |> ignore
        cmd.Parameters.AddWithValue("$ua", userAgent) |> ignore

        cmd.ExecuteNonQuery() |> ignore
        Ok ()
    with ex ->
        Error $"Failed to insert ToS acceptance: {ex.Message}"

let listUsers (connectionString: string) (page: int) (pageSize: int) =
    let offset = (page - 1) * pageSize

    use conn = new SqliteConnection($"Data Source={connectionString}")
    conn.Open()

    use cmd = conn.CreateCommand()
    cmd.CommandText <- """
        SELECT id, email, name
        FROM users
        ORDER BY name
        LIMIT $limit OFFSET $offset
    """
    cmd.Parameters.AddWithValue("$limit", pageSize) |> ignore
    cmd.Parameters.AddWithValue("$offset", offset) |> ignore

    use reader = cmd.ExecuteReader()
    let results = ResizeArray<_>()

    while reader.Read() do
        let user =
            {| id = reader.GetString(0)
               email = reader.GetString(1)
               name = if not (reader.IsDBNull(2)) then reader.GetString(2) else null |}
        results.Add(user)

    List.ofSeq results

let getUserCount (connectionString: string) =
    use conn = new SqliteConnection($"Data Source={connectionString}")
    conn.Open()

    use cmd = conn.CreateCommand()
    cmd.CommandText <- "SELECT COUNT(*) FROM users"
    let count = cmd.ExecuteScalar() :?> int64
    int count
