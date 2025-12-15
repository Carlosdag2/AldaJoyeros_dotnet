namespace AldaJoyeros.Constants
{
    /// <summary>
    /// Constantes con los datos de códigos postales de España.
    /// Siguiendo el principio de Single Responsibility: esta clase solo contiene datos estáticos.
    /// </summary>
    public static class CodigoPostalData
    {
        /// <summary>
        /// Longitud válida de un código postal español
        /// </summary>
        public const int LongitudCodigoPostal = 5;

        /// <summary>
        /// Mapeo de los primeros 2 dígitos del código postal a provincias de España
        /// </summary>
        public static IReadOnlyDictionary<string, string> PrefijosProvincias { get; } = new Dictionary<string, string>
        {
            { "01", "Álava" },
            { "02", "Albacete" },
            { "03", "Alicante" },
            { "04", "Almería" },
            { "05", "Ávila" },
            { "06", "Badajoz" },
            { "07", "Baleares" },
            { "08", "Barcelona" },
            { "09", "Burgos" },
            { "10", "Cáceres" },
            { "11", "Cádiz" },
            { "12", "Castellón" },
            { "13", "Ciudad Real" },
            { "14", "Córdoba" },
            { "15", "A Coruña" },
            { "16", "Cuenca" },
            { "17", "Girona" },
            { "18", "Granada" },
            { "19", "Guadalajara" },
            { "20", "Gipuzkoa" },
            { "21", "Huelva" },
            { "22", "Huesca" },
            { "23", "Jaén" },
            { "24", "León" },
            { "25", "Lleida" },
            { "26", "La Rioja" },
            { "27", "Lugo" },
            { "28", "Madrid" },
            { "29", "Málaga" },
            { "30", "Murcia" },
            { "31", "Navarra" },
            { "32", "Ourense" },
            { "33", "Asturias" },
            { "34", "Palencia" },
            { "35", "Las Palmas" },
            { "36", "Pontevedra" },
            { "37", "Salamanca" },
            { "38", "Santa Cruz de Tenerife" },
            { "39", "Cantabria" },
            { "40", "Segovia" },
            { "41", "Sevilla" },
            { "42", "Soria" },
            { "43", "Tarragona" },
            { "44", "Teruel" },
            { "45", "Toledo" },
            { "46", "Valencia" },
            { "47", "Valladolid" },
            { "48", "Vizcaya" },
            { "49", "Zamora" },
            { "50", "Zaragoza" },
            { "51", "Ceuta" },
            { "52", "Melilla" }
        }.AsReadOnly();

        /// <summary>
        /// Ciudades principales por provincia
        /// </summary>
        public static IReadOnlyDictionary<string, IReadOnlyList<string>> CiudadesPorProvincia { get; } = new Dictionary<string, IReadOnlyList<string>>
        {
            { "Álava", new[] { "Vitoria-Gasteiz", "Llodio", "Amurrio", "Salvatierra", "Oyón" } },
            { "Albacete", new[] { "Albacete", "Hellín", "Villarrobledo", "Almansa", "La Roda", "Caudete" } },
            { "Alicante", new[] { "Alicante", "Elche", "Torrevieja", "Orihuela", "Benidorm", "Alcoy", "Elda", "San Vicente del Raspeig", "Dénia", "Villena" } },
            { "Almería", new[] { "Almería", "El Ejido", "Roquetas de Mar", "Níjar", "Adra", "Vícar" } },
            { "Ávila", new[] { "Ávila", "Arévalo", "Arenas de San Pedro", "Las Navas del Marqués" } },
            { "Badajoz", new[] { "Badajoz", "Mérida", "Don Benito", "Almendralejo", "Villanueva de la Serena", "Zafra" } },
            { "Baleares", new[] { "Palma de Mallorca", "Ibiza", "Manacor", "Inca", "Ciutadella de Menorca", "Mahón", "Llucmajor" } },
            { "Barcelona", new[] { "Barcelona", "L'Hospitalet de Llobregat", "Badalona", "Terrassa", "Sabadell", "Mataró", "Santa Coloma de Gramenet", "Cornellà de Llobregat", "Sant Boi de Llobregat", "Rubí", "Manresa", "Granollers", "Cerdanyola del Vallès", "El Prat de Llobregat" } },
            { "Burgos", new[] { "Burgos", "Miranda de Ebro", "Aranda de Duero", "Briviesca" } },
            { "Cáceres", new[] { "Cáceres", "Plasencia", "Navalmoral de la Mata", "Coria", "Trujillo" } },
            { "Cádiz", new[] { "Cádiz", "Jerez de la Frontera", "Algeciras", "San Fernando", "El Puerto de Santa María", "Chiclana de la Frontera", "Sanlúcar de Barrameda", "La Línea de la Concepción", "Rota" } },
            { "Castellón", new[] { "Castellón de la Plana", "Vila-real", "Burriana", "La Vall d'Uixó", "Vinaròs", "Benicarló" } },
            { "Ciudad Real", new[] { "Ciudad Real", "Puertollano", "Tomelloso", "Alcázar de San Juan", "Valdepeñas", "Manzanares", "Daimiel" } },
            { "Córdoba", new[] { "Córdoba", "Lucena", "Puente Genil", "Montilla", "Priego de Córdoba", "Cabra", "Baena" } },
            { "A Coruña", new[] { "A Coruña", "Santiago de Compostela", "Ferrol", "Narón", "Oleiros", "Carballo", "Culleredo", "Arteixo", "Cambre" } },
            { "Cuenca", new[] { "Cuenca", "Tarancón", "San Clemente", "Motilla del Palancar" } },
            { "Girona", new[] { "Girona", "Figueres", "Blanes", "Lloret de Mar", "Olot", "Salt", "Palafrugell", "Sant Feliu de Guíxols", "Roses" } },
            { "Granada", new[] { "Granada", "Motril", "Almuñécar", "Armilla", "Maracena", "Baza", "Loja", "Guadix" } },
            { "Guadalajara", new[] { "Guadalajara", "Azuqueca de Henares", "Alovera", "El Casar", "Cabanillas del Campo" } },
            { "Gipuzkoa", new[] { "San Sebastián", "Irún", "Errenteria", "Eibar", "Zarautz", "Arrasate", "Hernani", "Tolosa", "Hondarribia" } },
            { "Huelva", new[] { "Huelva", "Lepe", "Almonte", "Isla Cristina", "Moguer", "Ayamonte" } },
            { "Huesca", new[] { "Huesca", "Monzón", "Barbastro", "Fraga", "Jaca", "Binéfar" } },
            { "Jaén", new[] { "Jaén", "Linares", "Úbeda", "Martos", "Andújar", "Alcalá la Real", "Baeza" } },
            { "León", new[] { "León", "Ponferrada", "San Andrés del Rabanedo", "Villaquilambre", "Astorga" } },
            { "Lleida", new[] { "Lleida", "Balaguer", "Tàrrega", "La Seu d'Urgell", "Mollerussa" } },
            { "La Rioja", new[] { "Logroño", "Calahorra", "Arnedo", "Haro", "Alfaro", "Lardero", "Nájera" } },
            { "Lugo", new[] { "Lugo", "Monforte de Lemos", "Viveiro", "Vilalba", "Sarria", "Foz" } },
            { "Madrid", new[] { "Madrid", "Móstoles", "Alcalá de Henares", "Fuenlabrada", "Leganés", "Getafe", "Alcorcón", "Torrejón de Ardoz", "Parla", "Alcobendas", "Las Rozas de Madrid", "San Sebastián de los Reyes", "Pozuelo de Alarcón", "Coslada", "Rivas-Vaciamadrid", "Valdemoro", "Majadahonda", "Collado Villalba", "Aranjuez", "Arganda del Rey", "Boadilla del Monte", "Pinto", "Colmenar Viejo", "Tres Cantos", "San Fernando de Henares", "Galapagar", "Torrelodones", "Navalcarnero", "Ciempozuelos", "Villanueva de la Cañada", "Arroyomolinos", "Humanes de Madrid" } },
            { "Málaga", new[] { "Málaga", "Marbella", "Mijas", "Vélez-Málaga", "Fuengirola", "Torremolinos", "Benalmádena", "Estepona", "Rincón de la Victoria", "Antequera", "Alhaurín de la Torre", "Nerja", "Ronda", "Alhaurín el Grande" } },
            { "Murcia", new[] { "Murcia", "Cartagena", "Lorca", "Molina de Segura", "Alcantarilla", "Mazarrón", "Cieza", "Águilas", "Yecla", "Torre-Pacheco", "Totana", "San Javier", "Caravaca de la Cruz", "Jumilla", "San Pedro del Pinatar" } },
            { "Navarra", new[] { "Pamplona", "Tudela", "Barañáin", "Burlada", "Estella", "Zizur Mayor", "Tafalla", "Villava", "Ansoáin" } },
            { "Ourense", new[] { "Ourense", "Verín", "O Barco de Valdeorras", "O Carballiño", "Xinzo de Limia" } },
            { "Asturias", new[] { "Oviedo", "Gijón", "Avilés", "Siero", "Langreo", "Mieres", "Castrillón", "San Martín del Rey Aurelio" } },
            { "Palencia", new[] { "Palencia", "Aguilar de Campoo", "Guardo", "Venta de Baños" } },
            { "Las Palmas", new[] { "Las Palmas de Gran Canaria", "Telde", "Santa Lucía de Tirajana", "Arrecife", "San Bartolomé de Tirajana", "Arucas", "Ingenio", "Agüimes", "Puerto del Rosario", "La Oliva", "Gáldar", "Teguise" } },
            { "Pontevedra", new[] { "Vigo", "Pontevedra", "Vilagarcía de Arousa", "Redondela", "Cangas", "Marín", "O Porriño", "Ponteareas", "Lalín", "Sanxenxo", "Tui" } },
            { "Salamanca", new[] { "Salamanca", "Santa Marta de Tormes", "Béjar", "Ciudad Rodrigo", "Carbajosa de la Sagrada" } },
            { "Santa Cruz de Tenerife", new[] { "Santa Cruz de Tenerife", "San Cristóbal de La Laguna", "Arona", "Adeje", "Granadilla de Abona", "La Orotava", "Los Realejos", "Puerto de la Cruz", "San Miguel de Abona", "Icod de los Vinos", "Candelaria", "Tacoronte" } },
            { "Cantabria", new[] { "Santander", "Torrelavega", "Camargo", "Castro-Urdiales", "Piélagos", "El Astillero", "Laredo", "Santoña", "Los Corrales de Buelna", "Santa Cruz de Bezana" } },
            { "Segovia", new[] { "Segovia", "Cuéllar", "El Espinar", "San Ildefonso" } },
            { "Sevilla", new[] { "Sevilla", "Dos Hermanas", "Alcalá de Guadaíra", "Utrera", "Mairena del Aljarafe", "Écija", "La Rinconada", "Los Palacios y Villafranca", "Coria del Río", "Tomares", "Carmona", "San Juan de Aznalfarache", "Bormujos", "Lebrija", "Camas", "Morón de la Frontera", "Marchena", "Osuna" } },
            { "Soria", new[] { "Soria", "Almazán", "El Burgo de Osma", "San Esteban de Gormaz" } },
            { "Tarragona", new[] { "Tarragona", "Reus", "Tortosa", "El Vendrell", "Cambrils", "Salou", "Valls", "Amposta", "Vila-seca", "Calafell" } },
            { "Teruel", new[] { "Teruel", "Alcañiz", "Andorra", "Calamocha" } },
            { "Toledo", new[] { "Toledo", "Talavera de la Reina", "Illescas", "Seseña", "Torrijos", "Sonseca", "Madridejos", "Consuegra", "Quintanar de la Orden", "Fuensalida", "Mora", "Ocaña", "Villacañas", "Bargas", "Yeles", "Yuncos", "Esquivias" } },
            { "Valencia", new[] { "Valencia", "Torrent", "Gandía", "Paterna", "Sagunto", "Mislata", "Burjassot", "Ontinyent", "Aldaia", "Manises", "Alfafar", "Xirivella", "Alaquàs", "Quart de Poblet", "Catarroja", "La Pobla de Vallbona", "Sueca", "Alzira", "Xàtiva", "Cullera", "Oliva", "Bétera", "Paiporta", "Requena", "L'Eliana", "Llíria" } },
            { "Valladolid", new[] { "Valladolid", "Medina del Campo", "Laguna de Duero", "Arroyo de la Encomienda", "Tordesillas" } },
            { "Vizcaya", new[] { "Bilbao", "Barakaldo", "Getxo", "Portugalete", "Santurtzi", "Basauri", "Leioa", "Durango", "Galdakao", "Erandio", "Sestao", "Ermua" } },
            { "Zamora", new[] { "Zamora", "Benavente", "Toro" } },
            { "Zaragoza", new[] { "Zaragoza", "Calatayud", "Utebo", "Ejea de los Caballeros", "Cuarte de Huerva", "Tarazona", "Caspe", "La Almunia de Doña Godina" } },
            { "Ceuta", new[] { "Ceuta" } },
            { "Melilla", new[] { "Melilla" } }
        }.ToDictionary(kvp => kvp.Key, kvp => (IReadOnlyList<string>)kvp.Value).AsReadOnly();

        /// <summary>
        /// Mapeo específico de códigos postales a ciudades
        /// </summary>
        public static IReadOnlyDictionary<string, string> CodigosPostalesCiudades { get; } = new Dictionary<string, string>
        {
            // Madrid capital
            { "28001", "Madrid" }, { "28002", "Madrid" }, { "28003", "Madrid" }, { "28004", "Madrid" }, { "28005", "Madrid" },
            { "28006", "Madrid" }, { "28007", "Madrid" }, { "28008", "Madrid" }, { "28009", "Madrid" }, { "28010", "Madrid" },
            { "28011", "Madrid" }, { "28012", "Madrid" }, { "28013", "Madrid" }, { "28014", "Madrid" }, { "28015", "Madrid" },
            { "28016", "Madrid" }, { "28017", "Madrid" }, { "28018", "Madrid" }, { "28019", "Madrid" }, { "28020", "Madrid" },
            { "28021", "Madrid" }, { "28022", "Madrid" }, { "28023", "Madrid" }, { "28024", "Madrid" }, { "28025", "Madrid" },
            { "28026", "Madrid" }, { "28027", "Madrid" }, { "28028", "Madrid" }, { "28029", "Madrid" }, { "28030", "Madrid" },
            { "28031", "Madrid" }, { "28032", "Madrid" }, { "28033", "Madrid" }, { "28034", "Madrid" }, { "28035", "Madrid" },
            { "28036", "Madrid" }, { "28037", "Madrid" }, { "28038", "Madrid" }, { "28039", "Madrid" }, { "28040", "Madrid" },
            { "28041", "Madrid" }, { "28042", "Madrid" }, { "28043", "Madrid" }, { "28044", "Madrid" }, { "28045", "Madrid" },
            { "28046", "Madrid" }, { "28047", "Madrid" }, { "28048", "Madrid" }, { "28049", "Madrid" }, { "28050", "Madrid" },
            { "28051", "Madrid" }, { "28052", "Madrid" }, { "28053", "Madrid" }, { "28054", "Madrid" }, { "28055", "Madrid" },
            // Barcelona capital
            { "08001", "Barcelona" }, { "08002", "Barcelona" }, { "08003", "Barcelona" }, { "08004", "Barcelona" }, { "08005", "Barcelona" },
            { "08006", "Barcelona" }, { "08007", "Barcelona" }, { "08008", "Barcelona" }, { "08009", "Barcelona" }, { "08010", "Barcelona" },
            { "08011", "Barcelona" }, { "08012", "Barcelona" }, { "08013", "Barcelona" }, { "08014", "Barcelona" }, { "08015", "Barcelona" },
            { "08016", "Barcelona" }, { "08017", "Barcelona" }, { "08018", "Barcelona" }, { "08019", "Barcelona" }, { "08020", "Barcelona" },
            { "08021", "Barcelona" }, { "08022", "Barcelona" }, { "08023", "Barcelona" }, { "08024", "Barcelona" }, { "08025", "Barcelona" },
            { "08026", "Barcelona" }, { "08027", "Barcelona" }, { "08028", "Barcelona" }, { "08029", "Barcelona" }, { "08030", "Barcelona" },
            { "08031", "Barcelona" }, { "08032", "Barcelona" }, { "08033", "Barcelona" }, { "08034", "Barcelona" }, { "08035", "Barcelona" },
            { "08036", "Barcelona" }, { "08037", "Barcelona" }, { "08038", "Barcelona" }, { "08039", "Barcelona" }, { "08040", "Barcelona" },
            { "08041", "Barcelona" }, { "08042", "Barcelona" },
            // Valencia capital
            { "46001", "Valencia" }, { "46002", "Valencia" }, { "46003", "Valencia" }, { "46004", "Valencia" }, { "46005", "Valencia" },
            { "46006", "Valencia" }, { "46007", "Valencia" }, { "46008", "Valencia" }, { "46009", "Valencia" }, { "46010", "Valencia" },
            { "46011", "Valencia" }, { "46012", "Valencia" }, { "46013", "Valencia" }, { "46014", "Valencia" }, { "46015", "Valencia" },
            { "46016", "Valencia" }, { "46017", "Valencia" }, { "46018", "Valencia" }, { "46019", "Valencia" }, { "46020", "Valencia" },
            { "46021", "Valencia" }, { "46022", "Valencia" }, { "46023", "Valencia" }, { "46024", "Valencia" }, { "46025", "Valencia" },
            { "46026", "Valencia" },
            // Sevilla capital
            { "41001", "Sevilla" }, { "41002", "Sevilla" }, { "41003", "Sevilla" }, { "41004", "Sevilla" }, { "41005", "Sevilla" },
            { "41006", "Sevilla" }, { "41007", "Sevilla" }, { "41008", "Sevilla" }, { "41009", "Sevilla" }, { "41010", "Sevilla" },
            { "41011", "Sevilla" }, { "41012", "Sevilla" }, { "41013", "Sevilla" }, { "41014", "Sevilla" }, { "41015", "Sevilla" },
            { "41016", "Sevilla" }, { "41017", "Sevilla" }, { "41018", "Sevilla" },
            // Zaragoza capital
            { "50001", "Zaragoza" }, { "50002", "Zaragoza" }, { "50003", "Zaragoza" }, { "50004", "Zaragoza" }, { "50005", "Zaragoza" },
            { "50006", "Zaragoza" }, { "50007", "Zaragoza" }, { "50008", "Zaragoza" }, { "50009", "Zaragoza" }, { "50010", "Zaragoza" },
            { "50011", "Zaragoza" }, { "50012", "Zaragoza" }, { "50013", "Zaragoza" }, { "50014", "Zaragoza" }, { "50015", "Zaragoza" },
            { "50016", "Zaragoza" }, { "50017", "Zaragoza" }, { "50018", "Zaragoza" },
            // Málaga capital
            { "29001", "Málaga" }, { "29002", "Málaga" }, { "29003", "Málaga" }, { "29004", "Málaga" }, { "29005", "Málaga" },
            { "29006", "Málaga" }, { "29007", "Málaga" }, { "29008", "Málaga" }, { "29009", "Málaga" }, { "29010", "Málaga" },
            { "29011", "Málaga" }, { "29012", "Málaga" }, { "29013", "Málaga" }, { "29014", "Málaga" }, { "29015", "Málaga" },
            { "29016", "Málaga" }, { "29017", "Málaga" }, { "29018", "Málaga" },
            // Murcia capital
            { "30001", "Murcia" }, { "30002", "Murcia" }, { "30003", "Murcia" }, { "30004", "Murcia" }, { "30005", "Murcia" },
            { "30006", "Murcia" }, { "30007", "Murcia" }, { "30008", "Murcia" }, { "30009", "Murcia" }, { "30010", "Murcia" },
            { "30011", "Murcia" }, { "30012", "Murcia" },
            // Bilbao
            { "48001", "Bilbao" }, { "48002", "Bilbao" }, { "48003", "Bilbao" }, { "48004", "Bilbao" }, { "48005", "Bilbao" },
            { "48006", "Bilbao" }, { "48007", "Bilbao" }, { "48008", "Bilbao" }, { "48009", "Bilbao" }, { "48010", "Bilbao" },
            { "48011", "Bilbao" }, { "48012", "Bilbao" }, { "48013", "Bilbao" }, { "48014", "Bilbao" }, { "48015", "Bilbao" },
            // Toledo - Illescas (zona del cliente)
            { "45200", "Illescas" }, { "45210", "Villaluenga de la Sagra" }, { "45220", "Yeles" },
            { "45230", "Numancia de la Sagra" }, { "45240", "Alameda de la Sagra" },
            { "45001", "Toledo" }, { "45002", "Toledo" }, { "45003", "Toledo" }, { "45004", "Toledo" }, { "45005", "Toledo" },
            { "45006", "Toledo" }, { "45007", "Toledo" }, { "45008", "Toledo" },
            // Municipios de Madrid
            { "28901", "Getafe" }, { "28902", "Getafe" }, { "28903", "Getafe" }, { "28904", "Getafe" }, { "28905", "Getafe" },
            { "28906", "Getafe" },
            { "28850", "Torrejón de Ardoz" },
            { "28980", "Parla" }, { "28981", "Parla" }, { "28982", "Parla" }, { "28983", "Parla" },
            { "28936", "Móstoles" },
            { "28801", "Alcalá de Henares" }, { "28802", "Alcalá de Henares" }, { "28803", "Alcalá de Henares" },
            { "28804", "Alcalá de Henares" }, { "28805", "Alcalá de Henares" }, { "28806", "Alcalá de Henares" },
            { "28807", "Alcalá de Henares" },
            { "28911", "Leganés" }, { "28912", "Leganés" }, { "28913", "Leganés" }, { "28914", "Leganés" }, { "28915", "Leganés" },
            { "28916", "Leganés" }, { "28917", "Leganés" }, { "28918", "Leganés" }, { "28919", "Leganés" },
            { "28921", "Alcorcón" }, { "28922", "Alcorcón" }, { "28923", "Alcorcón" }, { "28924", "Alcorcón" }, { "28925", "Alcorcón" },
        }.AsReadOnly();
    }
}
