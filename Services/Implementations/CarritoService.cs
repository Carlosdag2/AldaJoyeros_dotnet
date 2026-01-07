using AutoMapper;
using AldaJoyeros.DTOs;
using AldaJoyeros.Entities;
using AldaJoyeros.Repositories.Interfaces;
using AldaJoyeros.Services.Interfaces;

namespace AldaJoyeros.Services.Implementations
{
    public class CarritoService : ICarritoService
    {
        private readonly ICarritoRepository _carritoRepository;
        private readonly IProductoRepository _productoRepository;
        private readonly IMapper _mapper;

        public CarritoService(
            ICarritoRepository carritoRepository,
            IProductoRepository productoRepository,
            IMapper mapper)
        {
            _carritoRepository = carritoRepository;
            _productoRepository = productoRepository;
            _mapper = mapper;
        }

        public async Task<IEnumerable<CarritoItemDto>> GetByUsuarioIdAsync(long usuarioId)
        {
            var items = await _carritoRepository.GetByUsuarioIdAsync(usuarioId);
            return _mapper.Map<IEnumerable<CarritoItemDto>>(items);
        }

        public async Task<CarritoItemDto> AddItemAsync(long usuarioId, CarritoItemCreateDto carritoItemCreateDto)
        {
            var producto = await _productoRepository.GetByIdAsync(carritoItemCreateDto.ProductoId);
            if (producto == null)
            {
                throw new KeyNotFoundException("Producto no encontrado");
            }

            var existingItem = await _carritoRepository.GetByUsuarioAndProductoAsync(usuarioId, carritoItemCreateDto.ProductoId);
            
            if (existingItem != null)
            {
                existingItem.Cantidad += carritoItemCreateDto.Cantidad;
                var updatedItem = await _carritoRepository.UpdateAsync(existingItem);
                return _mapper.Map<CarritoItemDto>(updatedItem);
            }

            var carritoItem = new CarritoItem
            {
                UsuarioId = usuarioId,
                ProductoId = carritoItemCreateDto.ProductoId,
                Cantidad = carritoItemCreateDto.Cantidad,
                Precio = producto.Precio
            };

            var createdItem = await _carritoRepository.CreateAsync(carritoItem);
            var result = await _carritoRepository.GetByIdAsync(createdItem.Id);
            return _mapper.Map<CarritoItemDto>(result);
        }

        public async Task<CarritoItemDto> UpdateItemAsync(long usuarioId, long itemId, CarritoItemUpdateDto carritoItemUpdateDto)
        {
            var item = await _carritoRepository.GetByIdAsync(itemId);
            if (item == null || item.UsuarioId != usuarioId)
            {
                throw new KeyNotFoundException("Item del carrito no encontrado");
            }

            item.Cantidad = carritoItemUpdateDto.Cantidad;
            var updatedItem = await _carritoRepository.UpdateAsync(item);
            return _mapper.Map<CarritoItemDto>(updatedItem);
        }

        public async Task DeleteItemAsync(long usuarioId, long itemId)
        {
            var item = await _carritoRepository.GetByIdAsync(itemId);
            if (item == null || item.UsuarioId != usuarioId)
            {
                throw new KeyNotFoundException("Item del carrito no encontrado");
            }

            await _carritoRepository.DeleteAsync(itemId);
        }

        public async Task ClearCarritoAsync(long usuarioId)
        {
            await _carritoRepository.DeleteByUsuarioIdAsync(usuarioId);
        }

        public async Task<double> GetTotalAsync(long usuarioId)
        {
            var items = await _carritoRepository.GetByUsuarioIdAsync(usuarioId);
            return items.Sum(i => i.Cantidad * i.Precio);
        }

        public async Task<int> GetTotalItemsAsync(long usuarioId)
        {
            var items = await _carritoRepository.GetByUsuarioIdAsync(usuarioId);
            return items.Sum(i => i.Cantidad);
        }
    }
}
