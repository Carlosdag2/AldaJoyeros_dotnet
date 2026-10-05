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
        public async Task<IEnumerable<ProductoDto>> GetAllAsync(bool cargarImagenes = true)
        {
            var productos = await _productoRepository.GetAllAsync();
            var productosDto = _mapper.Map<IEnumerable<ProductoDto>>(productos).ToList();
            
            if (cargarImagenes) await CargarImagenesAsync(productosDto);
            
            return productosDto;
        }

        /// <summary>
        /// Obtiene todos los productos incluyendo eliminados (para administración)
        /// </summary>
        public async Task<IEnumerable<ProductoDto>> GetAllIncludingDeletedAsync(bool cargarImagenes = true)
        {
            var productos = await _productoRepository.GetAllIncludingDeletedAsync();
            var productosDto = _mapper.Map<IEnumerable<ProductoDto>>(productos).ToList();
            
            if (cargarImagenes) await CargarImagenesAsync(productosDto);
            
            return productosDto;
        }

        public async Task<ProductoDto?> GetByIdAsync(long id)
        {
            var producto = await _productoRepository.GetByIdAsync(id);
            if (producto == null) return null;
            
            var productoDto = _mapper.Map<ProductoDto>(producto);
            CompletarDatosProveedor(producto, productoDto);
            
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
            CompletarDatosProveedor(producto, productoDto);
            
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

        public async Task<IEnumerable<ProductoDto>> GetByCategoriaAsync(long categoriaId, bool cargarImagenes = true)
        {
            var productos = await _productoRepository.GetByCategoriaAsync(categoriaId);
            var productosDto = _mapper.Map<IEnumerable<ProductoDto>>(productos).ToList();
            
            if (cargarImagenes) await CargarImagenesAsync(productosDto);
            
            return productosDto;
        }

        /// <summary>
        /// Busca productos por término de búsqueda con filtro opcional de categoría
        /// </summary>
        public async Task<IEnumerable<ProductoDto>> BuscarAsync(string termino, long? categoriaId = null, int limite = 0, bool cargarImagenes = true)
        {
            if (string.IsNullOrWhiteSpace(termino))
            {
                return Enumerable.Empty<ProductoDto>();
            }

            var productos = await _productoRepository.GetSearchCandidatesAsync(categoriaId);
            var search = new AldaJoyeros.Helpers.CatalogSearch(termino);
            
            // Búsqueda con scoring para mejores resultados
            var resultadosConScore = productos.Select(p => new
            {
                Producto = p,
                Score = search.Score(p.Nombre, p.Descripcion, p.Categoria?.Nombre ?? "",
                    p.Proveedores.Where(s => s.Estado == "completo").Select(s => s.ReferenciaExterna).Prepend(p.Nombre),
                    p.Proveedores.Where(s => s.Estado == "completo").SelectMany(s =>
                        AldaJoyeros.Helpers.CatalogSearch.AttributeText(s.CaracteristicasJson)
                            .Concat(new[] { s.DescripcionCompleta, s.Marca, s.Coleccion })))
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score).ThenBy(x => x.Producto.Id)
            .Select(x => x.Producto);

            if (limite > 0)
            {
                resultadosConScore = resultadosConScore.Take(limite);
            }

            var resultados = _mapper.Map<List<ProductoDto>>(resultadosConScore.ToList());
            if (cargarImagenes) await CargarImagenesAsync(resultados);
            return resultados;
        }

        /// <summary>
        /// Calcula un score de relevancia para ordenar los resultados de búsqueda
        /// </summary>
        public static void CompletarDatosProveedor(Entities.Producto producto, ProductoDto dto)
        {
            var proveedor = producto.Proveedores.FirstOrDefault(p => (p.Proveedor == "Munreco" || p.Proveedor == "PerezMora") && p.Estado == "completo");
            if (proveedor == null) return;
            dto.Marca = proveedor.Marca;
            dto.Coleccion = proveedor.Coleccion;
            if (proveedor.CreadoPorImportacion && !string.IsNullOrWhiteSpace(proveedor.DescripcionCompleta))
                dto.DescripcionCompleta = proveedor.Proveedor == "PerezMora" && producto.Descripcion != proveedor.DescripcionCompleta
                    ? producto.Descripcion ?? "" : proveedor.DescripcionCompleta;
            using var json = System.Text.Json.JsonDocument.Parse(proveedor.CaracteristicasJson);
            static IEnumerable<System.Text.Json.JsonElement> Valores(System.Text.Json.JsonElement node)
            {
                if (node.ValueKind == System.Text.Json.JsonValueKind.Object)
                    return node.EnumerateObject().Select(p => p.Value).ToArray();
                if (node.ValueKind == System.Text.Json.JsonValueKind.Array)
                    return node.EnumerateArray().ToArray();
                return Array.Empty<System.Text.Json.JsonElement>();
            }
            foreach (var grupo in Valores(json.RootElement))
            {
                if (!grupo.TryGetProperty("attribute", out var atributos)) continue;
                var nombreGrupo = grupo.TryGetProperty("name", out var nombre) ? nombre.GetString() ?? "" : "";
                foreach (var atributo in Valores(atributos))
                {
                    if (!atributo.TryGetProperty("name", out var etiqueta) || !atributo.TryGetProperty("text", out var valor)) continue;
                    dto.Caracteristicas.Add(new CaracteristicaProductoDto
                    {
                        Grupo = nombreGrupo, Nombre = etiqueta.GetString() ?? "", Valor = valor.GetString() ?? ""
                    });
                }
            }
        }

        public async Task CargarImagenesAsync(IEnumerable<ProductoDto> productos)
        {
            foreach (var producto in productos)
            {
                producto.Imagenes = (await _imagenService.GetByProductoIdAsync(producto.Id)).ToList();
            }
        }

    }
}
