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

        public async Task<IEnumerable<ProductoDto>> GetAllAsync()
        {
            var productos = await _productoRepository.GetAllAsync();
            var productosDto = _mapper.Map<IEnumerable<ProductoDto>>(productos);
            
            // Cargar imágenes desde MongoDB para cada producto
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
            
            // Cargar imágenes desde MongoDB
            var imagenes = await _imagenService.GetByProductoIdAsync(id);
            productoDto.Imagenes = imagenes.ToList();
            
            return productoDto;
        }

        public async Task<ProductoDto> CreateAsync(ProductoCreateDto productoDto)
        {
            var producto = _mapper.Map<Entities.Producto>(productoDto);
            var createdProducto = await _productoRepository.CreateAsync(producto);
            var result = _mapper.Map<ProductoDto>(createdProducto);
            result.Imagenes = new List<ProductoImagenDto>(); // Vacío al crear
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
            
            // Cargar imágenes desde MongoDB
            var imagenes = await _imagenService.GetByProductoIdAsync(id);
            result.Imagenes = imagenes.ToList();
            
            return result;
        }

        public async Task DeleteAsync(long id)
        {
            if (!await _productoRepository.ExistsAsync(id))
            {
                throw new KeyNotFoundException("Producto no encontrado");
            }

            // Eliminar imágenes de MongoDB primero
            var imagenes = await _imagenService.GetByProductoIdAsync(id);
            foreach (var imagen in imagenes)
            {
                // Convertir string ID a long temporalmente para el método Delete
                if (long.TryParse(imagen.Id, out long imagenId))
                {
                    await _imagenService.DeleteAsync(imagenId);
                }
            }

            await _productoRepository.DeleteAsync(id);
        }

        public async Task<IEnumerable<ProductoDto>> GetByCategoriaAsync(long categoriaId)
        {
            var productos = await _productoRepository.GetByCategoriaAsync(categoriaId);
            var productosDto = _mapper.Map<IEnumerable<ProductoDto>>(productos);
            
            // Cargar imágenes desde MongoDB para cada producto
            foreach (var productoDto in productosDto)
            {
                var imagenes = await _imagenService.GetByProductoIdAsync(productoDto.Id);
                productoDto.Imagenes = imagenes.ToList();
            }
            
            return productosDto;
        }
    }
}
