using AutoMapper;
using AldaJoyeros.DTOs;
using AldaJoyeros.Models.MongoDB;
using AldaJoyeros.Repositories.Interfaces;
using AldaJoyeros.Services.Interfaces;

namespace AldaJoyeros.Services.Implementations
{
    /// <summary>
    /// Servicio de imágenes usando MongoDB
    /// </summary>
    public class ProductoImagenMongoService : IProductoImagenService
    {
        private readonly IProductoImagenMongoRepository _repository;
        private readonly IMapper _mapper;
        private readonly ILogger<ProductoImagenMongoService> _logger;

        public ProductoImagenMongoService(
            IProductoImagenMongoRepository repository,
            IMapper mapper,
            ILogger<ProductoImagenMongoService> logger)
        {
            _repository = repository;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<IEnumerable<ProductoImagenDto>> GetByProductoIdAsync(long productoId)
        {
            var imagenes = await _repository.GetByProductoIdAsync(productoId);
            return _mapper.Map<IEnumerable<ProductoImagenDto>>(imagenes);
        }

        public async Task<ProductoImagenDto?> GetByIdAsync(long id)
        {
            // Convertir long a string para MongoDB
            var imagen = await _repository.GetByIdAsync(id.ToString());
            return imagen == null ? null : _mapper.Map<ProductoImagenDto>(imagen);
        }

        public async Task<ProductoImagenDto> CreateAsync(ProductoImagenCreateDto imagenDto)
        {
            try
            {
                var imagen = _mapper.Map<ProductoImagenMongo>(imagenDto);
                
                // Calcular tamaño
                imagen.TamanoBytes = imagenDto.ImagenData.Length;
                
                // Si es la primera imagen, marcarla como principal
                var existentes = await _repository.CountByProductoIdAsync(imagenDto.ProductoId);
                if (existentes == 0)
                {
                    imagen.EsPrincipal = true;
                    imagen.Orden = 0;
                }
                else if (imagen.EsPrincipal)
                {
                    // Quitar principal de las demás
                    await _repository.QuitarPrincipalAsync(imagenDto.ProductoId);
                }

                // Verificar que no exista el orden
                if (await _repository.ExistsOrdenAsync(imagenDto.ProductoId, imagen.Orden))
                {
                    // Buscar el siguiente orden disponible
                    var imagenes = await _repository.GetByProductoIdAsync(imagenDto.ProductoId);
                    var ordenesUsados = imagenes.Select(i => i.Orden).ToHashSet();
                    int nuevoOrden = 0;
                    while (ordenesUsados.Contains(nuevoOrden))
                    {
                        nuevoOrden++;
                    }
                    imagen.Orden = nuevoOrden;
                }

                imagen.FechaCreacion = DateTime.UtcNow;
                
                var resultado = await _repository.CreateAsync(imagen);
                
                _logger.LogInformation("Imagen creada en MongoDB con ID: {ImagenId} para producto: {ProductoId}", 
                    resultado.Id, resultado.ProductoId);
                
                return _mapper.Map<ProductoImagenDto>(resultado);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear imagen en MongoDB para producto: {ProductoId}", imagenDto.ProductoId);
                throw;
            }
        }

        public async Task<ProductoImagenDto> CreateFromFileAsync(ProductoImagenUploadDto uploadDto)
        {
            using var memoryStream = new MemoryStream();
            await uploadDto.Archivo.CopyToAsync(memoryStream);

            var imagenData = memoryStream.ToArray();
            var tipoMime = uploadDto.Archivo.ContentType;

            // Validar tipo de imagen
            if (!tipoMime.StartsWith("image/"))
            {
                throw new InvalidOperationException("El archivo debe ser una imagen");
            }

            // Validar tamaño (máximo 5MB)
            if (imagenData.Length > 5 * 1024 * 1024)
            {
                throw new InvalidOperationException("La imagen no puede superar los 5MB");
            }

            var createDto = new ProductoImagenCreateDto
            {
                ProductoId = uploadDto.ProductoId,
                ImagenData = imagenData,
                TipoMime = tipoMime,
                Orden = uploadDto.Orden,
                EsPrincipal = uploadDto.EsPrincipal,
                NombreArchivo = uploadDto.Archivo.FileName
            };

            return await CreateAsync(createDto);
        }

        public async Task DeleteAsync(long id)
        {
            try
            {
                var imagen = await _repository.GetByIdAsync(id.ToString());
                if (imagen == null)
                {
                    throw new KeyNotFoundException("Imagen no encontrada");
                }

                var productoId = imagen.ProductoId;
                var eraPrincipal = imagen.EsPrincipal;

                var deleted = await _repository.DeleteAsync(id.ToString());
                
                if (!deleted)
                {
                    throw new InvalidOperationException("No se pudo eliminar la imagen");
                }

                _logger.LogInformation("Imagen eliminada de MongoDB con ID: {ImagenId}", id);

                // Si era la principal, marcar otra como principal
                if (eraPrincipal)
                {
                    var imagenes = await _repository.GetByProductoIdAsync(productoId);
                    var siguiente = imagenes.OrderBy(i => i.Orden).FirstOrDefault();

                    if (siguiente != null)
                    {
                        siguiente.EsPrincipal = true;
                        await _repository.UpdateAsync(siguiente.Id, siguiente);
                        
                        _logger.LogInformation("Nueva imagen principal establecida: {ImagenId}", siguiente.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar imagen con ID: {ImagenId}", id);
                throw;
            }
        }

        public async Task SetAsPrincipalAsync(long id)
        {
            try
            {
                var imagen = await _repository.GetByIdAsync(id.ToString());
                if (imagen == null)
                {
                    throw new KeyNotFoundException("Imagen no encontrada");
                }

                // Quitar principal de las demás
                await _repository.QuitarPrincipalAsync(imagen.ProductoId, imagen.Id);

                // Marcar como principal
                imagen.EsPrincipal = true;
                await _repository.UpdateAsync(imagen.Id, imagen);
                
                _logger.LogInformation("Imagen establecida como principal: {ImagenId} para producto: {ProductoId}", 
                    imagen.Id, imagen.ProductoId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al establecer imagen principal con ID: {ImagenId}", id);
                throw;
            }
        }
    }
}
