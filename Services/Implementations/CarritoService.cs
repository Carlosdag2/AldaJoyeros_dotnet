using AldaJoyeros.Mappings;
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
        private readonly AppMapper _mapper;
        private readonly IHttpContextAccessor? _http;
        private readonly AldaJoyeros.Services.CatalogoService _catalogo;

        public CarritoService(
            ICarritoRepository carritoRepository,
            IProductoRepository productoRepository,
            AppMapper mapper, AldaJoyeros.Services.CatalogoService catalogo, IHttpContextAccessor? http = null)
        {
            _carritoRepository = carritoRepository;
            _productoRepository = productoRepository;
            _mapper = mapper;
            _catalogo = catalogo;
            _http = http;
        }

        public async Task<IEnumerable<CarritoItemDto>> GetByUsuarioIdAsync(long usuarioId)
        {
            var items = await _carritoRepository.GetByUsuarioIdAsync(usuarioId);
            var quote=AldaJoyeros.Helpers.CartPaymentQuote.Get(_http?.HttpContext);
            var locked=quote?.UserId==usuarioId && quote.Fingerprint==AldaJoyeros.Helpers.CartPaymentQuote.Signature(_mapper.Map<IEnumerable<CarritoItemDto>>(items));
            var result=new List<CarritoItemDto>();
            foreach(var item in items) {
                var price=locked?null:await _catalogo.PriceAsync(item.ProductoId);
                if(price!=null&&item.Precio!=price.Price){item.Precio=price.Price;await _carritoRepository.UpdateAsync(item);}
                var dto=_mapper.Map<CarritoItemDto>(item);
                if(dto.Producto!=null){dto.Producto.Precio=item.Precio;dto.Producto.PrecioAnterior=price?.Previous;dto.Producto.PrecioSinDescuento=price?.BasePrice;dto.Producto.DescuentoPorcentaje=price?.Percent??0;}
                result.Add(dto);
            }
            return result;
        }

        public async Task<CarritoItemDto> AddItemAsync(long usuarioId, CarritoItemCreateDto carritoItemCreateDto)
        {
            if (carritoItemCreateDto.Cantidad is < 1 or > 99) throw new InvalidOperationException("La cantidad debe estar entre 1 y 99.");
            if (!await _catalogo.IsPurchasableAsync(carritoItemCreateDto.ProductoId))
                throw new InvalidOperationException("Selecciona una talla disponible en la ficha del producto antes de añadirlo al carrito.");
            var producto = await _productoRepository.GetByIdAsync(carritoItemCreateDto.ProductoId);
            if (producto == null)
            {
                throw new KeyNotFoundException("Producto no encontrado");
            }

            var existingItem = await _carritoRepository.GetByUsuarioAndProductoAsync(usuarioId, carritoItemCreateDto.ProductoId);
            
            if (existingItem != null)
            {
                if (existingItem.Cantidad > 99 - carritoItemCreateDto.Cantidad) throw new InvalidOperationException("La cantidad máxima por producto es 99.");
                existingItem.Cantidad += carritoItemCreateDto.Cantidad;
                if(AldaJoyeros.Helpers.CartPaymentQuote.Get(_http?.HttpContext)==null) existingItem.Precio=(await _catalogo.PriceAsync(producto.Id))?.Price??producto.Precio;
                var updatedItem = await _carritoRepository.UpdateAsync(existingItem);
                return _mapper.Map<CarritoItemDto>(updatedItem);
            }

            var carritoItem = new CarritoItem
            {
                UsuarioId = usuarioId,
                ProductoId = carritoItemCreateDto.ProductoId,
                Cantidad = carritoItemCreateDto.Cantidad,
                Precio = (await _catalogo.PriceAsync(producto.Id))?.Price ?? producto.Precio
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

            if (carritoItemUpdateDto.Cantidad is < 1 or > 99) throw new InvalidOperationException("La cantidad debe estar entre 1 y 99.");
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
            var items = await GetByUsuarioIdAsync(usuarioId);
            return items.Sum(i => i.Cantidad * i.Precio);
        }

        public async Task<int> GetTotalItemsAsync(long usuarioId)
        {
            var items = await _carritoRepository.GetByUsuarioIdAsync(usuarioId);
            return items.Sum(i => i.Cantidad);
        }
    }
}
