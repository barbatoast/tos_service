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
    CREATE TABLE IF NOT EXISTS law_firms (
        id TEXT PRIMARY KEY,
        name TEXT NOT NULL,
        created_at TEXT NOT NULL
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

let addLawFirm (connectionString: string) (name: string) =
    use conn = new SqliteConnection($"Data Source={connectionString}")
    conn.Open()
    let id = Guid.NewGuid()
    let cmd = conn.CreateCommand()
    cmd.CommandText <- "INSERT INTO law_firms (id, name, created_at) VALUES ($id, $name, $created_at)"
    cmd.Parameters.AddWithValue("$id", id.ToString()) |> ignore
    cmd.Parameters.AddWithValue("$name", name) |> ignore
    cmd.Parameters.AddWithValue("$created_at", DateTime.UtcNow.ToString("o")) |> ignore
    cmd.ExecuteNonQuery() |> ignore
    id

// Firms

let getLawFirmCount (connectionString: string) (query: string) =
    use conn = new SqliteConnection($"Data Source={connectionString}")
    conn.Open()
    use cmd = conn.CreateCommand()
    cmd.CommandText <- "SELECT COUNT(*) FROM law_firms WHERE name LIKE $query"
    cmd.Parameters.AddWithValue("$query", "%" + query + "%") |> ignore
    let count = cmd.ExecuteScalar() :?> int64
    int count

let listLawFirms (connectionString: string) (page: int) (pageSize: int) (query: string) =
    let offset = (page - 1) * pageSize
    use conn = new SqliteConnection($"Data Source={connectionString}")
    conn.Open()
    use cmd = conn.CreateCommand()
    cmd.CommandText <- """
        SELECT id, name, created_at
        FROM law_firms
        WHERE name LIKE $query
        ORDER BY created_at DESC
        LIMIT $limit OFFSET $offset
    """
    cmd.Parameters.AddWithValue("$query", "%" + query + "%") |> ignore
    cmd.Parameters.AddWithValue("$limit", pageSize) |> ignore
    cmd.Parameters.AddWithValue("$offset", offset) |> ignore
    use reader = cmd.ExecuteReader()
    let results = ResizeArray<_>()
    while reader.Read() do
        results.Add
            {| id = reader.GetString(0)
               name = reader.GetString(1)
               createdAt = reader.GetString(2) |}
    List.ofSeq results

let getLawyerById (connectionString: string) (id: string) =
    use conn = new SqliteConnection($"Data Source={connectionString}")
    conn.Open()
    use cmd = conn.CreateCommand()
    cmd.CommandText <- "SELECT name, email FROM users WHERE id = $id"
    cmd.Parameters.AddWithValue("$id", id) |> ignore
    use reader = cmd.ExecuteReader()
    if reader.Read() then
        Some
            {| name = reader.GetString(0)
               email = reader.GetString(1)
               isAppUser = true |}
    else
        None
