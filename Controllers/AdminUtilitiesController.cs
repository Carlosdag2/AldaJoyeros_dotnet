using Microsoft.AspNetCore.Mvc;
using AldaJoyeros.Utilities;

namespace AldaJoyeros.Controllers
{
    /// <summary>
    /// Controlador para utilidades de administración
    /// </summary>
    public class AdminUtilitiesController : BaseController
    {
        private readonly ImageMigrationUtility _migrationUtility;
        private readonly ILogger<AdminUtilitiesController> _logger;

        public AdminUtilitiesController(
            ImageMigrationUtility migrationUtility,
            ILogger<AdminUtilitiesController> logger)
        {
            _migrationUtility = migrationUtility;
            _logger = logger;
        }

        // GET: AdminUtilities/Migration
        [HttpGet]
        public IActionResult Migration()
        {
            if (!IsAuthenticated || !IsAdmin)
            {
                return RedirectToAction("Login", "Auth");
            }

            return View();
        }

        // POST: AdminUtilities/GetMySqlStats
        [HttpPost]
        public async Task<IActionResult> GetMySqlStats()
        {
            if (!IsAuthenticated || !IsAdmin)
            {
                return Json(new { success = false, message = "No autorizado" });
            }

            try
            {
                var stats = await _migrationUtility.GetMySqlStatsAsync();
                
                return Json(new
                {
                    success = true,
                    tableExists = stats.TableExists,
                    totalImages = stats.TotalImages,
                    totalSizeBytes = stats.TotalSizeBytes,
                    totalSizeMB = stats.TotalSizeMB
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo estadísticas de MySQL");
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        // POST: AdminUtilities/TestMongoConnection
        [HttpPost]
        public async Task<IActionResult> TestMongoConnection()
        {
            if (!IsAuthenticated || !IsAdmin)
            {
                return Json(new { success = false, message = "No autorizado" });
            }

            try
            {
                var connected = await _migrationUtility.TestMongoConnectionAsync();
                
                if (connected)
                {
                    return Json(new { success = true, message = "? Conexión a MongoDB exitosa" });
                }
                else
                {
                    return Json(new { success = false, message = "? No se pudo conectar a MongoDB" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error probando conexión MongoDB");
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        // POST: AdminUtilities/MigrateImages
        [HttpPost]
        public async Task<IActionResult> MigrateImages([FromForm] bool clearExisting = false)
        {
            if (!IsAuthenticated || !IsAdmin)
            {
                return Json(new { success = false, message = "No autorizado" });
            }

            try
            {
                _logger.LogInformation("Iniciando migración de imágenes desde interfaz web");

                // Limpiar colección si se solicita
                if (clearExisting)
                {
                    var cleared = await _migrationUtility.ClearMongoCollectionAsync();
                    if (!cleared)
                    {
                        return Json(new { success = false, message = "Error limpiando colección de MongoDB" });
                    }
                }

                // Ejecutar migración
                var result = await _migrationUtility.MigrateAllImagesAsync();

                return Json(new
                {
                    success = result.Success > 0 || result.TotalProcessed == 0,
                    message = result.Message,
                    totalProcessed = result.TotalProcessed,
                    successCount = result.Success,
                    errorCount = result.Errors
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en migración de imágenes");
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        // POST: AdminUtilities/ClearMongoImages
        [HttpPost]
        public async Task<IActionResult> ClearMongoImages()
        {
            if (!IsAuthenticated || !IsAdmin)
            {
                return Json(new { success = false, message = "No autorizado" });
            }

            try
            {
                var cleared = await _migrationUtility.ClearMongoCollectionAsync();
                
                if (cleared)
                {
                    return Json(new { success = true, message = "? Colección de MongoDB limpiada" });
                }
                else
                {
                    return Json(new { success = false, message = "? Error limpiando colección" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error limpiando colección MongoDB");
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }
    }
}
