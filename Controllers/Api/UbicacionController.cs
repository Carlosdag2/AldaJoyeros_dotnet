using AldaJoyeros.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AldaJoyeros.Controllers.Api
{
    [ApiController]
    [Route("api/[controller]")]
    public class UbicacionController : ControllerBase
    {
        private readonly ICodigoPostalService _codigoPostalService;

        public UbicacionController(ICodigoPostalService codigoPostalService)
        {
            _codigoPostalService = codigoPostalService;
        }

        /// <summary>
        /// Obtiene todas las provincias de España
        /// </summary>
        [HttpGet("provincias")]
        public IActionResult ObtenerProvincias()
        {
            var provincias = _codigoPostalService.ObtenerProvincias();
            return Ok(provincias);
        }

        /// <summary>
        /// Obtiene las ciudades de una provincia
        /// </summary>
        [HttpGet("ciudades/{provincia}")]
        public IActionResult ObtenerCiudades(string provincia)
        {
            if (string.IsNullOrEmpty(provincia))
            {
                return BadRequest(new { error = "La provincia es requerida" });
            }

            var ciudades = _codigoPostalService.ObtenerCiudadesPorProvincia(provincia);
            return Ok(ciudades);
        }

        /// <summary>
        /// Busca información de ubicación por código postal
        /// </summary>
        [HttpGet("codigopostal/{cp}")]
        public IActionResult BuscarPorCodigoPostal(string cp)
        {
            if (string.IsNullOrEmpty(cp) || !_codigoPostalService.EsCodigoPostalValido(cp))
            {
                return BadRequest(new { error = "El código postal debe tener 5 dígitos válidos" });
            }

            var info = _codigoPostalService.BuscarPorCodigoPostal(cp);
            
            if (info == null)
            {
                return NotFound(new { error = "Código postal no válido para España" });
            }

            return Ok(info);
        }
    }
}
