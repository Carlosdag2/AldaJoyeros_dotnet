using AutoMapper;
using AldaJoyeros.DTOs;
using AldaJoyeros.Repositories.Interfaces;
using AldaJoyeros.Services.Interfaces;

namespace AldaJoyeros.Services.Implementations
{
    public class ProductoService : IProductoService
    {
        private readonly IProductoRepository _productoRepository;
        private readonly IProductoImagenService _imagenService;
        private readonly IMapper _mapper;

        public ProductoService(
            IProductoRepository productoRepository,
            IProductoImagenService imagenService,
            IMapper mapper)
        {
            _productoRepository = productoRepository;
            _imagenService = imagenService;
            _mapper = mapper;
        }

        /// <summary>
        /// Obtiene todos los productos activos (no eliminados)
        /// </summary>
        public async Task<IEnumerable<ProductoDto>> GetAllAsync()
        {
            var productos = await _productoRepository.GetAllAsync();
            var productosDto = _mapper.Map<IEnumerable<ProductoDto>>(productos);
            
            foreach (var productoDto in productosDto)
            {
                var imagenes = await _imagenService.GetByProductoIdAsync(productoDto.Id);
                productoDto.Imagenes = imagenes.ToList();
            }
            
            return productosDto;
        }

        /// <summary>
        /// Obtiene todos los productos incluyendo eliminados (para administración)
        /// </summary>
        public async Task<IEnumerable<ProductoDto>> GetAllIncludingDeletedAsync()
        {
            var productos = await _productoRepository.GetAllIncludingDeletedAsync();
            var productosDto = _mapper.Map<IEnumerable<ProductoDto>>(productos);
            
            foreach (var productoDto in productosDto)
            {
                var imagenes = await _imagenService.GetByProductoIdAsync(productoDto.Id);
                productoDto.Imagenes = imagenes.ToList();
            }
            
            return productosDto;
        }

        public async Task<ProductoDto?> GetByIdAsync(long id)
        {
            var producto = await _productoRepository.GetByIdAsync(id);
            if (producto == null) return null;
            
            var productoDto = _mapper.Map<ProductoDto>(producto);
            
            var imagenes = await _imagenService.GetByProductoIdAsync(id);
            productoDto.Imagenes = imagenes.ToList();
            
            return productoDto;
        }

        /// <summary>
        /// Obtiene un producto solo si está activo (para el catálogo público)
        /// </summary>
        public async Task<ProductoDto?> GetByIdActiveAsync(long id)
        {
            var producto = await _productoRepository.GetByIdActiveAsync(id);
            if (producto == null) return null;
            
            var productoDto = _mapper.Map<ProductoDto>(producto);
            
            var imagenes = await _imagenService.GetByProductoIdAsync(id);
            productoDto.Imagenes = imagenes.ToList();
            
            return productoDto;
        }

        public async Task<ProductoDto> CreateAsync(ProductoCreateDto productoDto)
        {
            var producto = _mapper.Map<Entities.Producto>(productoDto);
            var createdProducto = await _productoRepository.CreateAsync(producto);
            var result = _mapper.Map<ProductoDto>(createdProducto);
            result.Imagenes = new List<ProductoImagenDto>();
            return result;
        }

        public async Task<ProductoDto> UpdateAsync(long id, ProductoUpdateDto productoDto)
        {
            var producto = await _productoRepository.GetByIdAsync(id);
            if (producto == null)
            {
                throw new KeyNotFoundException("Producto no encontrado");
            }

            _mapper.Map(productoDto, producto);
            var updatedProducto = await _productoRepository.UpdateAsync(producto);
            var result = _mapper.Map<ProductoDto>(updatedProducto);
            
            var imagenes = await _imagenService.GetByProductoIdAsync(id);
            result.Imagenes = imagenes.ToList();
            
            return result;
        }

        /// <summary>
        /// Soft delete: marca el producto como eliminado pero mantiene los datos
        /// </summary>
        public async Task DeleteAsync(long id)
        {
            var producto = await _productoRepository.GetByIdAsync(id);
            if (producto == null)
            {
                throw new KeyNotFoundException("Producto no encontrado");
            }

            // NO eliminamos las imágenes - se mantienen por si se restaura el producto
            // Solo marcamos el producto como eliminado
            await _productoRepository.DeleteAsync(id);
        }

        /// <summary>
        /// Restaura un producto eliminado
        /// </summary>
        public async Task RestoreAsync(long id)
        {
            var producto = await _productoRepository.GetByIdAsync(id);
            if (producto == null)
            {
                throw new KeyNotFoundException("Producto no encontrado");
            }

            await _productoRepository.RestoreAsync(id);
        }

        /// <summary>
        /// Elimina permanentemente un producto y sus imágenes
        /// </summary>
        public async Task HardDeleteAsync(long id)
        {
            var producto = await _productoRepository.GetByIdAsync(id);
            if (producto == null)
            {
                throw new KeyNotFoundException("Producto no encontrado");
            }

            // Eliminar imágenes de MongoDB
            var imagenes = await _imagenService.GetByProductoIdAsync(id);
            foreach (var imagen in imagenes)
            {
                await _imagenService.DeleteByStringIdAsync(imagen.Id);
            }

            await _productoRepository.HardDeleteAsync(id);
        }

        public async Task<IEnumerable<ProductoDto>> GetByCategoriaAsync(long categoriaId)
        {
            var productos = await _productoRepository.GetByCategoriaAsync(categoriaId);
            var productosDto = _mapper.Map<IEnumerable<ProductoDto>>(productos);
            
            foreach (var productoDto in productosDto)
            {
                var imagenes = await _imagenService.GetByProductoIdAsync(productoDto.Id);
                productoDto.Imagenes = imagenes.ToList();
            }
            
            return productosDto;
        }

        /// <summary>
        /// Busca productos por término de búsqueda con filtro opcional de categoría
        /// </summary>
        public async Task<IEnumerable<ProductoDto>> BuscarAsync(string termino, long? categoriaId = null, int limite = 0)
        {
            if (string.IsNullOrWhiteSpace(termino))
            {
                return Enumerable.Empty<ProductoDto>();
            }

            var productos = categoriaId.HasValue
                ? await GetByCategoriaAsync(categoriaId.Value)
                : await GetAllAsync();

            // Normalizar el término de búsqueda
            var terminoLower = termino.ToLower();
            
            // Búsqueda con scoring para mejores resultados
            var resultadosConScore = productos.Select(p => new
            {
                Producto = p,
                Score = CalcularScore(p, terminoLower)
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Select(x => x.Producto);

            if (limite > 0)
            {
                resultadosConScore = resultadosConScore.Take(limite);
            }

            return resultadosConScore.ToList();
        }

        /// <summary>
        /// Calcula un score de relevancia para ordenar los resultados de búsqueda
        /// </summary>
        private int CalcularScore(ProductoDto producto, string terminoLower)
        {
            int score = 0;

            // Coincidencia exacta en el nombre (máxima prioridad)
            if (producto.Nombre.Equals(terminoLower, StringComparison.OrdinalIgnoreCase))
                score += 100;

            // El nombre comienza con el término
            if (producto.Nombre.StartsWith(terminoLower, StringComparison.OrdinalIgnoreCase))
                score += 50;

            // El nombre contiene el término
            if (producto.Nombre.Contains(terminoLower, StringComparison.OrdinalIgnoreCase))
                score += 30;

            // La descripción contiene el término
            if (producto.Descripcion != null && producto.Descripcion.Contains(terminoLower, StringComparison.OrdinalIgnoreCase))
                score += 10;

            // La categoría contiene el término
            if (producto.CategoriaNombre.Contains(terminoLower, StringComparison.OrdinalIgnoreCase))
                score += 20;

            return score;
        }
    }
}
