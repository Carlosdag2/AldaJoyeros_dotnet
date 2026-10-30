using MySqlConnector;
namespace AldaJoyeros.Services.Duward;
public sealed class DuwardStore(IConfiguration configuration) {
 readonly SemaphoreSlim schemaGate=new(1,1);bool ready;
 public MySqlConnection Connection(bool pooled=true)=>new(new MySqlConnectionStringBuilder(configuration.GetConnectionString("DefaultConnection")??throw new InvalidOperationException("Falta la conexión MySQL")){Pooling=pooled,ConnectionTimeout=15,DefaultCommandTimeout=30}.ConnectionString);
 public async Task Ensure(CancellationToken ct=default) {
  if(ready)return;await schemaGate.WaitAsync(ct);try{if(ready)return;await using var sql=Connection();await sql.OpenAsync(ct);
   await using var c=new MySqlCommand("""
    CREATE TABLE IF NOT EXISTS duward_jobs (
     id char(32) PRIMARY KEY,disparador varchar(20) NOT NULL,estado varchar(24) NOT NULL,
     progreso varchar(500) NOT NULL,solicitado_por varchar(80) NOT NULL,creado_utc datetime(6) NOT NULL,
     iniciado_utc datetime(6) NULL,terminado_utc datetime(6) NULL,resultado_json longtext NULL,activo tinyint NULL,
     UNIQUE KEY uq_duward_activo(activo),KEY idx_duward_fecha(creado_utc)
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
   """,sql);await c.ExecuteNonQueryAsync(ct);ready=true;
  }finally{schemaGate.Release();}
 }
 public async Task<IReadOnlyList<DuwardJob>> Jobs(CancellationToken ct=default) {
  await Ensure(ct);await using var sql=Connection();await sql.OpenAsync(ct);await using var c=new MySqlCommand("SELECT id,disparador,estado,progreso,creado_utc,iniciado_utc,terminado_utc,resultado_json FROM duward_jobs ORDER BY creado_utc DESC LIMIT 30",sql);
  await using var r=await c.ExecuteReaderAsync(ct);var jobs=new List<DuwardJob>();while(await r.ReadAsync(ct))jobs.Add(new(r.GetString(0),r.GetString(1),r.GetString(2),r.GetString(3),r.GetDateTime(4),r.IsDBNull(5)?null:r.GetDateTime(5),r.IsDBNull(6)?null:r.GetDateTime(6),r.IsDBNull(7)?null:r.GetString(7)));return jobs;
 }
 public async Task<string?> Enqueue(string trigger,string user,string folder,DuwardSource source,CancellationToken ct) {
  await Ensure(ct);await using var sql=Connection();await sql.OpenAsync(ct);await using var tx=await sql.BeginTransactionAsync(ct);var id=Guid.NewGuid().ToString("N");
  await using var c=new MySqlCommand("INSERT INTO duward_jobs VALUES(@id,@trigger,'queued','CSV validados. Esperando al proceso de importación',@user,UTC_TIMESTAMP(6),NULL,NULL,NULL,1)",sql,tx);c.Parameters.AddWithValue("@id",id);c.Parameters.AddWithValue("@trigger",trigger);c.Parameters.AddWithValue("@user",user);
  try{await c.ExecuteNonQueryAsync(ct);}catch(MySqlException e) when(e.Number==1062){await tx.RollbackAsync(ct);return null;}
  Directory.Move(folder,source.Run(id));await tx.CommitAsync(ct);return id;
 }
 public async Task Update(string id,string state,string progress,string? result=null,CancellationToken ct=default) {
  await using var sql=Connection();await sql.OpenAsync(ct);await using var c=new MySqlCommand("UPDATE duward_jobs SET estado=@state,progreso=@progress,resultado_json=@result,activo=IF(@state IN ('queued','running'),1,NULL),iniciado_utc=IF(@state='running',COALESCE(iniciado_utc,UTC_TIMESTAMP(6)),iniciado_utc),terminado_utc=IF(@state IN ('queued','running'),NULL,UTC_TIMESTAMP(6)) WHERE id=@id",sql);
  c.Parameters.AddWithValue("@id",id);c.Parameters.AddWithValue("@state",state);c.Parameters.AddWithValue("@progress",progress);c.Parameters.AddWithValue("@result",(object?)result??DBNull.Value);await c.ExecuteNonQueryAsync(ct);
 }
 public async Task Recover(CancellationToken ct){await using var sql=Connection();await sql.OpenAsync(ct);await using var c=new MySqlCommand("UPDATE duward_jobs SET estado='interrupted',activo=NULL,terminado_utc=UTC_TIMESTAMP(6),progreso='La aplicación se reinició durante la importación. Revisa el informe y vuelve a intentarlo.' WHERE estado='running'",sql);await c.ExecuteNonQueryAsync(ct);}
}
