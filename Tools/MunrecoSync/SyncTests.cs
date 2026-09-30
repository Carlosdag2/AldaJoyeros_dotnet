using System.Text.Json.Nodes;

static class SyncTests {
    public static void Run() {
        int passed=0;
        void Check(bool result,string name){if(!result)throw new Exception("FAIL "+name);passed++;Console.WriteLine("PASS "+name);}
        var time=new DateTime(2026,9,29,12,0,0);var active=new ManagedState(false,null,false,"disponible");
        var empty=new ManagedState(true,time,false,"sin_existencias");
        Check(SyncPolicy.Decide(true,false,null,active,false,0,false)==new Visibility(true,false,"retirado_proveedor"),"Retirada oculta sin eliminar el producto");
        Check(!SyncPolicy.Decide(true,true,time,empty,true,5,false).Hidden,"Reposicion reactiva producto agotado automaticamente");
        Check(SyncPolicy.Decide(true,false,null,active,true,0,false).Hidden,"Agotado oculta producto visible");
        var manual=SyncPolicy.Decide(true,true,time,active,true,5,false);
        Check(manual.Manual&&manual.Hidden,"Borrado manual no se reactiva");
        var secondDelete=SyncPolicy.Decide(true,true,time.AddHours(1),empty,true,5,false);
        Check(secondDelete.Manual&&secondDelete.Hidden,"Cambio manual de fecha de ocultacion se conserva");
        Check(!SyncPolicy.Decide(false,false,null,active,false,0,false).Hidden,"Catalogo original no cambia de visibilidad");
        Check(SyncPolicy.Decide(true,false,null,active,true,5,true).Hidden,"Datos incompletos quedan ocultos");
        Check(!SyncPolicy.Bootstrap(true,true,time,0,false,time.AddMilliseconds(500)).Manual,"Ocultacion de importacion inicial reconocida");
        Check(SyncPolicy.Bootstrap(true,true,time,0,false,time.AddHours(-1)).Manual,"Ocultacion posterior se trata como manual");
        Check(!SyncPolicy.Bootstrap(true,true,time,0,false,time.AddMinutes(2),time.AddMinutes(5)).Manual,"Reimportacion anterior a la verificacion inicial sigue automatica");
        Check(SyncPolicy.Bootstrap(true,true,time.AddHours(1),0,false,time,time.AddMinutes(5)).Manual,"Borrado posterior a la verificacion inicial sigue manual");
        var a=JsonNode.Parse("{\"model\":\"ABC\",\"quantity\":\"0\",\"price\":\"10\"}")!.AsObject();
        var b=a.DeepClone().AsObject();b["quantity"]="8";
        Check(JsonNode.DeepEquals(SyncPolicy.DetailComparable(a),SyncPolicy.DetailComparable(b)),"Reposicion no obliga a descargar otra vez fotos sin cambios");
        b["price"]="12";
        Check(!JsonNode.DeepEquals(SyncPolicy.DetailComparable(a),SyncPolicy.DetailComparable(b)),"Cambio PVP provoca revision de ficha");
        a["quantity"]="desconocido";bool bad=false;try{SyncPolicy.Quantity(a);}catch(InvalidOperationException){bad=true;}
        Check(bad,"Cantidades malformadas bloquean la sincronizacion");
        var all=new JsonArray();var sections=new JsonArray();var prior=new List<(string Source,string Section)>();
        foreach(var section in CatalogCrawler.Sections) {
            for(int i=0;i<20;i++){var id=section.Id+"-"+i;all.Add(new JsonObject{["product_id"]=id,["model"]=id,["quantity"]="1",["_source_section"]=section.Id});prior.Add((id,section.Id));}
            sections.Add(new JsonObject{["Id"]=section.Id,["products"]=20,["pages"]=2});
        }
        var coverage=new JsonObject{["products"]=80,["sections"]=sections};SyncPolicy.ValidateCoverage(all,coverage,prior);
        Check(true,"Catalogo completo valido");
        for(int i=0;i<6;i++)all.RemoveAt(0);coverage["products"]=74;sections[0]!["products"]=14;
        bad=false;try{SyncPolicy.ValidateCoverage(all,coverage,prior);}catch(InvalidOperationException){bad=true;}
        Check(bad,"Retirada masiva se bloquea antes de escribir");
        all.Clear();coverage["products"]=0;
        bad=false;try{SyncPolicy.ValidateCoverage(all,coverage,prior);}catch(InvalidOperationException){bad=true;}
        Check(bad,"Catalogo vacio no vacia la tienda");
        Console.WriteLine($"ALL {passed} SYNC CHECKS PASSED");
    }
}
