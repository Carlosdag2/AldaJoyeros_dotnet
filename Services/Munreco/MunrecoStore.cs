using MySqlConnector;

namespace AldaJoyeros.Services.Munreco;

public sealed class MunrecoStore(IConfiguration configuration) {
    readonly SemaphoreSlim schemaGate=new(1,1);
    bool ready;
    public MySqlConnection Connection(bool pooled=true)=>new(new MySqlConnectionStringBuilder(configuration.GetConnectionString("DefaultConnection")??throw new InvalidOperationException("Falta la conexión MySQL")){Pooling=pooled,ConnectionTimeout=15,DefaultCommandTimeout=30}.ConnectionString);
    public async Task Ensure(CancellationToken ct=default) {
        if(ready)return;await schemaGate.WaitAsync(ct);
        try {
            if(ready)return;
            await using var sql=Connection();await sql.OpenAsync(ct);
            const string ddl="""
            CREATE TABLE IF NOT EXISTS munreco_jobs (
              id char(32) PRIMARY KEY, disparador varchar(20) NOT NULL, estado varchar(24) NOT NULL,
              progreso varchar(500) NOT NULL, solicitado_por varchar(80) NOT NULL,
              creado_utc datetime(6) NOT NULL, iniciado_utc datetime(6) NULL, terminado_utc datetime(6) NULL,
              resultado_json longtext NULL, activo tinyint NULL,
              UNIQUE KEY uq_munreco_activo(activo), KEY idx_munreco_fecha(creado_utc)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
            CREATE TABLE IF NOT EXISTS munreco_schedule (
              id int PRIMARY KEY, habilitado tinyint(1) NOT NULL, dia int NOT NULL, hora int NOT NULL,
              siguiente_utc datetime(6) NULL
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
            INSERT IGNORE INTO munreco_schedule VALUES(1,0,1,10,NULL);
            """;
            await using var c=new MySqlCommand(ddl,sql);await c.ExecuteNonQueryAsync(ct);ready=true;
        } finally {schemaGate.Release();}
    }
    public async Task<MunrecoSchedule> Schedule(CancellationToken ct=default) {
        await Ensure(ct);await using var sql=Connection();await sql.OpenAsync(ct);
        await using var c=new MySqlCommand("SELECT habilitado,dia,hora,siguiente_utc FROM munreco_schedule WHERE id=1",sql);
        await using var r=await c.ExecuteReaderAsync(ct);await r.ReadAsync(ct);
        return new(){Enabled=r.GetBoolean(0),Day=r.GetInt32(1),Hour=r.GetInt32(2),NextUtc=r.IsDBNull(3)?null:r.GetDateTime(3)};
    }
    public async Task SaveSchedule(MunrecoSchedule value,CancellationToken ct=default) {
        var next=MunrecoSchedule.Next(DateTime.UtcNow,value.Day,value.Hour);
        await Ensure(ct);await using var sql=Connection();await sql.OpenAsync(ct);
        await using var c=new MySqlCommand("UPDATE munreco_schedule SET habilitado=@enabled,dia=@day,hora=@hour,siguiente_utc=@next WHERE id=1",sql);
        c.Parameters.AddWithValue("@enabled",value.Enabled);c.Parameters.AddWithValue("@day",value.Day);c.Parameters.AddWithValue("@hour",value.Hour);c.Parameters.AddWithValue("@next",value.Enabled?next:DBNull.Value);await c.ExecuteNonQueryAsync(ct);
    }
    public async Task<string?> Enqueue(string trigger,string user,CancellationToken ct=default) {
        await Ensure(ct);await using var sql=Connection();await sql.OpenAsync(ct);
        var id=Guid.NewGuid().ToString("N");
        await using var c=new MySqlCommand("INSERT INTO munreco_jobs VALUES(@id,@trigger,'queued','Esperando el inicio de la actualización',@user,UTC_TIMESTAMP(6),NULL,NULL,NULL,1)",sql);
        c.Parameters.AddWithValue("@id",id);c.Parameters.AddWithValue("@trigger",trigger);c.Parameters.AddWithValue("@user",user);
        try {await c.ExecuteNonQueryAsync(ct);return id;}catch(MySqlException e) when(e.Number==1062){return null;}
    }
    public async Task ScheduleDue(CancellationToken ct) {
        await using var sql=Connection();await sql.OpenAsync(ct);await using var tx=await sql.BeginTransactionAsync(ct);
        MunrecoSchedule schedule;
        await using(var c=new MySqlCommand("SELECT habilitado,dia,hora,siguiente_utc FROM munreco_schedule WHERE id=1 FOR UPDATE",sql,tx)) {
            await using var r=await c.ExecuteReaderAsync(ct);await r.ReadAsync(ct);
            schedule=new(){Enabled=r.GetBoolean(0),Day=r.GetInt32(1),Hour=r.GetInt32(2),NextUtc=r.IsDBNull(3)?null:r.GetDateTime(3)};
        }
        if(!schedule.Enabled||schedule.NextUtc>DateTime.UtcNow){await tx.CommitAsync(ct);return;}
        await using(var c=new MySqlCommand("INSERT INTO munreco_jobs VALUES(@id,'monthly','queued','Actualización mensual pendiente','programacion',UTC_TIMESTAMP(6),NULL,NULL,NULL,1)",sql,tx)) {
            c.Parameters.AddWithValue("@id",Guid.NewGuid().ToString("N"));
            try {await c.ExecuteNonQueryAsync(ct);}catch(MySqlException e) when(e.Number==1062){await tx.RollbackAsync(ct);return;}
        }
        await using(var c=new MySqlCommand("UPDATE munreco_schedule SET siguiente_utc=@next WHERE id=1",sql,tx)) {
            c.Parameters.AddWithValue("@next",MunrecoSchedule.Next(DateTime.UtcNow,schedule.Day,schedule.Hour));await c.ExecuteNonQueryAsync(ct);
        }
        await tx.CommitAsync(ct);
    }
    public async Task<IReadOnlyList<MunrecoJob>> Jobs(CancellationToken ct=default) {
        await Ensure(ct);await using var sql=Connection();await sql.OpenAsync(ct);
        await using var c=new MySqlCommand("SELECT id,disparador,estado,progreso,creado_utc,iniciado_utc,terminado_utc,resultado_json FROM munreco_jobs ORDER BY creado_utc DESC LIMIT 30",sql);
        await using var r=await c.ExecuteReaderAsync(ct);var rows=new List<MunrecoJob>();
        while(await r.ReadAsync(ct))rows.Add(new(r.GetString(0),r.GetString(1),r.GetString(2),r.GetString(3),r.GetDateTime(4),r.IsDBNull(5)?null:r.GetDateTime(5),r.IsDBNull(6)?null:r.GetDateTime(6),r.IsDBNull(7)?null:r.GetString(7)));
        return rows;
    }
    public async Task Update(string id,string state,string progress,string? result=null,CancellationToken ct=default) {
        await using var sql=Connection();await sql.OpenAsync(ct);
        await using var c=new MySqlCommand("UPDATE munreco_jobs SET estado=@state,progreso=@progress,resultado_json=@result,activo=IF(@state IN ('queued','running'),1,NULL),iniciado_utc=IF(@state='running',COALESCE(iniciado_utc,UTC_TIMESTAMP(6)),iniciado_utc),terminado_utc=IF(@state IN ('queued','running'),NULL,UTC_TIMESTAMP(6)) WHERE id=@id",sql);
        c.Parameters.AddWithValue("@id",id);c.Parameters.AddWithValue("@state",state);c.Parameters.AddWithValue("@progress",progress);c.Parameters.AddWithValue("@result",(object?)result??DBNull.Value);await c.ExecuteNonQueryAsync(ct);
    }
    public async Task RecoverInterrupted(CancellationToken ct) {
        await using var sql=Connection();await sql.OpenAsync(ct);
        await using var c=new MySqlCommand("UPDATE munreco_jobs SET estado='interrupted',activo=NULL,terminado_utc=UTC_TIMESTAMP(6),progreso='La aplicación se reinició durante la actualización. Puedes volver a ejecutarla.' WHERE estado='running'",sql);await c.ExecuteNonQueryAsync(ct);
    }
}
