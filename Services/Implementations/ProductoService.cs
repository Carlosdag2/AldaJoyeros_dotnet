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
    }
}
