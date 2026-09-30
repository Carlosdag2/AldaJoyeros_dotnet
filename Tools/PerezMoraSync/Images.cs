using MongoDB.Bson;
using MongoDB.Driver;
using System.Text;

namespace PerezMoraSync;

public sealed record CachedImage(string Url,string Hash,string File,string Mime);
public sealed class ImageCache(string root,bool offline=false) {
    readonly HttpClient client=new(new HttpClientHandler{AllowAutoRedirect=false}){Timeout=TimeSpan.FromSeconds(40)};
    public System.Collections.Concurrent.ConcurrentDictionary<string,string> Failures { get; }=new();
    public async Task<Dictionary<string,CachedImage>> Download(IEnumerable<string> urls) {
        Directory.CreateDirectory(root);
        var results=new System.Collections.Concurrent.ConcurrentDictionary<string,CachedImage>();
        int done=0;var unique=urls.Distinct().ToArray();
        await Parallel.ForEachAsync(unique,new ParallelOptions{MaxDegreeOfParallelism=3},async (url,ct)=> {
            if(!Catalog.AllowedImage(url))throw new InvalidDataException("Imagen fuera del proveedor.");
            var path=Path.Combine(root,Catalog.Hash(url)+".image");
            byte[] bytes;
            try {
            if(offline&&File.Exists(path))bytes=await File.ReadAllBytesAsync(path,ct);
            else {
                bytes=[];
                for(int attempt=0;attempt<3;attempt++) {
                    try {
                        using var request=new HttpRequestMessage(HttpMethod.Get,url);
                        if(File.Exists(path+".etag"))request.Headers.TryAddWithoutValidation("If-None-Match",await File.ReadAllTextAsync(path+".etag",ct));
                        if(File.Exists(path))request.Headers.IfModifiedSince=File.GetLastWriteTimeUtc(path);
                        using var response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,ct);
                        if(response.StatusCode==System.Net.HttpStatusCode.NotModified&&File.Exists(path)){bytes=await File.ReadAllBytesAsync(path,ct);break;}
                        response.EnsureSuccessStatusCode();
                        if(response.Content.Headers.ContentLength>5*1024*1024)throw new InvalidDataException("Imagen demasiado grande.");
                        using var memory=new MemoryStream();await using var stream=await response.Content.ReadAsStreamAsync(ct);var buffer=new byte[65536];int read;
                        while((read=await stream.ReadAsync(buffer,ct))>0){if(memory.Length+read>5*1024*1024)throw new InvalidDataException("Imagen demasiado grande.");memory.Write(buffer,0,read);}
                        bytes=memory.ToArray();Mime(bytes);
                        if(response.Headers.ETag!=null)await File.WriteAllTextAsync(path+".etag",response.Headers.ETag.ToString(),ct);
                        break;
                    }catch(HttpRequestException) when(attempt<2){await Task.Delay(500*(attempt+1),ct);}
                }
                await File.WriteAllBytesAsync(path+".tmp",bytes,ct);File.Move(path+".tmp",path,true);await Task.Delay(100,ct);
            }
            results[url]=new(url,Catalog.Hash(bytes),path,Mime(bytes));
            }catch(Exception e) when(e is HttpRequestException or InvalidDataException or TaskCanceledException) {Failures[url]=e.GetType().Name;}
            var current=Interlocked.Increment(ref done);if(current%100==0||current==unique.Length)Console.WriteLine($"IMAGES {current}/{unique.Length}");
        });
        return new(results);
    }
    public static string Mime(byte[] bytes) {
        if(bytes.Length is <12 or >5*1024*1024)throw new InvalidDataException("Imagen vacía o inválida.");
        if(bytes[0]==0xff&&bytes[1]==0xd8&&bytes[2]==0xff)return "image/jpeg";
        if(bytes.AsSpan(0,8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10}))return "image/png";
        if(Encoding.ASCII.GetString(bytes,0,6) is "GIF87a" or "GIF89a")return "image/gif";
        if(Encoding.ASCII.GetString(bytes,0,4)=="RIFF"&&Encoding.ASCII.GetString(bytes,8,4)=="WEBP")return "image/webp";
        throw new InvalidDataException("La descarga no es una imagen compatible.");
    }
    public static async Task<string[]> Store(IMongoCollection<BsonDocument> collection,long productId,IEnumerable<CachedImage> wanted,string[] previous) {
        var current=await collection.Find(Builders<BsonDocument>.Filter.Eq("producto_id",productId)).ToListAsync();
        var byHash=current.Where(x=>x.TryGetValue("imagen_data",out var data)&&data.IsBsonBinaryData)
            .GroupBy(x=>Catalog.Hash(x["imagen_data"].AsBsonBinaryData.Bytes)).ToDictionary(x=>x.Key,x=>x.First());
        var selected=new List<string>();var distinct=wanted.DistinctBy(x=>x.Hash).ToArray();
        var manual=current.Where(x=>!Owned(x,productId)).ToArray();
        var manualPrincipal=manual.Any(x=>x.GetValue("es_principal",false).ToBoolean());
        int order=current.Count==0?0:current.Max(x=>x.GetValue("orden",0).ToInt32())+1;
        foreach(var image in distinct) {
            if(byHash.TryGetValue(image.Hash,out var existing)){selected.Add(existing["_id"].ToString()!);continue;}
            var bytes=await File.ReadAllBytesAsync(image.File);if(Catalog.Hash(bytes)!=image.Hash)throw new InvalidDataException("La imagen cambió después de validarla.");
            var id=new ObjectId(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes("PerezMora|"+productId+"|"+image.Hash)).Take(12).ToArray());
            var doc=new BsonDocument{{"_id",id},{"producto_id",new BsonInt64(productId)},{"imagen_data",new BsonBinaryData(bytes)},
                {"tipo_mime",image.Mime},{"tamano_bytes",bytes.Length},{"orden",order++},{"es_principal",false},
                {"fecha_creacion",new BsonDateTime(DateTime.UtcNow)},{"nombre_archivo",Path.GetFileName(new Uri(image.Url).AbsolutePath)}};
            await collection.ReplaceOneAsync(Builders<BsonDocument>.Filter.Eq("_id",id),doc,new ReplaceOptions{IsUpsert=true});selected.Add(id.ToString());
        }
        // Keep manual pictures; replace only images previously linked by this provider.
        for(int i=0;i<selected.Count;i++) {
            var id=ObjectId.Parse(selected[i]);
            if(manual.Any(x=>x["_id"].AsObjectId==id))continue;
            await collection.UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("_id",id),Builders<BsonDocument>.Update.Set("orden",i).Set("es_principal",!manualPrincipal&&i==0));
        }
        return selected.ToArray();
    }
    public static async Task RemoveOld(IMongoCollection<BsonDocument> collection,long productId,string[] previous,string[] selected) {
        var stale=previous.Except(selected).Select(ObjectId.Parse).ToArray();
        if(stale.Length==0)return;
        var filter=Builders<BsonDocument>.Filter.Eq("producto_id",productId)&Builders<BsonDocument>.Filter.In("_id",stale);
        var docs=await collection.Find(filter).ToListAsync();var owned=docs.Where(x=>Owned(x,productId)).Select(x=>x["_id"].AsObjectId).ToArray();
        if(owned.Length>0)await collection.DeleteManyAsync(Builders<BsonDocument>.Filter.Eq("producto_id",productId)&Builders<BsonDocument>.Filter.In("_id",owned));
    }
    static bool Owned(BsonDocument doc,long productId) {
        if(!doc.TryGetValue("imagen_data",out var bytes)||!bytes.IsBsonBinaryData)return false;
        var expected=new ObjectId(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes("PerezMora|"+productId+"|"+Catalog.Hash(bytes.AsBsonBinaryData.Bytes))).Take(12).ToArray());
        return doc["_id"].AsObjectId==expected;
    }
}
